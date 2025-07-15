using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace MedicalScribeR.Web.Services
{
    /// <summary>
    /// Serviço para integração com Azure Machine Learning
    /// </summary>
    public class AzureMLService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AzureMLService> _logger;
        private readonly string _subscriptionId;
        private readonly string _resourceGroup;
        private readonly string _workspaceName;
        private readonly string _region;

        public AzureMLService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<AzureMLService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _subscriptionId = _configuration["AZURE_SUBSCRIPTION_ID"] ?? throw new InvalidOperationException("AZURE_SUBSCRIPTION_ID não configurado");
            _resourceGroup = _configuration["AZURE_ML_RESOURCE_GROUP"] ?? "rg-medicalscribe";
            _workspaceName = _configuration["AZURE_ML_WORKSPACE"] ?? "azuremlworkspace";
            _region = _configuration["AZURE_ML_REGION"] ?? "eastus2";

            ConfigureHttpClient();
        }

        private void ConfigureHttpClient()
        {
            _httpClient.BaseAddress = new Uri($"https://{_region}.api.azureml.ms/");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "MedicalScribeR/1.0");
        }

        /// <summary>
        /// Obtém token de acesso para Azure ML usando Azure Identity
        /// </summary>
        private async Task<string> GetAccessTokenAsync()
        {
            try
            {
                var credential = new DefaultAzureCredential();
                var tokenRequestContext = new TokenRequestContext(new[] { "https://ml.azure.com/.default" });
                var token = await credential.GetTokenAsync(tokenRequestContext);
                
                return token.Token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter token de acesso do Azure ML");
                throw;
            }
        }

        /// <summary>
        /// Analisa sentimento médico usando modelos do Azure ML
        /// </summary>
        public async Task<MedicalSentimentResult> AnalyzeMedicalSentiment(string medicalText)
        {
            try
            {
                var token = await GetAccessTokenAsync();
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var requestBody = new
                {
                    text = medicalText,
                    language = "pt-BR",
                    domain = "medical"
                };

                var jsonContent = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json"
                );

                var endpoint = $"mlflow/v2.0/subscriptions/{_subscriptionId}/resourceGroups/{_resourceGroup}/providers/Microsoft.MachineLearningServices/workspaces/{_workspaceName}/sentiment/analyze";
                
                var response = await _httpClient.PostAsync(endpoint, jsonContent);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<MedicalSentimentResult>(responseContent);
                    
                    _logger.LogInformation("Análise de sentimento médico concluída para texto de {Length} caracteres", medicalText.Length);
                    return result ?? new MedicalSentimentResult { Sentiment = "neutral", Confidence = 0.5 };
                }
                else
                {
                    _logger.LogWarning("Erro na análise de sentimento: {StatusCode} - {Reason}", response.StatusCode, response.ReasonPhrase);
                    return new MedicalSentimentResult { Sentiment = "neutral", Confidence = 0.5 };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao analisar sentimento médico");
                return new MedicalSentimentResult { Sentiment = "neutral", Confidence = 0.5, Error = ex.Message };
            }
        }

        /// <summary>
        /// Extrai entidades médicas usando modelos especializados do Azure ML
        /// </summary>
        public async Task<MedicalEntityExtractionResult> ExtractMedicalEntities(string medicalText)
        {
            try
            {
                var token = await GetAccessTokenAsync();
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var requestBody = new
                {
                    text = medicalText,
                    language = "pt-BR",
                    entityTypes = new[] { "MEDICATION", "DIAGNOSIS", "SYMPTOM", "PROCEDURE", "ANATOMY", "DOSAGE" }
                };

                var jsonContent = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json"
                );

                var endpoint = $"mlflow/v2.0/subscriptions/{_subscriptionId}/resourceGroups/{_resourceGroup}/providers/Microsoft.MachineLearningServices/workspaces/{_workspaceName}/entities/extract";
                
                var response = await _httpClient.PostAsync(endpoint, jsonContent);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<MedicalEntityExtractionResult>(responseContent);
                    
                    _logger.LogInformation("Extração de entidades médicas concluída: {EntityCount} entidades encontradas", result?.Entities?.Count ?? 0);
                    return result ?? new MedicalEntityExtractionResult { Entities = new List<MedicalEntity>() };
                }
                else
                {
                    _logger.LogWarning("Erro na extração de entidades: {StatusCode} - {Reason}", response.StatusCode, response.ReasonPhrase);
                    return new MedicalEntityExtractionResult { Entities = new List<MedicalEntity>() };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao extrair entidades médicas");
                return new MedicalEntityExtractionResult { Entities = new List<MedicalEntity>(), Error = ex.Message };
            }
        }

        /// <summary>
        /// Classifica a urgência de casos médicos usando modelos do Azure ML
        /// </summary>
        public async Task<MedicalUrgencyResult> ClassifyMedicalUrgency(string symptoms, string patientHistory)
        {
            try
            {
                var token = await GetAccessTokenAsync();
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var requestBody = new
                {
                    symptoms = symptoms,
                    patientHistory = patientHistory,
                    language = "pt-BR"
                };

                var jsonContent = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json"
                );

                var endpoint = $"mlflow/v2.0/subscriptions/{_subscriptionId}/resourceGroups/{_resourceGroup}/providers/Microsoft.MachineLearningServices/workspaces/{_workspaceName}/urgency/classify";
                
                var response = await _httpClient.PostAsync(endpoint, jsonContent);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<MedicalUrgencyResult>(responseContent);
                    
                    _logger.LogInformation("Classificação de urgência médica concluída: {UrgencyLevel}", result?.UrgencyLevel ?? "unknown");
                    return result ?? new MedicalUrgencyResult { UrgencyLevel = "medium", Confidence = 0.5 };
                }
                else
                {
                    _logger.LogWarning("Erro na classificação de urgência: {StatusCode} - {Reason}", response.StatusCode, response.ReasonPhrase);
                    return new MedicalUrgencyResult { UrgencyLevel = "medium", Confidence = 0.5 };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao classificar urgência médica");
                return new MedicalUrgencyResult { UrgencyLevel = "medium", Confidence = 0.5, Error = ex.Message };
            }
        }

        /// <summary>
        /// Gera sugestões de diagnóstico usando modelos do Azure ML
        /// </summary>
        public async Task<DiagnosticSuggestionsResult> GenerateDiagnosticSuggestions(string symptoms, string findings, string patientData)
        {
            try
            {
                var token = await GetAccessTokenAsync();
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var requestBody = new
                {
                    symptoms = symptoms,
                    findings = findings,
                    patientData = patientData,
                    language = "pt-BR",
                    maxSuggestions = 5
                };

                var jsonContent = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json"
                );

                var endpoint = $"mlflow/v2.0/subscriptions/{_subscriptionId}/resourceGroups/{_resourceGroup}/providers/Microsoft.MachineLearningServices/workspaces/{_workspaceName}/diagnostics/suggest";
                
                var response = await _httpClient.PostAsync(endpoint, jsonContent);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<DiagnosticSuggestionsResult>(responseContent);
                    
                    _logger.LogInformation("Sugestões diagnósticas geradas: {SuggestionsCount} sugestões", result?.Suggestions?.Count ?? 0);
                    return result ?? new DiagnosticSuggestionsResult { Suggestions = new List<DiagnosticSuggestion>() };
                }
                else
                {
                    _logger.LogWarning("Erro na geração de sugestões diagnósticas: {StatusCode} - {Reason}", response.StatusCode, response.ReasonPhrase);
                    return new DiagnosticSuggestionsResult { Suggestions = new List<DiagnosticSuggestion>() };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar sugestões diagnósticas");
                return new DiagnosticSuggestionsResult { Suggestions = new List<DiagnosticSuggestion>(), Error = ex.Message };
            }
        }
    }

    // Modelos para Azure ML
    public class MedicalSentimentResult
    {
        public string Sentiment { get; set; } = "";
        public double Confidence { get; set; }
        public string? Error { get; set; }
    }

    public class MedicalEntityExtractionResult
    {
        public List<MedicalEntity> Entities { get; set; } = new();
        public string? Error { get; set; }
    }

    public class MedicalEntity
    {
        public string Text { get; set; } = "";
        public string Type { get; set; } = "";
        public double Confidence { get; set; }
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
    }

    public class MedicalUrgencyResult
    {
        public string UrgencyLevel { get; set; } = ""; // low, medium, high, critical
        public double Confidence { get; set; }
        public string? Reasoning { get; set; }
        public string? Error { get; set; }
    }

    public class DiagnosticSuggestionsResult
    {
        public List<DiagnosticSuggestion> Suggestions { get; set; } = new();
        public string? Error { get; set; }
    }

    public class DiagnosticSuggestion
    {
        public string Diagnosis { get; set; } = "";
        public double Confidence { get; set; }
        public string Description { get; set; } = "";
        public List<string> SupportingEvidence { get; set; } = new();
        public string RecommendedActions { get; set; } = "";
    }
}
