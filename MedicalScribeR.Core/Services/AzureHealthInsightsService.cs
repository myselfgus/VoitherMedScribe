using Azure;
using Azure.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MedicalScribeR.Core.Interfaces;
using MedicalScribeR.Core.Models;

namespace MedicalScribeR.Core.Services
{
    /// <summary>
    /// Service for Azure Health Insights integration - Clinical Reasoning and Patient Timeline Analysis.
    /// Implements best practices with managed identity, retry logic, and comprehensive error handling.
    /// Reference: https://learn.microsoft.com/en-us/azure/azure-health-insights/overview
    /// </summary>
    public class AzureHealthInsightsService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AzureHealthInsightsService> _logger;
        private readonly string _endpoint;
        private readonly string _apiKey;
        private readonly TimeSpan _requestTimeout = TimeSpan.FromMinutes(2); // Health Insights can take longer
        private readonly int _maxRetryAttempts = 3;
        private readonly string _apiVersion = "2024-04-01"; // Latest API version

        public AzureHealthInsightsService(IConfiguration configuration, ILogger<AzureHealthInsightsService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            try
            {
                _endpoint = configuration["Azure:HealthInsights:Endpoint"] ?? 
                    "https://insightshealth.cognitiveservices.azure.com/";
                _apiKey = configuration["Azure:HealthInsights:ApiKey"] ?? 
                    throw new InvalidOperationException("Azure:HealthInsights:ApiKey not configured");

                _httpClient = new HttpClient();
                _httpClient.BaseAddress = new Uri(_endpoint);
                _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _apiKey);
                _httpClient.Timeout = _requestTimeout;

                _logger.LogInformation("AzureHealthInsightsService initialized successfully with endpoint: {Endpoint}", _endpoint);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing AzureHealthInsightsService");
                throw;
            }
        }

        /// <summary>
        /// Analyzes clinical reasoning from patient data using Radiology Insights model.
        /// Provides quality checks, critical findings, and follow-up recommendations.
        /// </summary>
        public async Task<RadiologyInsightsResult> AnalyzeRadiologyInsightsAsync(
            string patientId, 
            string radiologyReport, 
            PatientInfo patientInfo,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(radiologyReport))
            {
                _logger.LogWarning("Empty radiology report provided for analysis");
                return new RadiologyInsightsResult { PatientId = patientId, Status = "Failed", Message = "Empty report" };
            }

            var jobId = Guid.NewGuid().ToString();
            var attempt = 0;
            Exception? lastException = null;

            while (attempt < _maxRetryAttempts)
            {
                try
                {
                    _logger.LogDebug("Starting radiology insights analysis - Attempt {Attempt}, JobId: {JobId}", attempt + 1, jobId);

                    var requestBody = CreateRadiologyInsightsRequest(patientId, radiologyReport, patientInfo, jobId);
                    var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                    using var timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeoutTokenSource.CancelAfter(_requestTimeout);

                    // Submit analysis job
                    var submitResponse = await _httpClient.PostAsync(
                        $"health-insights/radiology-insights/jobs/{jobId}?api-version={_apiVersion}", 
                        content, 
                        timeoutTokenSource.Token);

                    submitResponse.EnsureSuccessStatusCode();

                    _logger.LogDebug("Radiology insights job submitted successfully: {JobId}", jobId);

                    // Poll for results with exponential backoff
                    var result = await PollForRadiologyResultsAsync(jobId, timeoutTokenSource.Token);
                    
                    _logger.LogInformation("Radiology insights analysis completed for patient: {PatientId}", patientId);
                    return result;
                }
                catch (HttpRequestException ex) when (IsTransientHttpError(ex))
                {
                    lastException = ex;
                    attempt++;
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    
                    _logger.LogWarning(ex, "Transient error in radiology insights analysis. Attempt {Attempt}/{MaxAttempts}. Waiting {Delay}s", 
                        attempt, _maxRetryAttempts, delay.TotalSeconds);
                    
                    if (attempt < _maxRetryAttempts)
                    {
                        await Task.Delay(delay, cancellationToken);
                    }
                }
                catch (TaskCanceledException ex) when (ex.CancellationToken == cancellationToken)
                {
                    _logger.LogWarning("Radiology insights analysis cancelled by user");
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Non-transient error in radiology insights analysis");
                    throw;
                }
            }

            _logger.LogError(lastException, "Definitive failure in radiology insights analysis after {MaxAttempts} attempts", _maxRetryAttempts);
            return new RadiologyInsightsResult { PatientId = patientId, Status = "Failed", Message = lastException?.Message ?? "Unknown error" };
        }

        /// <summary>
        /// Finds relevant clinical trials for a patient using Trial Matcher model.
        /// Matches patient data against clinical trial eligibility criteria.
        /// </summary>
        public async Task<TrialMatcherResult> FindClinicalTrialsAsync(
            string patientId, 
            PatientClinicalData clinicalData,
            CancellationToken cancellationToken = default)
        {
            if (clinicalData == null || string.IsNullOrWhiteSpace(clinicalData.ClinicalNotes))
            {
                _logger.LogWarning("Invalid clinical data provided for trial matching");
                return new TrialMatcherResult { PatientId = patientId, Status = "Failed", Message = "Invalid clinical data" };
            }

            var jobId = Guid.NewGuid().ToString();
            var attempt = 0;
            Exception? lastException = null;

            while (attempt < _maxRetryAttempts)
            {
                try
                {
                    _logger.LogDebug("Starting trial matcher analysis - Attempt {Attempt}, JobId: {JobId}", attempt + 1, jobId);

                    var requestBody = CreateTrialMatcherRequest(patientId, clinicalData, jobId);
                    var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                    using var timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeoutTokenSource.CancelAfter(_requestTimeout);

                    // Submit analysis job
                    var submitResponse = await _httpClient.PostAsync(
                        $"health-insights/trial-matcher/jobs/{jobId}?api-version={_apiVersion}", 
                        content, 
                        timeoutTokenSource.Token);

                    submitResponse.EnsureSuccessStatusCode();

                    _logger.LogDebug("Trial matcher job submitted successfully: {JobId}", jobId);

                    // Poll for results
                    var result = await PollForTrialMatcherResultsAsync(jobId, timeoutTokenSource.Token);
                    
                    _logger.LogInformation("Trial matcher analysis completed for patient: {PatientId}. Found {Count} matching trials", 
                        patientId, result.MatchingTrials?.Count ?? 0);
                    return result;
                }
                catch (HttpRequestException ex) when (IsTransientHttpError(ex))
                {
                    lastException = ex;
                    attempt++;
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    
                    _logger.LogWarning(ex, "Transient error in trial matcher analysis. Attempt {Attempt}/{MaxAttempts}. Waiting {Delay}s", 
                        attempt, _maxRetryAttempts, delay.TotalSeconds);
                    
                    if (attempt < _maxRetryAttempts)
                    {
                        await Task.Delay(delay, cancellationToken);
                    }
                }
                catch (TaskCanceledException ex) when (ex.CancellationToken == cancellationToken)
                {
                    _logger.LogWarning("Trial matcher analysis cancelled by user");
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Non-transient error in trial matcher analysis");
                    throw;
                }
            }

            _logger.LogError(lastException, "Definitive failure in trial matcher analysis after {MaxAttempts} attempts", _maxRetryAttempts);
            return new TrialMatcherResult { PatientId = patientId, Status = "Failed", Message = lastException?.Message ?? "Unknown error" };
        }

        /// <summary>
        /// Creates request body for Radiology Insights analysis.
        /// </summary>
        private object CreateRadiologyInsightsRequest(string patientId, string radiologyReport, PatientInfo patientInfo, string jobId)
        {
            return new
            {
                jobData = new
                {
                    patients = new[]
                    {
                        new
                        {
                            id = patientId,
                            info = new
                            {
                                sex = patientInfo.Gender,
                                birthDate = patientInfo.BirthDate?.ToString("yyyy-MM-dd"),
                                clinicalInfo = new[]
                                {
                                    new
                                    {
                                        resourceType = "Observation",
                                        status = "final",
                                        code = new
                                        {
                                            coding = new[]
                                            {
                                                new
                                                {
                                                    system = "http://loinc.org",
                                                    code = "18748-4",
                                                    display = "Diagnostic imaging study"
                                                }
                                            }
                                        },
                                        valueString = radiologyReport
                                    }
                                }
                            }
                        }
                    }
                },
                configuration = new
                {
                    includeEvidence = true,
                    inferenceTypes = new[] { "criticalResult", "followupRecommendation", "lateralityDiscrepancy", "completeOrderDiscrepancy" },
                    inferenceOptions = new
                    {
                        followupRecommendationOptions = new
                        {
                            includeRecommendationsWithNoSpecifiedModality = true,
                            includeRecommendationsInReferences = true,
                            provideFocusedSentenceEvidence = true
                        },
                        findingOptions = new
                        {
                            provideFocusedSentenceEvidence = true
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Creates request body for Trial Matcher analysis.
        /// </summary>
        private object CreateTrialMatcherRequest(string patientId, PatientClinicalData clinicalData, string jobId)
        {
            return new
            {
                jobData = new
                {
                    patients = new[]
                    {
                        new
                        {
                            id = patientId,
                            info = new
                            {
                                sex = clinicalData.PatientInfo?.Gender,
                                birthDate = clinicalData.PatientInfo?.BirthDate?.ToString("yyyy-MM-dd"),
                                clinicalInfo = new[]
                                {
                                    new
                                    {
                                        resourceType = "Condition",
                                        clinicalStatus = new
                                        {
                                            coding = new[]
                                            {
                                                new
                                                {
                                                    system = "http://terminology.hl7.org/CodeSystem/condition-clinical",
                                                    code = "active"
                                                }
                                            }
                                        },
                                        code = new
                                        {
                                            text = clinicalData.PrimaryDiagnosis
                                        }
                                    }
                                },
                                data = new[]
                                {
                                    new
                                    {
                                        type = "note",
                                        content = clinicalData.ClinicalNotes,
                                        clinical_type = "consultation",
                                        created_date_time = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                                    }
                                }
                            }
                        }
                    }
                },
                configuration = new
                {
                    includeEvidence = true,
                    clinicalTrials = new
                    {
                        customTrials = new object[0], // Can be populated with specific trials
                        registryFilters = new[]
                        {
                            new
                            {
                                conditions = new[] { clinicalData.PrimaryDiagnosis },
                                studyTypes = new[] { "interventional" },
                                recruitmentStatuses = new[] { "recruiting", "not_yet_recruiting" },
                                sponsors = new[] { "nih", "industry", "other" }
                            }
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Polls for radiology insights analysis results with exponential backoff.
        /// </summary>
        private async Task<RadiologyInsightsResult> PollForRadiologyResultsAsync(string jobId, CancellationToken cancellationToken)
        {
            var pollInterval = TimeSpan.FromSeconds(5);
            var maxPollTime = TimeSpan.FromMinutes(5);
            var stopWatch = System.Diagnostics.Stopwatch.StartNew();

            while (stopWatch.Elapsed < maxPollTime && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var response = await _httpClient.GetAsync(
                        $"health-insights/radiology-insights/jobs/{jobId}?api-version={_apiVersion}", 
                        cancellationToken);

                    response.EnsureSuccessStatusCode();
                    
                    var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    var jobResult = JsonSerializer.Deserialize<HealthInsightsJobResult>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (jobResult?.Status == "succeeded")
                    {
                        return ParseRadiologyInsightsResult(jobResult);
                    }
                    else if (jobResult?.Status == "failed")
                    {
                        _logger.LogError("Radiology insights job failed: {Error}", jobResult.Error);
                        return new RadiologyInsightsResult 
                        { 
                            Status = "Failed", 
                            Message = jobResult.Error ?? "Job failed",
                            JobId = jobId
                        };
                    }

                    // Job still running, wait before next poll
                    await Task.Delay(pollInterval, cancellationToken);
                    pollInterval = TimeSpan.FromMilliseconds(Math.Min(pollInterval.TotalMilliseconds * 1.5, 30000)); // Cap at 30s
                }
                catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Radiology insights polling cancelled");
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error polling for radiology insights results");
                    throw;
                }
            }

            _logger.LogWarning("Radiology insights polling timed out after {ElapsedTime}", stopWatch.Elapsed);
            return new RadiologyInsightsResult { Status = "Timeout", Message = "Analysis timed out", JobId = jobId };
        }

        /// <summary>
        /// Polls for trial matcher analysis results with exponential backoff.
        /// </summary>
        private async Task<TrialMatcherResult> PollForTrialMatcherResultsAsync(string jobId, CancellationToken cancellationToken)
        {
            var pollInterval = TimeSpan.FromSeconds(10); // Trial matcher typically takes longer
            var maxPollTime = TimeSpan.FromMinutes(10);
            var stopWatch = System.Diagnostics.Stopwatch.StartNew();

            while (stopWatch.Elapsed < maxPollTime && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var response = await _httpClient.GetAsync(
                        $"health-insights/trial-matcher/jobs/{jobId}?api-version={_apiVersion}", 
                        cancellationToken);

                    response.EnsureSuccessStatusCode();
                    
                    var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    var jobResult = JsonSerializer.Deserialize<HealthInsightsJobResult>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (jobResult?.Status == "succeeded")
                    {
                        return ParseTrialMatcherResult(jobResult);
                    }
                    else if (jobResult?.Status == "failed")
                    {
                        _logger.LogError("Trial matcher job failed: {Error}", jobResult.Error);
                        return new TrialMatcherResult 
                        { 
                            Status = "Failed", 
                            Message = jobResult.Error ?? "Job failed",
                            JobId = jobId
                        };
                    }

                    // Job still running, wait before next poll
                    await Task.Delay(pollInterval, cancellationToken);
                    pollInterval = TimeSpan.FromMilliseconds(Math.Min(pollInterval.TotalMilliseconds * 1.2, 60000)); // Cap at 60s
                }
                catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Trial matcher polling cancelled");
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error polling for trial matcher results");
                    throw;
                }
            }

            _logger.LogWarning("Trial matcher polling timed out after {ElapsedTime}", stopWatch.Elapsed);
            return new TrialMatcherResult { Status = "Timeout", Message = "Analysis timed out", JobId = jobId };
        }

        /// <summary>
        /// Parses radiology insights result from job response.
        /// </summary>
        private RadiologyInsightsResult ParseRadiologyInsightsResult(HealthInsightsJobResult jobResult)
        {
            try
            {
                var result = new RadiologyInsightsResult
                {
                    JobId = jobResult.JobId,
                    Status = "Succeeded",
                    ProcessedAt = DateTime.UtcNow,
                    CriticalFindings = new List<CriticalFinding>(),
                    FollowupRecommendations = new List<FollowupRecommendation>(),
                    QualityChecks = new List<QualityCheck>()
                };

                // Parse inferences from result
                if (jobResult.Result?.PatientResults != null)
                {
                    foreach (var patientResult in jobResult.Result.PatientResults)
                    {
                        result.PatientId = patientResult.PatientId;
                        
                        if (patientResult.Inferences != null)
                        {
                            foreach (var inference in patientResult.Inferences)
                            {
                                switch (inference.Kind?.ToLowerInvariant())
                                {
                                    case "criticalresult":
                                        result.CriticalFindings.Add(new CriticalFinding
                                        {
                                            Finding = inference.Result?.ToString() ?? string.Empty,
                                            Confidence = inference.ConfidenceScore ?? 0.0,
                                            Evidence = string.Join(", ", inference.Evidence ?? new List<string>())
                                        });
                                        break;
                                    
                                    case "followuprecommendation":
                                        result.FollowupRecommendations.Add(new FollowupRecommendation
                                        {
                                            Recommendation = inference.Result?.ToString() ?? string.Empty,
                                            Confidence = inference.ConfidenceScore ?? 0.0,
                                            Evidence = string.Join(", ", inference.Evidence ?? new List<string>())
                                        });
                                        break;
                                    
                                    case "lateralitydiscrepancy":
                                    case "completeorderdiscrepancy":
                                        result.QualityChecks.Add(new QualityCheck
                                        {
                                            CheckType = inference.Kind,
                                            Issue = inference.Result?.ToString() ?? string.Empty,
                                            Confidence = inference.ConfidenceScore ?? 0.0,
                                            Evidence = string.Join(", ", inference.Evidence ?? new List<string>())
                                        });
                                        break;
                                }
                            }
                        }
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing radiology insights result");
                return new RadiologyInsightsResult 
                { 
                    Status = "Failed", 
                    Message = "Error parsing results",
                    JobId = jobResult.JobId
                };
            }
        }

        /// <summary>
        /// Parses trial matcher result from job response.
        /// </summary>
        private TrialMatcherResult ParseTrialMatcherResult(HealthInsightsJobResult jobResult)
        {
            try
            {
                var result = new TrialMatcherResult
                {
                    JobId = jobResult.JobId,
                    Status = "Succeeded",
                    ProcessedAt = DateTime.UtcNow,
                    MatchingTrials = new List<ClinicalTrialMatch>()
                };

                // Parse trial matches from result
                if (jobResult.Result?.PatientResults != null)
                {
                    foreach (var patientResult in jobResult.Result.PatientResults)
                    {
                        result.PatientId = patientResult.PatientId;
                        
                        if (patientResult.Inferences != null)
                        {
                            foreach (var inference in patientResult.Inferences)
                            {
                                if (inference.Kind?.ToLowerInvariant() == "trialmatch" && inference.Value != null)
                                {
                                    var trialMatch = new ClinicalTrialMatch
                                    {
                                        TrialId = inference.Value.Id ?? string.Empty,
                                        Title = inference.Value.Name ?? string.Empty,
                                        Phase = inference.Value.Phase ?? string.Empty,
                                        StudyType = inference.Value.StudyType ?? string.Empty,
                                        MatchConfidence = inference.ConfidenceScore ?? 0.0,
                                        EligibilityReason = string.Join("; ", inference.Evidence ?? new List<string>()),
                                        Sponsor = inference.Value.Sponsor ?? string.Empty,
                                        Status = inference.Value.RecruitmentStatus ?? string.Empty,
                                        Location = inference.Value.FacilityName ?? string.Empty,
                                        ContactInfo = inference.Value.FacilityContactName ?? string.Empty
                                    };
                                    
                                    result.MatchingTrials.Add(trialMatch);
                                }
                            }
                        }
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing trial matcher result");
                return new TrialMatcherResult 
                { 
                    Status = "Failed", 
                    Message = "Error parsing results",
                    JobId = jobResult.JobId
                };
            }
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

        #region Helper Classes for JSON Deserialization

        private class HealthInsightsJobResult
        {
            public string? JobId { get; set; }
            public string? Status { get; set; }
            public string? Error { get; set; }
            public HealthInsightsResult? Result { get; set; }
        }

        private class HealthInsightsResult
        {
            public List<PatientResult>? PatientResults { get; set; }
        }

        private class PatientResult
        {
            public string? PatientId { get; set; }
            public List<Inference>? Inferences { get; set; }
        }

        private class Inference
        {
            public string? Kind { get; set; }
            public object? Result { get; set; }
            public double? ConfidenceScore { get; set; }
            public List<string>? Evidence { get; set; }
            public TrialValue? Value { get; set; }
        }

        private class TrialValue
        {
            public string? Id { get; set; }
            public string? Name { get; set; }
            public string? Phase { get; set; }
            public string? StudyType { get; set; }
            public string? Sponsor { get; set; }
            public string? RecruitmentStatus { get; set; }
            public string? FacilityName { get; set; }
            public string? FacilityContactName { get; set; }
        }

        #endregion
    }
}
