using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace MedicalScribeR.Core.Services
{
    /// <summary>
    /// Serviço para integração com Azure Machine Learning
    /// </summary>
    public class AzureMLService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AzureMLService> _logger;

        public AzureMLService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<AzureMLService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Chama um modelo customizado do Azure ML para análise médica avançada
        /// </summary>
        public async Task<string> CallCustomMedicalModelAsync(string text, string modelName)
        {
            try
            {
                _logger.LogInformation("Chamando modelo Azure ML: {ModelName} para análise de texto", modelName);

                var workspaceName = _configuration["Azure:MachineLearning:WorkspaceName"];
                var subscriptionId = _configuration["Azure:MachineLearning:SubscriptionId"];
                var resourceGroup = _configuration["Azure:MachineLearning:ResourceGroup"];
                var apiKey = _configuration["Azure:MachineLearning:ApiKey"];
                var region = _configuration["Azure:MachineLearning:Region"];
                var endpointName = _configuration["Azure:MachineLearning:EndpointName"];

                if (string.IsNullOrEmpty(workspaceName) || string.IsNullOrEmpty(apiKey) || 
                    string.IsNullOrEmpty(region) || string.IsNullOrEmpty(endpointName))
                {
                    throw new InvalidOperationException("Azure ML configuration is incomplete");
                }

                // Construir URL do endpoint real do Azure ML
                var endpointUrl = $"https://{endpointName}.{region}.inference.ml.azure.com/score";
                
                // Criar payload JSON para o modelo
                var payload = new
                {
                    data = new[]
                    {
                        new { text = text, model = modelName }
                    }
                };

                var jsonPayload = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // Configurar headers de autenticação
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                _httpClient.DefaultRequestHeaders.Add("azureml-model-deployment", modelName);

                _logger.LogDebug("Enviando requisição para Azure ML endpoint: {Endpoint}", endpointUrl);

                // Fazer chamada real para Azure ML
                var response = await _httpClient.PostAsync(endpointUrl, content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Azure ML endpoint retornou erro {StatusCode}: {Error}", 
                        response.StatusCode, errorContent);
                    throw new HttpRequestException($"Azure ML API error: {response.StatusCode} - {errorContent}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Resposta do Azure ML recebida: {ResponseLength} caracteres", responseContent.Length);

                // Parse da resposta JSON do Azure ML
                using var jsonDoc = JsonDocument.Parse(responseContent);
                var result = jsonDoc.RootElement;

                // Extrair resultado baseado na estrutura esperada do modelo
                if (result.TryGetProperty("result", out var resultProperty))
                {
                    var analysisResult = resultProperty.GetString();
                    _logger.LogInformation("Análise Azure ML completada com sucesso para modelo {ModelName}", modelName);
                    return analysisResult ?? string.Empty;
                }
                else if (result.TryGetProperty("predictions", out var predictions))
                {
                    // Formato alternativo de resposta
                    var firstPrediction = predictions.EnumerateArray().FirstOrDefault();
                    if (firstPrediction.ValueKind != JsonValueKind.Undefined)
                    {
                        return firstPrediction.GetString() ?? string.Empty;
                    }
                }

                // Se a estrutura da resposta não for reconhecida, retornar o JSON completo
                _logger.LogWarning("Estrutura de resposta do Azure ML não reconhecida, retornando JSON completo");
                return responseContent;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Erro de rede ao chamar Azure ML modelo: {ModelName}", modelName);
                throw new InvalidOperationException($"Falha na comunicação com Azure ML: {ex.Message}", ex);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Erro ao processar resposta JSON do Azure ML modelo: {ModelName}", modelName);
                throw new InvalidOperationException($"Resposta inválida do Azure ML: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao chamar Azure ML modelo: {ModelName}", modelName);
                throw;
            }
        }

        /// <summary>
        /// Analisa sentimentos médicos usando modelo customizado do Azure ML
        /// </summary>
        public async Task<Dictionary<string, double>> AnalyzeMedicalSentimentAsync(string text)
        {
            try
            {
                _logger.LogInformation("Iniciando análise de sentimento médico via Azure ML");

                var sentimentModelName = _configuration["Azure:MachineLearning:SentimentModelName"] ?? "medical-sentiment-model";
                
                // Usar o método CallCustomMedicalModelAsync para análise de sentimento
                var analysisResult = await CallCustomMedicalModelAsync(text, sentimentModelName);
                
                // Parse do resultado JSON do Azure ML para extrair scores de sentimento
                try
                {
                    using var jsonDoc = JsonDocument.Parse(analysisResult);
                    var root = jsonDoc.RootElement;
                    
                    var sentimentScores = new Dictionary<string, double>();
                    
                    // Tentar extrair scores no formato padrão do Azure ML
                    if (root.TryGetProperty("sentiment_scores", out var scoresProperty))
                    {
                        foreach (var score in scoresProperty.EnumerateObject())
                        {
                            if (score.Value.TryGetDouble(out var value))
                            {
                                sentimentScores[score.Name] = value;
                            }
                        }
                    }
                    else if (root.TryGetProperty("predictions", out var predictions))
                    {
                        // Formato alternativo com predictions array
                        var firstPrediction = predictions.EnumerateArray().FirstOrDefault();
                        if (firstPrediction.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in firstPrediction.EnumerateObject())
                            {
                                if (prop.Value.TryGetDouble(out var value))
                                {
                                    sentimentScores[prop.Name] = value;
                                }
                            }
                        }
                    }
                    
                    // Garantir que temos pelo menos as métricas básicas
                    if (sentimentScores.Count == 0)
                    {
                        _logger.LogWarning("Azure ML não retornou scores de sentimento reconhecidos, usando valores padrão");
                        sentimentScores = new Dictionary<string, double>
                        {
                            ["Confidence"] = 0.5,
                            ["Positive"] = 0.33,
                            ["Neutral"] = 0.34,
                            ["Negative"] = 0.33,
                            ["Medical_Urgency"] = 0.0
                        };
                    }
                    
                    // Adicionar confidence se não presente
                    if (!sentimentScores.ContainsKey("Confidence"))
                    {
                        sentimentScores["Confidence"] = CalculateOverallConfidence(sentimentScores);
                    }
                    
                    _logger.LogInformation("Análise de sentimento médico completada com {ScoreCount} métricas", sentimentScores.Count);
                    return sentimentScores;
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Erro ao processar resposta de sentimento do Azure ML");
                    
                    // Fallback: extrair sentimento básico do texto da resposta
                    return ExtractBasicSentimentFromText(analysisResult);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro na análise de sentimento médico via Azure ML");
                throw new InvalidOperationException($"Falha na análise de sentimento médico: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Calcula confidence geral baseado nos scores individuais
        /// </summary>
        private static double CalculateOverallConfidence(Dictionary<string, double> scores)
        {
            if (scores.Count == 0) return 0.5;
            
            // Usar o maior score como indicador de confidence
            var maxScore = scores.Values.Max();
            
            // Normalizar para range 0.5-1.0 (sempre pelo menos moderadamente confiante)
            return 0.5 + (maxScore * 0.5);
        }

        /// <summary>
        /// Extrai sentimento básico de resposta texto quando JSON parsing falha
        /// </summary>
        private Dictionary<string, double> ExtractBasicSentimentFromText(string responseText)
        {
            var lowerText = responseText.ToLower();
            var positive = 0.33;
            var negative = 0.33;
            var urgency = 0.0;
            
            // Análise heurística simples baseada em palavras-chave médicas
            if (lowerText.Contains("positiv") || lowerText.Contains("bem") || lowerText.Contains("normal"))
                positive += 0.2;
            
            if (lowerText.Contains("negativ") || lowerText.Contains("problem") || lowerText.Contains("dor"))
                negative += 0.2;
                
            if (lowerText.Contains("urgent") || lowerText.Contains("emergênci") || lowerText.Contains("crítico"))
                urgency += 0.5;
            
            // Normalizar
            var total = positive + negative + (1 - positive - negative);
            positive /= total;
            negative /= total;
            var neutral = 1.0 - positive - negative;
            
            return new Dictionary<string, double>
            {
                ["Confidence"] = 0.6,
                ["Positive"] = positive,
                ["Neutral"] = neutral,
                ["Negative"] = negative,
                ["Medical_Urgency"] = urgency
            };
        }
    }
}
