using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MedicalScribeR.Core.Models;
using MedicalScribeR.Core.Services;

namespace MedicalScribeR.Core.Services
{
    /// <summary>
    /// Orchestrates the complete healthcare AI pipeline integrating:
    /// - Text Analytics for Health (Entity extraction, sentiment)
    /// - Azure Health Insights (Clinical reasoning, trial matching)
    /// - Azure Healthcare APIs (FHIR data management)
    /// - Azure Cognitive Search (Medical knowledge)
    /// </summary>
    public class HealthcareAIPipelineService
    {
        private readonly AzureHealthcareNLPService _nlpService;
        private readonly AzureHealthInsightsService _healthInsightsService;
        private readonly AzureHealthcareApisService _fhirService;
        private readonly AzureAIService _aiService;
        private readonly ILogger<HealthcareAIPipelineService> _logger;

        public HealthcareAIPipelineService(
            AzureHealthcareNLPService nlpService,
            AzureHealthInsightsService healthInsightsService,
            AzureHealthcareApisService fhirService,
            AzureAIService aiService,
            ILogger<HealthcareAIPipelineService> logger)
        {
            _nlpService = nlpService ?? throw new ArgumentNullException(nameof(nlpService));
            _healthInsightsService = healthInsightsService ?? throw new ArgumentNullException(nameof(healthInsightsService));
            _fhirService = fhirService ?? throw new ArgumentNullException(nameof(fhirService));
            _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Processes medical transcription through the complete healthcare AI pipeline.
        /// </summary>
        public async Task<HealthcareProcessingResult> ProcessMedicalTranscriptionAsync(
            string patientId,
            string transcriptionText,
            PatientInfo patientInfo,
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            var result = new HealthcareProcessingResult
            {
                PatientId = patientId,
                SessionId = sessionId,
                ProcessingStarted = DateTime.UtcNow,
                Status = "Processing"
            };

            try
            {
                _logger.LogInformation("Starting healthcare AI pipeline processing for patient: {PatientId}, session: {SessionId}", 
                    patientId, sessionId);

                // Step 1: Healthcare NLP Analysis (Entity extraction, sentiment, relations)
                _logger.LogDebug("Step 1: Performing healthcare NLP analysis");
                var nlpResult = await _nlpService.AnalyzeHealthcareTextAsync(transcriptionText, "pt", cancellationToken);
                
                if (nlpResult.Status != "Succeeded")
                {
                    throw new InvalidOperationException($"NLP analysis failed: {nlpResult.Message}");
                }

                result.NlpAnalysis = nlpResult;
                result.ExtractedEntities = nlpResult.Entities.Cast<HealthcareEntity>().ToList();

                // Step 2: Sentiment Analysis for Healthcare Context
                _logger.LogDebug("Step 2: Performing healthcare sentiment analysis");
                var sentimentResult = await _nlpService.AnalyzeHealthcareSentimentAsync(transcriptionText, "pt", cancellationToken);
                result.SentimentAnalysis = ConvertToBasicSentiment(sentimentResult);

                // Step 3: Clinical Intent Classification
                _logger.LogDebug("Step 3: Classifying clinical intentions");
                var transcriptionChunk = new TranscriptionChunk 
                { 
                    Text = transcriptionText, 
                    SequenceNumber = 1,
                    SessionId = sessionId
                };
                result.IntentionClassification = await _aiService.ClassifyIntentionsAsync(transcriptionChunk, result.ExtractedEntities);

                // Step 4: Medical Knowledge Enrichment
                _logger.LogDebug("Step 4: Enriching with medical knowledge");
                await EnrichWithMedicalKnowledgeAsync(result, cancellationToken);

                // Step 5: Health Insights Analysis (if radiology or complex case)
                if (ContainsRadiologyContent(transcriptionText) || ContainsClinicalTrialCandidate(nlpResult.Entities))
                {
                    _logger.LogDebug("Step 5: Performing Health Insights analysis");
                    await PerformHealthInsightsAnalysisAsync(result, patientId, transcriptionText, patientInfo, cancellationToken);
                }

                // Step 6: FHIR Data Creation and Storage
                _logger.LogDebug("Step 6: Creating and storing FHIR resources");
                await CreateFhirResourcesAsync(result, patientId, transcriptionText, cancellationToken);

                // Step 7: Generate Clinical Summary and Action Items
                _logger.LogDebug("Step 7: Generating clinical summary and action items");
                result.ClinicalSummary = await _aiService.SummarizeConsultationAsync(new[] { transcriptionChunk });
                result.ActionItems = (await _aiService.GenerateActionItemsAsync(transcriptionText)).ToList();

                // Step 8: Quality Assurance and Validation
                _logger.LogDebug("Step 8: Performing quality assurance");
                await PerformQualityAssuranceAsync(result, cancellationToken);

                result.ProcessingCompleted = DateTime.UtcNow;
                result.Status = "Completed";
                result.ProcessingTimeMs = (int)(result.ProcessingCompleted - result.ProcessingStarted).TotalMilliseconds;

                _logger.LogInformation("Healthcare AI pipeline completed successfully for patient: {PatientId}. " +
                    "Processing time: {ProcessingTime}ms, Entities: {EntityCount}, Actions: {ActionCount}", 
                    patientId, result.ProcessingTimeMs, result.ExtractedEntities.Count, result.ActionItems.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in healthcare AI pipeline for patient: {PatientId}", patientId);
                
                result.Status = "Failed";
                result.ErrorMessage = ex.Message;
                result.ProcessingCompleted = DateTime.UtcNow;
                result.ProcessingTimeMs = (int)(result.ProcessingCompleted - result.ProcessingStarted).TotalMilliseconds;

                return result;
            }
        }

        /// <summary>
        /// Processes radiology reports with specialized analysis.
        /// </summary>
        public async Task<RadiologyProcessingResult> ProcessRadiologyReportAsync(
            string patientId,
            string radiologyReport,
            PatientInfo patientInfo,
            CancellationToken cancellationToken = default)
        {
            var result = new RadiologyProcessingResult
            {
                PatientId = patientId,
                ProcessingStarted = DateTime.UtcNow,
                Status = "Processing"
            };

            try
            {
                _logger.LogInformation("Starting radiology report processing for patient: {PatientId}", patientId);

                // Enhanced NLP analysis for radiology
                var nlpResult = await _nlpService.AnalyzeHealthcareTextAsync(radiologyReport, "pt", cancellationToken);
                result.NlpAnalysis = nlpResult;

                // Specialized radiology insights
                var radiologyInsights = await _healthInsightsService.AnalyzeRadiologyInsightsAsync(
                    patientId, radiologyReport, patientInfo, cancellationToken);
                result.RadiologyInsights = radiologyInsights;

                // Create specialized FHIR resources for radiology
                await CreateRadiologyFhirResourcesAsync(result, patientId, radiologyReport, cancellationToken);

                result.ProcessingCompleted = DateTime.UtcNow;
                result.Status = "Completed";
                result.ProcessingTimeMs = (int)(result.ProcessingCompleted - result.ProcessingStarted).TotalMilliseconds;

                _logger.LogInformation("Radiology processing completed for patient: {PatientId}. " +
                    "Critical findings: {CriticalCount}, Recommendations: {RecommendationCount}", 
                    patientId, radiologyInsights.CriticalFindings?.Count ?? 0, 
                    radiologyInsights.FollowupRecommendations?.Count ?? 0);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing radiology report for patient: {PatientId}", patientId);
                
                result.Status = "Failed";
                result.ErrorMessage = ex.Message;
                result.ProcessingCompleted = DateTime.UtcNow;
                
                return result;
            }
        }

        /// <summary>
        /// Finds relevant clinical trials for a patient.
        /// </summary>
        public async Task<ClinicalTrialSearchResult> FindClinicalTrialsAsync(
            string patientId,
            PatientClinicalData clinicalData,
            CancellationToken cancellationToken = default)
        {
            var result = new ClinicalTrialSearchResult
            {
                PatientId = patientId,
                ProcessingStarted = DateTime.UtcNow,
                Status = "Processing"
            };

            try
            {
                _logger.LogInformation("Finding clinical trials for patient: {PatientId}", patientId);

                var trialMatcherResult = await _healthInsightsService.FindClinicalTrialsAsync(
                    patientId, clinicalData, cancellationToken);
                
                result.TrialMatcherResult = trialMatcherResult;
                result.MatchingTrials = trialMatcherResult.MatchingTrials ?? new List<ClinicalTrialMatch>();

                // Enrich trial information with medical knowledge
                foreach (var trial in result.MatchingTrials.Take(5)) // Limit enrichment to top 5 trials
                {
                    var knowledgeResult = await _nlpService.SearchMedicalKnowledgeAsync(
                        $"{trial.Title} {clinicalData.PrimaryDiagnosis}", 3, cancellationToken);
                    
                    if (knowledgeResult.Status == "Succeeded" && knowledgeResult.Documents.Any())
                    {
                        trial.EligibilityReason += $" | Additional Info: {string.Join("; ", 
                            knowledgeResult.Documents.Take(2).Select(d => d.Title))}";
                    }
                }

                result.ProcessingCompleted = DateTime.UtcNow;
                result.Status = "Completed";
                result.ProcessingTimeMs = (int)(result.ProcessingCompleted - result.ProcessingStarted).TotalMilliseconds;

                _logger.LogInformation("Clinical trial search completed for patient: {PatientId}. Found {TrialCount} matching trials", 
                    patientId, result.MatchingTrials.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding clinical trials for patient: {PatientId}", patientId);
                
                result.Status = "Failed";
                result.ErrorMessage = ex.Message;
                result.ProcessingCompleted = DateTime.UtcNow;
                
                return result;
            }
        }

        /// <summary>
        /// Enriches processing result with medical knowledge.
        /// </summary>
        private async Task EnrichWithMedicalKnowledgeAsync(HealthcareProcessingResult result, CancellationToken cancellationToken)
        {
            try
            {
                var keyTerms = result.ExtractedEntities
                    .Where(e => e.Category == "Condition" || e.Category == "Medication" || e.Category == "Treatment")
                    .GroupBy(e => e.Text.ToLowerInvariant())
                    .Select(g => g.First())
                    .Take(3) // Limit to avoid too many search calls
                    .ToList();

                result.MedicalKnowledgeEnrichment = new List<MedicalKnowledgeResult>();

                foreach (var term in keyTerms)
                {
                    var knowledgeResult = await _nlpService.SearchMedicalKnowledgeAsync(term.Text, 3, cancellationToken);
                    if (knowledgeResult.Status == "Succeeded")
                    {
                        result.MedicalKnowledgeEnrichment.Add(knowledgeResult);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error enriching with medical knowledge");
                // Non-critical error, continue processing
            }
        }

        /// <summary>
        /// Performs Health Insights analysis for complex cases.
        /// </summary>
        private async Task PerformHealthInsightsAnalysisAsync(
            HealthcareProcessingResult result, 
            string patientId, 
            string transcriptionText, 
            PatientInfo patientInfo,
            CancellationToken cancellationToken)
        {
            try
            {
                if (ContainsRadiologyContent(transcriptionText))
                {
                    var radiologyInsights = await _healthInsightsService.AnalyzeRadiologyInsightsAsync(
                        patientId, transcriptionText, patientInfo, cancellationToken);
                    result.RadiologyInsights = radiologyInsights;
                }

                if (ContainsClinicalTrialCandidate(result.NlpAnalysis.Entities))
                {
                    var clinicalData = ExtractClinicalDataFromEntities(result.NlpAnalysis.Entities, patientInfo, transcriptionText);
                    var trialMatcherResult = await _healthInsightsService.FindClinicalTrialsAsync(
                        patientId, clinicalData, cancellationToken);
                    result.TrialMatcherResult = trialMatcherResult;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error in Health Insights analysis");
                // Non-critical error, continue processing
            }
        }

        /// <summary>
        /// Creates FHIR resources from processing results.
        /// </summary>
        private async Task CreateFhirResourcesAsync(
            HealthcareProcessingResult result, 
            string patientId, 
            string transcriptionText,
            CancellationToken cancellationToken)
        {
            try
            {
                result.FhirOperations = new List<FhirOperationResult>();

                // Create clinical document
                var documentResult = await _fhirService.CreateClinicalDocumentAsync(
                    Guid.NewGuid().ToString(), patientId, 
                    $"Medical Consultation - {DateTime.UtcNow:yyyy-MM-dd HH:mm}", 
                    transcriptionText, "consultation", null, cancellationToken);
                
                result.FhirOperations.Add(new FhirOperationResult
                {
                    Status = documentResult != null ? "Success" : "Failed",
                    ResourceId = documentResult,
                    ResourceType = "DocumentReference",
                    OperationType = "Create",
                    ProcessedAt = DateTime.UtcNow,
                    Message = documentResult != null ? "Clinical document created successfully" : "Failed to create clinical document"
                });

                // Create observations from entities
                if (result.NlpAnalysis?.Entities != null)
                {
                    var observationResults = await _fhirService.CreateObservationsFromEntitiesAsync(
                        patientId, result.NlpAnalysis.Entities, null, cancellationToken);
                    
                    // Convert string results to FhirOperationResult
                    var fhirResults = observationResults.Select(resourceId => new FhirOperationResult
                    {
                        Status = "Success",
                        ResourceId = resourceId,
                        ResourceType = "Observation",
                        OperationType = "Create",
                        ProcessedAt = DateTime.UtcNow,
                        Message = "Observation created from healthcare entity"
                    });
                    
                    result.FhirOperations.AddRange(fhirResults);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error creating FHIR resources");
                // Non-critical error, continue processing
            }
        }

        /// <summary>
        /// Creates specialized FHIR resources for radiology reports.
        /// </summary>
        private async Task CreateRadiologyFhirResourcesAsync(
            RadiologyProcessingResult result, 
            string patientId, 
            string radiologyReport,
            CancellationToken cancellationToken)
        {
            try
            {
                result.FhirOperations = new List<FhirOperationResult>();

                // Create radiology document
                var documentResult = await _fhirService.CreateClinicalDocumentAsync(
                    Guid.NewGuid().ToString(), patientId, 
                    $"Radiology Report - {DateTime.UtcNow:yyyy-MM-dd HH:mm}", 
                    radiologyReport, "radiology report", null, cancellationToken);
                
                result.FhirOperations.Add(new FhirOperationResult
                {
                    Status = documentResult != null ? "Success" : "Failed",
                    ResourceId = documentResult,
                    ResourceType = "DocumentReference",
                    OperationType = "Create",
                    ProcessedAt = DateTime.UtcNow,
                    Message = documentResult != null ? "Radiology document created successfully" : "Failed to create radiology document"
                });

                // Create observations for critical findings
                if (result.RadiologyInsights?.CriticalFindings != null)
                {
                    foreach (var finding in result.RadiologyInsights.CriticalFindings)
                    {
                        var entity = new HealthcareEntityExtended
                        {
                            Text = finding.Finding,
                            Category = "CriticalFinding",
                            ConfidenceScore = (decimal)finding.Confidence,
                            ExtractedAt = DateTime.UtcNow
                        };

                        var observationResults = await _fhirService.CreateObservationsFromEntitiesAsync(
                            patientId, new List<HealthcareEntityExtended> { entity }, null, cancellationToken);
                        
                        // Convert string results to FhirOperationResult
                        var fhirResults = observationResults.Select(resourceId => new FhirOperationResult
                        {
                            Status = "Success",
                            ResourceId = resourceId,
                            ResourceType = "Observation",
                            OperationType = "Create",
                            ProcessedAt = DateTime.UtcNow,
                            Message = "Critical finding observation created"
                        });
                        
                        result.FhirOperations.AddRange(fhirResults);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error creating radiology FHIR resources");
            }
        }

        /// <summary>
        /// Performs quality assurance checks on processing results.
        /// </summary>
        private async Task PerformQualityAssuranceAsync(HealthcareProcessingResult result, CancellationToken cancellationToken)
        {
            try
            {
                result.QualityMetrics = new QualityMetrics
                {
                    EntityExtractionScore = CalculateEntityQualityScore(result.ExtractedEntities),
                    SentimentConfidence = result.SentimentAnalysis?.ConfidenceScore ?? 0.0,
                    IntentionConfidence = result.IntentionClassification?.TopIntent?.Confidence ?? 0.0,
                    OverallQualityScore = 0.0,
                    ValidationErrors = new List<string>()
                };

                // Calculate overall quality score
                result.QualityMetrics.OverallQualityScore = (
                    result.QualityMetrics.EntityExtractionScore * 0.4 +
                    result.QualityMetrics.SentimentConfidence * 0.3 +
                    result.QualityMetrics.IntentionConfidence * 0.3
                );

                // Validate critical information
                if (result.ExtractedEntities.Count == 0)
                {
                    result.QualityMetrics.ValidationErrors.Add("No medical entities extracted");
                }

                if (string.IsNullOrWhiteSpace(result.ClinicalSummary))
                {
                    result.QualityMetrics.ValidationErrors.Add("Clinical summary generation failed");
                }

                if (result.QualityMetrics.OverallQualityScore < 0.7)
                {
                    result.QualityMetrics.ValidationErrors.Add("Overall quality score below threshold");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error in quality assurance");
            }
        }

        /// <summary>
        /// Checks if text contains radiology content.
        /// </summary>
        private bool ContainsRadiologyContent(string text)
        {
            var radiologyKeywords = new[] { "raio-x", "tomografia", "ressonância", "ultrassom", "mamografia", 
                "radiografia", "contraste", "imagem", "exame de imagem" };
            
            return radiologyKeywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Checks if entities suggest clinical trial candidacy.
        /// </summary>
        private bool ContainsClinicalTrialCandidate(List<HealthcareEntityExtended> entities)
        {
            var trialIndicators = new[] { "cancer", "neoplasia", "tumor", "oncologia", "quimioterapia", 
                "experimental", "protocolo", "ensaio clínico" };
            
            return entities.Any(e => trialIndicators.Any(indicator => 
                e.Text.Contains(indicator, StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>
        /// Extracts clinical data from NLP entities.
        /// </summary>
        private PatientClinicalData ExtractClinicalDataFromEntities(
            List<HealthcareEntityExtended> entities, 
            PatientInfo patientInfo, 
            string clinicalNotes)
        {
            return new PatientClinicalData
            {
                PatientInfo = patientInfo,
                ClinicalNotes = clinicalNotes,
                PrimaryDiagnosis = entities.FirstOrDefault(e => e.Category == "Condition")?.Text ?? "Unknown",
                Medications = entities.Where(e => e.Category == "Medication").Select(e => e.Text).ToList(),
                Allergies = entities.Where(e => e.Category == "Allergy").Select(e => e.Text).ToList()
            };
        }

        /// <summary>
        /// Converts enhanced sentiment to basic sentiment analysis.
        /// </summary>
        private SentimentAnalysis ConvertToBasicSentiment(HealthcareSentimentResult sentimentResult)
        {
            return new SentimentAnalysis
            {
                OverallSentiment = sentimentResult.OverallSentiment,
                ConfidenceScore = Math.Max(sentimentResult.PositiveScore, 
                    Math.Max(sentimentResult.NegativeScore, sentimentResult.NeutralScore)),
                PositiveScore = sentimentResult.PositiveScore,
                NegativeScore = sentimentResult.NegativeScore,
                NeutralScore = sentimentResult.NeutralScore
            };
        }

        /// <summary>
        /// Calculates entity extraction quality score.
        /// </summary>
        private double CalculateEntityQualityScore(List<HealthcareEntity> entities)
        {
            if (!entities.Any()) return 0.0;

            var avgConfidence = entities.Average(e => (double)e.ConfidenceScore);
            var varietyScore = Math.Min(entities.GroupBy(e => e.Category).Count() / 5.0, 1.0); // Max 5 categories
            
            return (avgConfidence * 0.7) + (varietyScore * 0.3);
        }
    }

    #region Result Models

    /// <summary>
    /// Complete healthcare processing result.
    /// </summary>
    public class HealthcareProcessingResult
    {
        public string PatientId { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public DateTime ProcessingStarted { get; set; }
        public DateTime ProcessingCompleted { get; set; }
        public int ProcessingTimeMs { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }

        // Analysis Results
        public HealthcareAnalysisResult? NlpAnalysis { get; set; }
        public List<HealthcareEntity> ExtractedEntities { get; set; } = new();
        public SentimentAnalysis? SentimentAnalysis { get; set; }
        public IntentionClassification? IntentionClassification { get; set; }
        public string? ClinicalSummary { get; set; }
        public List<ActionItem> ActionItems { get; set; } = new();

        // Advanced Insights
        public RadiologyInsightsResult? RadiologyInsights { get; set; }
        public TrialMatcherResult? TrialMatcherResult { get; set; }
        public List<MedicalKnowledgeResult> MedicalKnowledgeEnrichment { get; set; } = new();

        // FHIR Integration
        public List<FhirOperationResult> FhirOperations { get; set; } = new();

        // Quality Metrics
        public QualityMetrics? QualityMetrics { get; set; }
    }

    /// <summary>
    /// Radiology-specific processing result.
    /// </summary>
    public class RadiologyProcessingResult
    {
        public string PatientId { get; set; } = string.Empty;
        public DateTime ProcessingStarted { get; set; }
        public DateTime ProcessingCompleted { get; set; }
        public int ProcessingTimeMs { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }

        public HealthcareAnalysisResult? NlpAnalysis { get; set; }
        public RadiologyInsightsResult? RadiologyInsights { get; set; }
        public List<FhirOperationResult> FhirOperations { get; set; } = new();
    }

    /// <summary>
    /// Clinical trial search result.
    /// </summary>
    public class ClinicalTrialSearchResult
    {
        public string PatientId { get; set; } = string.Empty;
        public DateTime ProcessingStarted { get; set; }
        public DateTime ProcessingCompleted { get; set; }
        public int ProcessingTimeMs { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }

        public TrialMatcherResult? TrialMatcherResult { get; set; }
        public List<ClinicalTrialMatch> MatchingTrials { get; set; } = new();
    }

    /// <summary>
    /// Quality metrics for processing validation.
    /// </summary>
    public class QualityMetrics
    {
        public double EntityExtractionScore { get; set; }
        public double SentimentConfidence { get; set; }
        public double IntentionConfidence { get; set; }
        public double OverallQualityScore { get; set; }
        public List<string> ValidationErrors { get; set; } = new();
    }

    #endregion
}
