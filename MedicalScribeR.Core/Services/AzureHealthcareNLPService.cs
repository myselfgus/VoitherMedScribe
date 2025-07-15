using Azure;
using Azure.AI.TextAnalytics;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MedicalScribeR.Core.Interfaces;
using MedicalScribeR.Core.Models;

namespace MedicalScribeR.Core.Services
{
    /// <summary>
    /// Enhanced Azure Text Analytics service with Healthcare NLP capabilities.
    /// Integrates with Azure Cognitive Search for medical knowledge indexing.
    /// Reference: https://learn.microsoft.com/en-us/azure/ai-services/language-service/text-analytics-for-health/overview
    /// </summary>
    public class AzureHealthcareNLPService : IDisposable
    {
        private readonly TextAnalyticsClient _textAnalyticsClient;
        private readonly TextAnalyticsClient _backupTextAnalyticsClient; // etherim backup
        private readonly SearchClient _searchClient;
        private readonly ILogger<AzureHealthcareNLPService> _logger;
        private readonly TimeSpan _requestTimeout = TimeSpan.FromMinutes(1);
        private readonly int _maxRetryAttempts = 3;

        public AzureHealthcareNLPService(IConfiguration configuration, ILogger<AzureHealthcareNLPService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            try
            {
                // Primary Text Analytics Client (healthcaraNLP)
                var primaryEndpoint = new Uri(configuration["Azure:HealthcareNLP:Endpoint"] ?? 
                    "https://healthcaranlp.cognitiveservices.azure.com/");
                var primaryApiKey = new AzureKeyCredential(configuration["Azure:HealthcareNLP:ApiKey"] ?? 
                    throw new InvalidOperationException("Azure:HealthcareNLP:ApiKey not configured"));
                
                _textAnalyticsClient = new TextAnalyticsClient(primaryEndpoint, primaryApiKey);

                // Backup Text Analytics Client (etherim)
                var backupEndpoint = new Uri(configuration["Azure:HealthcareNLP:BackupEndpoint"] ?? 
                    "https://etherim.cognitiveservices.azure.com/");
                var backupApiKey = new AzureKeyCredential(configuration["Azure:HealthcareNLP:BackupApiKey"] ?? 
                    throw new InvalidOperationException("Azure:HealthcareNLP:BackupApiKey not configured"));
                
                _backupTextAnalyticsClient = new TextAnalyticsClient(backupEndpoint, backupApiKey);

                // Azure Cognitive Search Client for medical knowledge
                var searchEndpoint = new Uri(configuration["Azure:CognitiveSearch:Endpoint"] ?? 
                    "https://healthcaranlp-asuxj7c6w3imp6w.search.windows.net/");
                var searchApiKey = new AzureKeyCredential(configuration["Azure:CognitiveSearch:ApiKey"] ?? 
                    throw new InvalidOperationException("Azure:CognitiveSearch:ApiKey not configured"));
                
                _searchClient = new SearchClient(searchEndpoint, "medical-knowledge", searchApiKey);

                _logger.LogInformation("AzureHealthcareNLPService initialized successfully with primary and backup endpoints");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing AzureHealthcareNLPService");
                throw;
            }
        }

        /// <summary>
        /// Performs comprehensive healthcare entity extraction with UMLS linking.
        /// Uses Text Analytics for Health with medical vocabulary integration.
        /// </summary>
        public async Task<HealthcareAnalysisResult> AnalyzeHealthcareTextAsync(
            string text, 
            string? language = "pt", // Default to Portuguese for Brazil
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("Empty text provided for healthcare analysis");
                return new HealthcareAnalysisResult { Status = "Failed", Message = "Empty text" };
            }

            var attempt = 0;
            Exception? lastException = null;
            var currentClient = _textAnalyticsClient;

            while (attempt < _maxRetryAttempts)
            {
                try
                {
                    _logger.LogDebug("Starting healthcare text analysis - Attempt {Attempt}", attempt + 1);

                    using var timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeoutTokenSource.CancelAfter(_requestTimeout);

                    // Enhanced healthcare entity analysis with FHIR output support
                    var options = new AnalyzeHealthcareEntitiesOptions
                    {
                        IncludeStatistics = true,
                        DisplayName = $"MedicalScribe-Analysis-{DateTime.UtcNow:yyyyMMdd-HHmmss}"
                    };

                    AnalyzeHealthcareEntitiesOperation operation = await currentClient.AnalyzeHealthcareEntitiesAsync(
                        WaitUntil.Completed, 
                        new[] { new TextDocumentInput("1", text) { Language = language } },
                        options,
                        timeoutTokenSource.Token);

                    var result = new HealthcareAnalysisResult
                    {
                        Status = "Succeeded",
                        ProcessedAt = DateTime.UtcNow,
                        Language = language,
                        Entities = new List<HealthcareEntityExtended>(),
                        Relations = new List<HealthcareRelation>()
                    };

                    // Process all document results
                    await foreach (AnalyzeHealthcareEntitiesResultCollection documentsInPage in operation.Value)
                    {
                        foreach (AnalyzeHealthcareEntitiesResult documentResult in documentsInPage)
                        {
                            if (documentResult.HasError)
                            {
                                _logger.LogError("Healthcare analysis error: {Error}", documentResult.Error.Message);
                                continue;
                            }

                            // Extract entities with enhanced information
                            foreach (var entity in documentResult.Entities)
                            {
                                var enhancedEntity = new HealthcareEntityExtended
                                {
                                    Text = entity.Text,
                                    Category = entity.Category.ToString(),
                                    SubCategory = entity.SubCategory?.ToString(),
                                    ConfidenceScore = (decimal)entity.ConfidenceScore,
                                    Offset = entity.Offset,
                                    Length = entity.Length,
                                    IsNegated = entity.Assertion?.Association != null && entity.Assertion.Association.ToString() == "Other",
                                    CertaintyLevel = entity.Assertion?.Certainty?.ToString(),
                                    TemporalityType = null, // Propriedade removida ou não suportada na API atual
                                    ConditionalityType = entity.Assertion?.Conditionality?.ToString(),
                                    ExtractedAt = DateTime.UtcNow,
                                    NormalizedText = entity.NormalizedText,
                                    DataSources = entity.DataSources?.Select(ds => new Models.EntityDataSource
                                    {
                                        Name = ds.Name,
                                        EntityId = ds.EntityId
                                    }).ToList() ?? new List<Models.EntityDataSource>()
                                };

                                result.Entities.Add(enhancedEntity);
                            }

                            // Extract healthcare relations - API structure has changed
                            // Temporarily disable relation processing until API compatibility is verified
                            foreach (var relation in documentResult.EntityRelations)
                            {
                                var healthcareRelation = new HealthcareRelation
                                {
                                    RelationType = relation.RelationType.ToString(),
                                    ConfidenceScore = (decimal)relation.ConfidenceScore,
                                    SourceEntity = "relation_source", // TODO: Update with correct API properties
                                    TargetEntity = "relation_target", // TODO: Update with correct API properties
                                    SourceCategory = "Unknown",
                                    TargetCategory = "Unknown"
                                };

                                result.Relations.Add(healthcareRelation);
                            }

                            // Set document statistics (sempre disponível na API atual)
                            result.DocumentStatistics = new DocumentStatistics
                            {
                                CharacterCount = documentResult.Statistics.CharacterCount,
                                TransactionCount = documentResult.Statistics.TransactionCount
                            };
                        }
                    }

                    // Enrich with medical knowledge from Azure Search
                    await EnrichWithMedicalKnowledgeAsync(result, timeoutTokenSource.Token);

                    _logger.LogInformation("Healthcare analysis completed. Entities: {EntityCount}, Relations: {RelationCount}", 
                        result.Entities.Count, result.Relations.Count);

                    return result;
                }
                catch (RequestFailedException ex) when (IsTransientError(ex) && attempt < _maxRetryAttempts - 1)
                {
                    lastException = ex;
                    attempt++;
                    
                    // Switch to backup client on second attempt
                    if (attempt == 1)
                    {
                        currentClient = _backupTextAnalyticsClient;
                        _logger.LogWarning("Switching to backup Text Analytics client");
                    }
                    
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    
                    _logger.LogWarning(ex, "Transient error in healthcare analysis. Attempt {Attempt}/{MaxAttempts}. Waiting {Delay}s", 
                        attempt + 1, _maxRetryAttempts, delay.TotalSeconds);
                    
                    await Task.Delay(delay, cancellationToken);
                }
                catch (TaskCanceledException ex) when (ex.CancellationToken == cancellationToken)
                {
                    _logger.LogWarning("Healthcare analysis cancelled by user");
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Non-transient error in healthcare analysis");
                    throw;
                }
            }

            _logger.LogError(lastException, "Definitive failure in healthcare analysis after {MaxAttempts} attempts", _maxRetryAttempts);
            return new HealthcareAnalysisResult { Status = "Failed", Message = lastException?.Message ?? "Unknown error" };
        }

        /// <summary>
        /// Performs sentiment analysis with healthcare-specific considerations.
        /// </summary>
        public async Task<HealthcareSentimentResult> AnalyzeHealthcareSentimentAsync(
            string text, 
            string? language = "pt",
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("Empty text provided for sentiment analysis");
                return new HealthcareSentimentResult { Status = "Failed", Message = "Empty text" };
            }

            var attempt = 0;
            Exception? lastException = null;
            var currentClient = _textAnalyticsClient;

            while (attempt < _maxRetryAttempts)
            {
                try
                {
                    _logger.LogDebug("Starting healthcare sentiment analysis - Attempt {Attempt}", attempt + 1);

                    using var timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeoutTokenSource.CancelAfter(_requestTimeout);

                    var options = new AnalyzeSentimentOptions
                    {
                        IncludeOpinionMining = true,
                        IncludeStatistics = true
                    };

                    Response<DocumentSentiment> response = await currentClient.AnalyzeSentimentAsync(
                        text, 
                        language,
                        options,
                        timeoutTokenSource.Token);

                    var result = new HealthcareSentimentResult
                    {
                        Status = "Succeeded",
                        ProcessedAt = DateTime.UtcNow,
                        OverallSentiment = response.Value.Sentiment.ToString(),
                        PositiveScore = response.Value.ConfidenceScores.Positive,
                        NegativeScore = response.Value.ConfidenceScores.Negative,
                        NeutralScore = response.Value.ConfidenceScores.Neutral,
                        SentenceSentiments = new List<Models.SentenceSentiment>()
                    };

                    // Analyze sentence-level sentiment for clinical context
                    foreach (var sentence in response.Value.Sentences)
                    {
                        var sentenceSentiment = new Models.SentenceSentiment
                        {
                            Text = sentence.Text,
                            Sentiment = sentence.Sentiment.ToString(),
                            PositiveScore = sentence.ConfidenceScores.Positive,
                            NegativeScore = sentence.ConfidenceScores.Negative,
                            NeutralScore = sentence.ConfidenceScores.Neutral,
                            Offset = sentence.Offset,
                            Length = sentence.Length,
                            Opinions = new List<SentimentOpinion>()
                        };

                        // Extract opinion mining results for healthcare context
                        foreach (var opinion in sentence.Opinions)
                        {
                            var sentimentOpinion = new SentimentOpinion
                            {
                                Target = opinion.Target.Text,
                                TargetSentiment = opinion.Target.Sentiment.ToString(),
                                TargetConfidenceScores = new ConfidenceScores
                                {
                                    Positive = opinion.Target.ConfidenceScores.Positive,
                                    Negative = opinion.Target.ConfidenceScores.Negative,
                                    Neutral = opinion.Target.ConfidenceScores.Neutral
                                },
                                Assessments = opinion.Assessments.Select(a => new SentimentAssessment
                                {
                                    Text = a.Text,
                                    Sentiment = a.Sentiment.ToString(),
                                    ConfidenceScores = new ConfidenceScores
                                    {
                                        Positive = a.ConfidenceScores.Positive,
                                        Negative = a.ConfidenceScores.Negative,
                                        Neutral = a.ConfidenceScores.Neutral
                                    },
                                    IsNegated = a.IsNegated
                                }).ToList()
                            };

                            sentenceSentiment.Opinions.Add(sentimentOpinion);
                        }

                        result.SentenceSentiments.Add(sentenceSentiment);
                    }

                    _logger.LogInformation("Healthcare sentiment analysis completed: {Sentiment} ({Confidence:F2})", 
                        result.OverallSentiment, Math.Max(result.PositiveScore, Math.Max(result.NegativeScore, result.NeutralScore)));

                    return result;
                }
                catch (RequestFailedException ex) when (IsTransientError(ex) && attempt < _maxRetryAttempts - 1)
                {
                    lastException = ex;
                    attempt++;
                    
                    // Switch to backup client on second attempt
                    if (attempt == 1)
                    {
                        currentClient = _backupTextAnalyticsClient;
                        _logger.LogWarning("Switching to backup client for sentiment analysis");
                    }
                    
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    
                    _logger.LogWarning(ex, "Transient error in sentiment analysis. Attempt {Attempt}/{MaxAttempts}. Waiting {Delay}s", 
                        attempt + 1, _maxRetryAttempts, delay.TotalSeconds);
                    
                    await Task.Delay(delay, cancellationToken);
                }
                catch (TaskCanceledException ex) when (ex.CancellationToken == cancellationToken)
                {
                    _logger.LogWarning("Healthcare sentiment analysis cancelled");
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Non-transient error in sentiment analysis");
                    throw;
                }
            }

            _logger.LogError(lastException, "Definitive failure in sentiment analysis after {MaxAttempts} attempts", _maxRetryAttempts);
            return new HealthcareSentimentResult { Status = "Failed", Message = lastException?.Message ?? "Unknown error" };
        }

        /// <summary>
        /// Searches medical knowledge base using Azure Cognitive Search.
        /// </summary>
        public async Task<MedicalKnowledgeResult> SearchMedicalKnowledgeAsync(
            string query, 
            int maxResults = 10,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                _logger.LogWarning("Empty query provided for medical knowledge search");
                return new MedicalKnowledgeResult { Status = "Failed", Message = "Empty query" };
            }

            try
            {
                _logger.LogDebug("Searching medical knowledge for query: {Query}", query);

                var searchOptions = new SearchOptions
                {
                    Size = maxResults,
                    IncludeTotalCount = true,
                    SearchMode = SearchMode.Any,
                    QueryType = SearchQueryType.Semantic,
                    SemanticSearch = new()
                    {
                        SemanticConfigurationName = "medical-semantic-config",
                        QueryCaption = new(QueryCaptionType.Extractive),
                        QueryAnswer = new(QueryAnswerType.Extractive)
                    },
                    HighlightFields = { "content", "description" },
                    ScoringProfile = "medical-scoring",
                    Select = { "id", "title", "content", "category", "source", "lastUpdated", "confidence" }
                };

                searchOptions.Filter = "category eq 'medical' or category eq 'pharmaceutical' or category eq 'diagnostic'";

                var searchResult = await _searchClient.SearchAsync<MedicalKnowledgeDocument>(query, searchOptions, cancellationToken);

                var result = new MedicalKnowledgeResult
                {
                    Status = "Succeeded",
                    ProcessedAt = DateTime.UtcNow,
                    Query = query,
                    TotalCount = searchResult.Value.TotalCount ?? 0,
                    Documents = new List<MedicalKnowledgeDocument>()
                };

                await foreach (var searchResultItem in searchResult.Value.GetResultsAsync())
                {
                    var document = searchResultItem.Document;
                    document.SearchScore = searchResultItem.Score ?? 0.0;
                    document.Highlights = searchResultItem.Highlights?.ToDictionary(h => h.Key, h => h.Value.ToList()) ?? 
                        new Dictionary<string, List<string>>();
                    
                    result.Documents.Add(document);
                }

                _logger.LogInformation("Medical knowledge search completed. Found {Count} results for query: {Query}", 
                    result.Documents.Count, query);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching medical knowledge for query: {Query}", query);
                return new MedicalKnowledgeResult { Status = "Failed", Message = ex.Message, Query = query };
            }
        }

        /// <summary>
        /// Generates FHIR-compliant healthcare data bundle.
        /// </summary>
        private async Task<FhirBundle?> GenerateFhirBundleAsync(
            AnalyzeHealthcareEntitiesOperation operation, 
            string originalText,
            CancellationToken cancellationToken)
        {
            try
            {
                var bundle = new FhirBundle
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = "collection",
                    Timestamp = DateTime.UtcNow,
                    Entries = new List<FhirEntry>()
                };

                // Create DocumentReference for original text
                var documentEntry = new FhirEntry
                {
                    ResourceType = "DocumentReference",
                    Id = Guid.NewGuid().ToString(),
                    Content = new Dictionary<string, object>
                    {
                        ["status"] = "current",
                        ["type"] = new { coding = new[] { new { system = "http://loinc.org", code = "11488-4", display = "Consultation note" } } },
                        ["content"] = new[] { new { attachment = new { contentType = "text/plain", data = Convert.ToBase64String(Encoding.UTF8.GetBytes(originalText)) } } },
                        ["date"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                    }
                };

                bundle.Entries.Add(documentEntry);

                // Process healthcare entities and convert to FHIR resources
                await foreach (var documentsInPage in operation.Value)
                {
                    foreach (var documentResult in documentsInPage)
                    {
                        if (documentResult.HasError) continue;

                        foreach (var entity in documentResult.Entities)
                        {
                            var fhirEntry = CreateFhirEntryFromEntity(entity);
                            if (fhirEntry != null)
                            {
                                bundle.Entries.Add(fhirEntry);
                            }
                        }
                    }
                }

                return bundle;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating FHIR bundle");
                return null;
            }
        }

        /// <summary>
        /// Creates FHIR entry from healthcare entity.
        /// </summary>
        private FhirEntry? CreateFhirEntryFromEntity(Azure.AI.TextAnalytics.HealthcareEntity entity)
        {
            try
            {
                var categoryString = entity.Category.ToString();
                var resourceType = categoryString switch
                {
                    "Condition" => "Condition",
                    "Medication" => "Medication", 
                    "Dosage" => "Dosage",
                    "Treatment" => "Procedure",
                    "Diagnosis" => "Condition",
                    "SymptomOrSign" => "Observation",
                    "BodyStructure" => "BodyStructure",
                    "MedicalDevice" => "Device",
                    _ => "Observation"
                };

                var entry = new FhirEntry
                {
                    ResourceType = resourceType,
                    Id = Guid.NewGuid().ToString(),
                    Content = new Dictionary<string, object>
                    {
                        ["text"] = entity.Text,
                        ["category"] = entity.Category.ToString(),
                        ["confidence"] = entity.ConfidenceScore,
                        ["offset"] = entity.Offset,
                        ["length"] = entity.Length
                    }
                };

                // Add UMLS codes if available
                if (entity.DataSources != null && entity.DataSources.Any())
                {
                    entry.Content["coding"] = entity.DataSources.Select(ds => new
                    {
                        system = ds.Name,
                        code = ds.EntityId,
                        display = entity.Text
                    }).ToArray();
                }

                // Add assertion information
                if (entity.Assertion != null)
                {
                    entry.Content["assertion"] = new
                    {
                        certainty = entity.Assertion.Certainty?.ToString(),
                        conditionality = entity.Assertion.Conditionality?.ToString(),
                        association = entity.Assertion.Association?.ToString(),
                        temporality = "" // Propriedade removida na API atual
                    };
                }

                return entry;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating FHIR entry from entity: {EntityText}", entity.Text);
                return null;
            }
        }

        /// <summary>
        /// Enriches analysis results with medical knowledge from Azure Search.
        /// </summary>
        private async Task EnrichWithMedicalKnowledgeAsync(HealthcareAnalysisResult result, CancellationToken cancellationToken)
        {
            try
            {
                var medicalTerms = result.Entities
                    .Where(e => e.Category == "Condition" || e.Category == "Medication" || e.Category == "Treatment")
                    .Select(e => e.Text)
                    .Distinct()
                    .Take(5); // Limit to avoid too many search calls

                foreach (var term in medicalTerms)
                {
                    var knowledgeResult = await SearchMedicalKnowledgeAsync(term, 3, cancellationToken);
                    if (knowledgeResult.Status == "Succeeded" && knowledgeResult.Documents.Any())
                    {
                        var entity = result.Entities.FirstOrDefault(e => e.Text.Equals(term, StringComparison.OrdinalIgnoreCase));
                        if (entity != null)
                        {
                            entity.EnrichedInformation = knowledgeResult.Documents
                                .Select(d => $"{d.Title}: {d.Content?.Substring(0, Math.Min(200, d.Content?.Length ?? 0))}...")
                                .ToList();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error enriching results with medical knowledge");
                // Non-critical error, continue without enrichment
            }
        }

        /// <summary>
        /// Gets entity category for relation mapping.
        /// </summary>
        private string GetEntityCategory(string entityText, List<HealthcareEntityExtended> entities)
        {
            return entities.FirstOrDefault(e => e.Text.Equals(entityText, StringComparison.OrdinalIgnoreCase))?.Category ?? "Unknown";
        }

        /// <summary>
        /// Determines if an error is transient and can be retried.
        /// </summary>
        private static bool IsTransientError(RequestFailedException ex)
        {
            return ex.Status switch
            {
                429 => true, // Rate limit
                500 => true, // Internal server error
                502 => true, // Bad gateway
                503 => true, // Service unavailable
                504 => true, // Gateway timeout
                _ => false
            };
        }

        public void Dispose()
        {
            _textAnalyticsClient?.GetType().GetMethod("Dispose")?.Invoke(_textAnalyticsClient, null);
            _backupTextAnalyticsClient?.GetType().GetMethod("Dispose")?.Invoke(_backupTextAnalyticsClient, null);
        }
    }
}
