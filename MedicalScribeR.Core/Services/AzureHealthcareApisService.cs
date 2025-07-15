using Azure;
using Azure.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using MedicalScribeR.Core.Interfaces;
using MedicalScribeR.Core.Models;

namespace MedicalScribeR.Core.Services
{
    /// <summary>
    /// Service for Azure Healthcare APIs (FHIR) integration.
    /// Manages patient data, clinical documents, and healthcare workflows.
    /// Reference: https://learn.microsoft.com/en-us/azure/healthcare-apis/healthcare-apis-overview
    /// </summary>
    public class AzureHealthcareApisService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AzureHealthcareApisService> _logger;
        private readonly string _fhirEndpoint;
        private readonly string _workspaceName;
        private readonly TimeSpan _requestTimeout = TimeSpan.FromSeconds(30);
        private readonly int _maxRetryAttempts = 3;
        private readonly string _apiVersion = "2022-12-01";

        public AzureHealthcareApisService(IConfiguration configuration, ILogger<AzureHealthcareApisService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            try
            {
                _workspaceName = configuration["Azure:HealthcareApis:WorkspaceName"] ?? "medicalhub";
                var region = configuration["Azure:HealthcareApis:Region"] ?? "eastus2";
                
                _fhirEndpoint = configuration["Azure:HealthcareApis:FhirEndpoint"] ?? 
                    $"https://{_workspaceName}-fhirservice.fhir.azurehealthcareapis.com";

                // Note: In production, use Managed Identity instead of access token
                var accessToken = configuration["Azure:HealthcareApis:AccessToken"] ?? 
                    throw new InvalidOperationException("Azure:HealthcareApis:AccessToken not configured");

                _httpClient = new HttpClient();
                _httpClient.BaseAddress = new Uri(_fhirEndpoint);
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                _httpClient.DefaultRequestHeaders.Add("Accept", "application/fhir+json");
                _httpClient.DefaultRequestHeaders.Add("Content-Type", "application/fhir+json");
                _httpClient.Timeout = _requestTimeout;

                _logger.LogInformation("Initialized AzureHealthcareApisService with workspace {WorkspaceName} in {Region}", 
                    _workspaceName, region);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Azure Healthcare APIs service");
                throw;
            }
        }

        /// <summary>
        /// Creates or updates a FHIR patient resource.
        /// </summary>
        public async Task<string?> CreateOrUpdatePatientAsync(
            string patientId, 
            string firstName, 
            string lastName, 
            DateTime birthDate, 
            string? gender = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Creating/updating FHIR patient {PatientId}", patientId);

                var patient = CreateFhirPatient(patientId, firstName, lastName, birthDate, gender);
                var json = JsonSerializer.Serialize(patient, new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                });

                var response = await ExecuteWithRetryAsync(async () =>
                {
                    var content = new StringContent(json, Encoding.UTF8, "application/fhir+json");
                    return await _httpClient.PutAsync($"Patient/{patientId}", content, cancellationToken);
                }, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogInformation("Successfully created/updated patient {PatientId}", patientId);
                    return responseContent;
                }

                _logger.LogError("Failed to create/update patient {PatientId}. Status: {StatusCode}", 
                    patientId, response.StatusCode);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating/updating patient {PatientId}", patientId);
                return null;
            }
        }

        /// <summary>
        /// Creates a clinical document composition in FHIR format.
        /// </summary>
        public async Task<string?> CreateClinicalDocumentAsync(
            string documentId,
            string patientId,
            string title,
            string content,
            string documentType = "consultation",
            string? encounterId = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Creating clinical document {DocumentId} for patient {PatientId}", 
                    documentId, patientId);

                var composition = CreateFhirComposition(documentId, patientId, title, content, documentType, encounterId);
                var json = JsonSerializer.Serialize(composition, new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                });

                var response = await ExecuteWithRetryAsync(async () =>
                {
                    var httpContent = new StringContent(json, Encoding.UTF8, "application/fhir+json");
                    return await _httpClient.PutAsync($"Composition/{documentId}", httpContent, cancellationToken);
                }, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogInformation("Successfully created clinical document {DocumentId}", documentId);
                    return responseContent;
                }

                _logger.LogError("Failed to create clinical document {DocumentId}. Status: {StatusCode}", 
                    documentId, response.StatusCode);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating clinical document {DocumentId}", documentId);
                return null;
            }
        }

        /// <summary>
        /// Creates FHIR observations from healthcare entities.
        /// </summary>
        public async Task<List<string>> CreateObservationsFromEntitiesAsync(
            string patientId,
            IEnumerable<HealthcareEntityExtended> entities,
            string? encounterId = null,
            CancellationToken cancellationToken = default)
        {
            var createdObservations = new List<string>();

            try
            {
                _logger.LogInformation("Creating FHIR observations for patient {PatientId}", patientId);

                foreach (var entity in entities)
                {
                    if (!ShouldCreateObservation(entity))
                        continue;

                    var observationId = Guid.NewGuid().ToString();
                    var observation = CreateFhirObservation(observationId, patientId, entity, encounterId);

                    var json = JsonSerializer.Serialize(observation, new JsonSerializerOptions 
                    { 
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = false
                    });

                    var response = await ExecuteWithRetryAsync(async () =>
                    {
                        var content = new StringContent(json, Encoding.UTF8, "application/fhir+json");
                        return await _httpClient.PutAsync($"Observation/{observationId}", content, cancellationToken);
                    }, cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        createdObservations.Add(observationId);
                        _logger.LogDebug("Created observation {ObservationId} for entity {EntityText}", 
                            observationId, entity.Text);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to create observation for entity {EntityText}. Status: {StatusCode}", 
                            entity.Text, response.StatusCode);
                    }
                }

                _logger.LogInformation("Created {Count} observations for patient {PatientId}", 
                    createdObservations.Count, patientId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating observations for patient {PatientId}", patientId);
            }

            return createdObservations;
        }

        /// <summary>
        /// Searches for FHIR patients.
        /// </summary>
        public async Task<string?> SearchPatientsAsync(
            string? firstName = null,
            string? lastName = null,
            DateTime? birthDate = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var queryParams = new List<string>();
                
                if (!string.IsNullOrEmpty(firstName))
                    queryParams.Add($"given={Uri.EscapeDataString(firstName)}");
                
                if (!string.IsNullOrEmpty(lastName))
                    queryParams.Add($"family={Uri.EscapeDataString(lastName)}");
                
                if (birthDate.HasValue)
                    queryParams.Add($"birthdate={birthDate.Value:yyyy-MM-dd}");

                var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
                
                _logger.LogInformation("Searching for patients with query: {Query}", query);

                var response = await ExecuteWithRetryAsync(async () =>
                    await _httpClient.GetAsync($"Patient{query}", cancellationToken), cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogInformation("Patient search completed successfully");
                    return content;
                }

                _logger.LogWarning("Patient search failed. Status: {StatusCode}", response.StatusCode);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching for patients");
                return null;
            }
        }

        /// <summary>
        /// Gets FHIR resource by ID and type.
        /// </summary>
        public async Task<string?> GetResourceAsync(string resourceType, string resourceId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Retrieving {ResourceType}/{ResourceId}", resourceType, resourceId);

                var response = await ExecuteWithRetryAsync(async () =>
                    await _httpClient.GetAsync($"{resourceType}/{resourceId}", cancellationToken), cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogInformation("Successfully retrieved {ResourceType}/{ResourceId}", resourceType, resourceId);
                    return content;
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("{ResourceType}/{ResourceId} not found", resourceType, resourceId);
                    return null;
                }

                _logger.LogWarning("Failed to retrieve {ResourceType}/{ResourceId}. Status: {StatusCode}", 
                    resourceType, resourceId, response.StatusCode);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving {ResourceType}/{ResourceId}", resourceType, resourceId);
                return null;
            }
        }

        /// <summary>
        /// Executes HTTP request with retry logic for transient failures.
        /// </summary>
        private async Task<HttpResponseMessage> ExecuteWithRetryAsync(
            Func<Task<HttpResponseMessage>> operation, 
            CancellationToken cancellationToken)
        {
            var attempt = 0;
            Exception lastException = null!;

            while (attempt < _maxRetryAttempts)
            {
                try
                {
                    var response = await operation();
                    
                    // If successful or non-transient error, return immediately
                    if (response.IsSuccessStatusCode || !IsTransientError(response))
                        return response;

                    lastException = new HttpRequestException($"HTTP {response.StatusCode}: {response.ReasonPhrase}");
                }
                catch (HttpRequestException ex) when (IsTransientHttpError(ex))
                {
                    lastException = ex;
                }
                catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
                {
                    lastException = ex;
                }

                attempt++;
                
                if (attempt < _maxRetryAttempts)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // Exponential backoff
                    _logger.LogWarning("Request failed (attempt {Attempt}/{MaxAttempts}). Retrying in {Delay}ms...", 
                        attempt, _maxRetryAttempts, delay.TotalMilliseconds);
                    
                    await Task.Delay(delay, cancellationToken);
                }
            }

            _logger.LogError(lastException, "Request failed after {MaxAttempts} attempts", _maxRetryAttempts);
            throw lastException;
        }

        /// <summary>
        /// Determines if HTTP response indicates a transient error.
        /// </summary>
        private static bool IsTransientError(HttpResponseMessage response)
        {
            return response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                   response.StatusCode == System.Net.HttpStatusCode.RequestTimeout ||
                   response.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
                   response.StatusCode == System.Net.HttpStatusCode.InternalServerError ||
                   response.StatusCode == System.Net.HttpStatusCode.BadGateway ||
                   response.StatusCode == System.Net.HttpStatusCode.GatewayTimeout;
        }

        /// <summary>
        /// Creates FHIR Patient resource.
        /// </summary>
        private object CreateFhirPatient(string patientId, string firstName, string lastName, DateTime birthDate, string? gender)
        {
            return new
            {
                resourceType = "Patient",
                id = patientId,
                name = new[]
                {
                    new
                    {
                        use = "official",
                        family = lastName,
                        given = new[] { firstName }
                    }
                },
                gender = gender?.ToLowerInvariant() ?? "unknown",
                birthDate = birthDate.ToString("yyyy-MM-dd"),
                active = true,
                meta = new
                {
                    lastUpdated = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    tag = new[]
                    {
                        new
                        {
                            system = "http://voither.com/fhir/tags",
                            code = "medical-scribe",
                            display = "Created by Medical Scribe"
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Creates FHIR Composition resource for clinical documents.
        /// </summary>
        private object CreateFhirComposition(string documentId, string patientId, string title, string content,
            string documentType, string? encounterId)
        {
            return new
            {
                resourceType = "Composition",
                id = documentId,
                status = "final",
                type = new
                {
                    coding = new[]
                    {
                        new
                        {
                            system = "http://loinc.org",
                            code = GetLoincCodeForDocumentType(documentType),
                            display = documentType
                        }
                    }
                },
                subject = new
                {
                    reference = $"Patient/{patientId}"
                },
                encounter = encounterId != null ? new { reference = $"Encounter/{encounterId}" } : null,
                date = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                author = new[]
                {
                    new
                    {
                        display = "VoitherMedicalScribe AI System"
                    }
                },
                title = title,
                section = new[]
                {
                    new
                    {
                        title = title,
                        text = new
                        {
                            status = "generated",
                            div = $"<div xmlns=\"http://www.w3.org/1999/xhtml\">{content}</div>"
                        }
                    }
                },
                confidentiality = "N",
                custodian = new
                {
                    display = "Voither Medical Services"
                },
            };
        }


        /// <summary>
        /// Creates FHIR Observation resource from healthcare entity.
        /// </summary>
        private object CreateFhirObservation(string observationId, string patientId, HealthcareEntityExtended entity, string? encounterId)
        {
            return new
            {
                resourceType = "Observation",
                id = observationId,
                status = "final",
                category = new[]
                {
                    new
                    {
                        coding = new[]
                        {
                            new
                            {
                                system = "http://terminology.hl7.org/CodeSystem/observation-category",
                                code = GetObservationCategory(entity.Category),
                                display = entity.Category
                            }
                        }
                    }
                },
                code = new
                {
                    coding = new[]
                    {
                        new
                        {
                            system = "http://snomed.info/sct",
                            code = entity.IsNegated ? "410546004" : "118598001", // Negated vs Asserted
                            display = entity.Text
                        }
                    },
                    text = entity.Text
                },
                subject = new
                {
                    reference = $"Patient/{patientId}"
                },
                encounter = encounterId != null ? new { reference = $"Encounter/{encounterId}" } : null,
                effectiveDateTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                valueString = entity.Text,
                meta = new
                {
                    lastUpdated = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    tag = new[]
                    {
                        new
                        {
                            system = "http://voither.com/fhir/tags",
                            code = "nlp-extracted",
                            display = "Created by Medical Scribe NLP"
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Determines if an entity should create an observation.
        /// </summary>
        private bool ShouldCreateObservation(HealthcareEntityExtended entity)
        {
            var observableCategories = new[] { "SymptomOrSign", "Condition", "Diagnosis", "VitalSign", "LabResult" };
            return observableCategories.Contains(entity.Category) && entity.ConfidenceScore > 0.7m;
        }

        /// <summary>
        /// Gets LOINC code for document type.
        /// </summary>
        private string GetLoincCodeForDocumentType(string documentType)
        {
            return documentType.ToLowerInvariant() switch
            {
                "consultation" => "11488-4",
                "progress note" => "11506-3",
                "discharge summary" => "18842-5",
                "prescription" => "57833-6",
                "lab report" => "33747-0",
                _ => "34109-9" // General note
            };
        }

        /// <summary>
        /// Gets observation category for entity category.
        /// </summary>
        private string GetObservationCategory(string entityCategory)
        {
            return entityCategory.ToLowerInvariant() switch
            {
                "symptomorsign" => "exam",
                "symptomorSign" => "exam",
                "condition" => "exam",
                "diagnosis" => "exam",
                "vitalsign" => "vital-signs",
                "labresult" => "laboratory",
                _ => "exam"
            };
        }

        /// <summary>
        /// Determines if an HTTP error is transient and can be retried.
        /// </summary>
        private static bool IsTransientHttpError(HttpRequestException ex)
        {
            return ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("network", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("503", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("502", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("429", StringComparison.OrdinalIgnoreCase);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
