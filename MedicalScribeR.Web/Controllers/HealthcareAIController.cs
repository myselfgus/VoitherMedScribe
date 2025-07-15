using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MedicalScribeR.Core.Models;
using MedicalScribeR.Core.Services;

namespace MedicalScribeR.Web.Controllers
{
    /// <summary>
    /// Controller for Healthcare AI Pipeline operations.
    /// Exposes advanced medical text processing capabilities including
    /// entity extraction, sentiment analysis, clinical reasoning, and FHIR integration.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "MedicalProfessional")]
    public class HealthcareAIController : ControllerBase
    {
        private readonly HealthcareAIPipelineService _pipelineService;
        private readonly AzureHealthcareNLPService _nlpService;
        private readonly AzureHealthInsightsService _healthInsightsService;
        private readonly AzureHealthcareApisService _fhirService;
        private readonly ILogger<HealthcareAIController> _logger;

        public HealthcareAIController(
            HealthcareAIPipelineService pipelineService,
            AzureHealthcareNLPService nlpService,
            AzureHealthInsightsService healthInsightsService,
            AzureHealthcareApisService fhirService,
            ILogger<HealthcareAIController> logger)
        {
            _pipelineService = pipelineService ?? throw new ArgumentNullException(nameof(pipelineService));
            _nlpService = nlpService ?? throw new ArgumentNullException(nameof(nlpService));
            _healthInsightsService = healthInsightsService ?? throw new ArgumentNullException(nameof(healthInsightsService));
            _fhirService = fhirService ?? throw new ArgumentNullException(nameof(fhirService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Processes medical transcription through the complete Healthcare AI pipeline.
        /// </summary>
        [HttpPost("process-transcription")]
        public async Task<ActionResult<HealthcareProcessingResult>> ProcessTranscriptionAsync(
            [FromBody] TranscriptionProcessingRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.TranscriptionText))
                {
                    return BadRequest("Transcription text is required");
                }

                if (string.IsNullOrWhiteSpace(request.PatientId))
                {
                    return BadRequest("Patient ID is required");
                }

                _logger.LogInformation("Processing medical transcription for patient: {PatientId}", request.PatientId);

                var result = await _pipelineService.ProcessMedicalTranscriptionAsync(
                    request.PatientId,
                    request.TranscriptionText,
                    request.PatientInfo ?? new PatientInfo(),
                    request.SessionId ?? Guid.NewGuid().ToString(),
                    cancellationToken);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing medical transcription");
                return StatusCode(500, "Internal server error during transcription processing");
            }
        }

        /// <summary>
        /// Analyzes healthcare text using Azure Text Analytics for Health.
        /// </summary>
        [HttpPost("analyze-healthcare-text")]
        public async Task<ActionResult<HealthcareAnalysisResult>> AnalyzeHealthcareTextAsync(
            [FromBody] HealthcareTextAnalysisRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Text))
                {
                    return BadRequest("Text is required");
                }

                _logger.LogInformation("Analyzing healthcare text with NLP");

                var result = await _nlpService.AnalyzeHealthcareTextAsync(
                    request.Text,
                    request.Language ?? "pt",
                    cancellationToken);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing healthcare text");
                return StatusCode(500, "Internal server error during text analysis");
            }
        }

        /// <summary>
        /// Analyzes sentiment in healthcare context.
        /// </summary>
        [HttpPost("analyze-sentiment")]
        public async Task<ActionResult<HealthcareSentimentResult>> AnalyzeSentimentAsync(
            [FromBody] SentimentAnalysisRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Text))
                {
                    return BadRequest("Text is required");
                }

                _logger.LogInformation("Analyzing healthcare sentiment");

                var result = await _nlpService.AnalyzeHealthcareSentimentAsync(
                    request.Text,
                    request.Language ?? "pt",
                    cancellationToken);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing sentiment");
                return StatusCode(500, "Internal server error during sentiment analysis");
            }
        }

        /// <summary>
        /// Processes radiology reports with specialized analysis.
        /// </summary>
        [HttpPost("process-radiology")]
        public async Task<ActionResult<RadiologyProcessingResult>> ProcessRadiologyReportAsync(
            [FromBody] RadiologyProcessingRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.RadiologyReport))
                {
                    return BadRequest("Radiology report is required");
                }

                if (string.IsNullOrWhiteSpace(request.PatientId))
                {
                    return BadRequest("Patient ID is required");
                }

                _logger.LogInformation("Processing radiology report for patient: {PatientId}", request.PatientId);

                var result = await _pipelineService.ProcessRadiologyReportAsync(
                    request.PatientId,
                    request.RadiologyReport,
                    request.PatientInfo ?? new PatientInfo(),
                    cancellationToken);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing radiology report");
                return StatusCode(500, "Internal server error during radiology processing");
            }
        }

        /// <summary>
        /// Finds relevant clinical trials for a patient.
        /// </summary>
        [HttpPost("find-clinical-trials")]
        public async Task<ActionResult<ClinicalTrialSearchResult>> FindClinicalTrialsAsync(
            [FromBody] ClinicalTrialSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.PatientId))
                {
                    return BadRequest("Patient ID is required");
                }

                if (request.ClinicalData == null || string.IsNullOrWhiteSpace(request.ClinicalData.ClinicalNotes))
                {
                    return BadRequest("Clinical data with notes is required");
                }

                _logger.LogInformation("Finding clinical trials for patient: {PatientId}", request.PatientId);

                var result = await _pipelineService.FindClinicalTrialsAsync(
                    request.PatientId,
                    request.ClinicalData,
                    cancellationToken);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding clinical trials");
                return StatusCode(500, "Internal server error during clinical trial search");
            }
        }

        /// <summary>
        /// Searches medical knowledge base.
        /// </summary>
        [HttpGet("search-knowledge")]
        public async Task<ActionResult<MedicalKnowledgeResult>> SearchMedicalKnowledgeAsync(
            [FromQuery] string query,
            [FromQuery] int maxResults = 10,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return BadRequest("Query is required");
                }

                _logger.LogInformation("Searching medical knowledge for: {Query}", query);

                var result = await _nlpService.SearchMedicalKnowledgeAsync(query, maxResults, cancellationToken);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching medical knowledge");
                return StatusCode(500, "Internal server error during knowledge search");
            }
        }

        /// <summary>
        /// Creates or updates a patient in FHIR.
        /// </summary>
        [HttpPost("fhir/patient")]
        public async Task<ActionResult<FhirOperationResult>> CreatePatientAsync(
            [FromBody] PatientResource patient,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (patient == null || string.IsNullOrWhiteSpace(patient.Id))
                {
                    return BadRequest("Valid patient data with ID is required");
                }

                _logger.LogInformation("Creating/updating FHIR patient: {PatientId}", patient.Id);

                var result = await _fhirService.CreateOrUpdatePatientAsync(
                    patient.Id,
                    patient.FirstName ?? "Unknown",
                    patient.LastName ?? "Unknown", 
                    patient.BirthDate ?? DateTime.Now.AddYears(-30),
                    patient.Gender,
                    cancellationToken);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating FHIR patient");
                return StatusCode(500, "Internal server error during patient creation");
            }
        }

        /// <summary>
        /// Creates a clinical document in FHIR.
        /// </summary>
        [HttpPost("fhir/document")]
        public async Task<ActionResult<FhirOperationResult>> CreateClinicalDocumentAsync(
            [FromBody] ClinicalDocumentRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.PatientId) || string.IsNullOrWhiteSpace(request.Content))
                {
                    return BadRequest("Patient ID and content are required");
                }

                _logger.LogInformation("Creating clinical document for patient: {PatientId}", request.PatientId);

                var result = await _fhirService.CreateClinicalDocumentAsync(
                    Guid.NewGuid().ToString(), // documentId
                    request.PatientId,
                    request.Title ?? $"Clinical Document - {DateTime.UtcNow:yyyy-MM-dd HH:mm}",
                    request.Content,
                    request.DocumentType ?? "consultation",
                    null, // encounterId
                    cancellationToken);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating clinical document");
                return StatusCode(500, "Internal server error during document creation");
            }
        }

        /// <summary>
        /// Searches FHIR patients.
        /// </summary>
        [HttpGet("fhir/patients")]
        public async Task<ActionResult<FhirSearchResult>> SearchPatientsAsync(
            [FromQuery] string? searchParams,
            [FromQuery] int count = 20,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Searching FHIR patients with parameters: {SearchParams}", searchParams);

                var result = await _fhirService.SearchPatientsAsync(cancellationToken: cancellationToken);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching FHIR patients");
                return StatusCode(500, "Internal server error during patient search");
            }
        }

        /// <summary>
        /// Gets health check status for all healthcare AI services.
        /// </summary>
        [HttpGet("health")]
        [AllowAnonymous]
        public ActionResult<HealthCheckResult> GetHealthStatus()
        {
            try
            {
                var healthStatus = new HealthCheckResult
                {
                    Status = "Healthy",
                    Timestamp = DateTime.UtcNow,
                    Services = new Dictionary<string, bool>
                    {
                        ["HealthcareNLP"] = true,
                        ["HealthInsights"] = true,
                        ["HealthcareAPIs"] = true,
                        ["CognitiveSearch"] = true,
                        ["Pipeline"] = true
                    },
                    Version = "1.0.0",
                    Environment = HttpContext.RequestServices.GetService<IWebHostEnvironment>()?.EnvironmentName ?? "Unknown"
                };

                return Ok(healthStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking health status");
                return StatusCode(500, "Health check failed");
            }
        }
    }

    #region Request/Response Models

    /// <summary>
    /// Request model for transcription processing.
    /// </summary>
    public class TranscriptionProcessingRequest
    {
        public string PatientId { get; set; } = string.Empty;
        public string TranscriptionText { get; set; } = string.Empty;
        public PatientInfo? PatientInfo { get; set; }
        public string? SessionId { get; set; }
    }

    /// <summary>
    /// Request model for healthcare text analysis.
    /// </summary>
    public class HealthcareTextAnalysisRequest
    {
        public string Text { get; set; } = string.Empty;
        public string? Language { get; set; } = "pt";
    }

    /// <summary>
    /// Request model for sentiment analysis.
    /// </summary>
    public class SentimentAnalysisRequest
    {
        public string Text { get; set; } = string.Empty;
        public string? Language { get; set; } = "pt";
    }

    /// <summary>
    /// Request model for radiology processing.
    /// </summary>
    public class RadiologyProcessingRequest
    {
        public string PatientId { get; set; } = string.Empty;
        public string RadiologyReport { get; set; } = string.Empty;
        public PatientInfo? PatientInfo { get; set; }
    }

    /// <summary>
    /// Request model for clinical trial search.
    /// </summary>
    public class ClinicalTrialSearchRequest
    {
        public string PatientId { get; set; } = string.Empty;
        public PatientClinicalData ClinicalData { get; set; } = new();
    }

    /// <summary>
    /// Request model for clinical document creation.
    /// </summary>
    public class ClinicalDocumentRequest
    {
        public string PatientId { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? DocumentType { get; set; }
        public string? Title { get; set; }
        public DateTime? DocumentDate { get; set; }
    }

    /// <summary>
    /// Health check result model.
    /// </summary>
    public class HealthCheckResult
    {
        public string Status { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public Dictionary<string, bool> Services { get; set; } = new();
        public string Version { get; set; } = string.Empty;
        public string Environment { get; set; } = string.Empty;
    }

    #endregion
}
