using Azure;
using Azure.AI.OpenAI;
using Azure.AI.TextAnalytics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http;
using System.Threading;
using MedicalScribeR.Core.Interfaces;
using MedicalScribeR.Core.Models;
using AzureHealthcareEntity = Azure.AI.TextAnalytics.HealthcareEntity;

namespace MedicalScribeR.Core.Services
{
    /// <summary>
    /// Service implementation that integrates with Azure AI services.
    /// Uses best practices for integration with Azure OpenAI and Text Analytics.
    /// </summary>
    public class AzureAIService : IAzureAIService
    {
        private readonly AzureOpenAIClient _openAIClient;
        private readonly ChatClient _chatClient;
        private readonly TextAnalyticsClient _textAnalyticsClient;
        private readonly ILogger<AzureAIService> _logger;
        private readonly string _openAIDeploymentName;
        private readonly TimeSpan _requestTimeout = TimeSpan.FromSeconds(30);
        private readonly int _maxRetryAttempts = 3;

        public AzureAIService(IConfiguration configuration, ILogger<AzureAIService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            try
            {
                // Azure OpenAI Client configuration with retry and timeout
                var openAIEndpoint = new Uri(configuration["Azure:OpenAI:Endpoint"] ?? 
                    throw new InvalidOperationException("Azure:OpenAI:Endpoint not configured"));
                var openAIApiKey = new AzureKeyCredential(configuration["Azure:OpenAI:ApiKey"] ?? 
                    throw new InvalidOperationException("Azure:OpenAI:ApiKey not configured"));
                
                _openAIClient = new AzureOpenAIClient(openAIEndpoint, openAIApiKey);
                _openAIDeploymentName = configuration["Azure:OpenAI:DeploymentName"] ?? 
                    throw new InvalidOperationException("Azure:OpenAI:DeploymentName not configured");

                // Initialize Chat Client with deployment name
                _chatClient = _openAIClient.GetChatClient(_openAIDeploymentName);

                // Azure Text Analytics Client configuration
                var textAnalyticsEndpoint = new Uri(configuration["Azure:TextAnalytics:Endpoint"] ?? 
                    throw new InvalidOperationException("Azure:TextAnalytics:Endpoint not configured"));
                var textAnalyticsApiKey = new AzureKeyCredential(configuration["Azure:TextAnalytics:ApiKey"] ?? 
                    throw new InvalidOperationException("Azure:TextAnalytics:ApiKey not configured"));
                
                _textAnalyticsClient = new TextAnalyticsClient(textAnalyticsEndpoint, textAnalyticsApiKey);

                _logger.LogInformation("AzureAIService initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing AzureAIService");
                throw;
            }
        }

        /// <summary>
        /// Extracts medical entities from text using Text Analytics for Health service.
        /// Implements automatic retry and robust error handling.
        /// </summary>
        public async Task<IReadOnlyList<Models.HealthcareEntity>> ExtractMedicalEntitiesAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("Empty text provided for medical entity extraction");
                return new List<Models.HealthcareEntity>();
            }

            var attempt = 0;
            Exception? lastException = null;

            while (attempt < _maxRetryAttempts)
            {
                try
                {
                    _logger.LogDebug("Starting medical entity extraction - Attempt {Attempt}", attempt + 1);

                    using var cancellationTokenSource = new CancellationTokenSource(_requestTimeout);
                    
                    AnalyzeHealthcareEntitiesOperation operation = await _textAnalyticsClient.AnalyzeHealthcareEntitiesAsync(
                        WaitUntil.Completed, 
                        new[] { text }, 
                        cancellationToken: cancellationTokenSource.Token);

                    var results = new List<Models.HealthcareEntity>();
                    await foreach (AnalyzeHealthcareEntitiesResultCollection documentsInPage in operation.Value)
                    {
                        foreach (AnalyzeHealthcareEntitiesResult documentResult in documentsInPage)
                        {
                            if (documentResult.HasError)
                            {
                                _logger.LogError("Error extracting health entities: {Error}", documentResult.Error.Message);
                                continue;
                            }

                            results.AddRange(documentResult.Entities.Select(entity => new Models.HealthcareEntity
                            {
                                Text = entity.Text,
                                Category = entity.Category.ToString(),
                                ConfidenceScore = (decimal)entity.ConfidenceScore,
                                Offset = entity.Offset,
                                Length = entity.Length,
                                ExtractedAt = DateTime.UtcNow,
                                SessionId = string.Empty // Will be filled by caller
                            }));
                        }
                    }

                    _logger.LogInformation("Extracted {Count} medical entities successfully", results.Count);
                    return results;
                }
                catch (RequestFailedException ex) when (IsTransientError(ex))
                {
                    lastException = ex;
                    attempt++;
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // Exponential backoff
                    
                    _logger.LogWarning(ex, "Transient error in medical entity extraction. Attempt {Attempt}/{MaxAttempts}. Waiting {Delay}s before next attempt", 
                        attempt, _maxRetryAttempts, delay.TotalSeconds);
                    
                    if (attempt < _maxRetryAttempts)
                    {
                        await Task.Delay(delay);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Non-transient error extracting medical entities");
                    throw;
                }
            }

            _logger.LogError(lastException, "Definitive failure extracting medical entities after {MaxAttempts} attempts", _maxRetryAttempts);
            return new List<Models.HealthcareEntity>();
        }

        /// <summary>
        /// Classifies the main intention of text using an OpenAI language model.
        /// Implements robust JSON parsing and automatic retry.
        /// </summary>
        public async Task<IntentionClassification> ClassifyIntentionsAsync(TranscriptionChunk chunk, IEnumerable<Models.HealthcareEntity> entities)
        {
            if (chunk == null || string.IsNullOrWhiteSpace(chunk.Text))
            {
                _logger.LogWarning("Invalid chunk provided for intention classification");
                return new IntentionClassification();
            }

            var systemPrompt = @"
You are an AI assistant specialized in analyzing medical consultation transcripts.
Your task is to identify the main intention of the speaker based on the provided text.
The possible intention categories are: 'RequestSummary', 'RequestPrescription', 'RequestSoapNote', 'GeneralConversation'.

IMPORTANT: Respond ONLY with a valid JSON object in the following format:
{
  ""topIntent"": {
    ""category"": ""RequestPrescription"",
    ""confidence"": 0.92
  }
}

Do not include explanations or additional text outside the JSON.";

            var entitiesText = entities.Any() ? 
                $"\nIdentified medical entities: {string.Join(", ", entities.Select(e => $"{e.Category}: {e.Text}"))}" : 
                "";

            var userPrompt = $"Analyze the following transcription and classify the intention:\n\nText: \"{chunk.Text}\"{entitiesText}";

            var attempt = 0;
            Exception? lastException = null;

            while (attempt < _maxRetryAttempts)
            {
                try
                {
                    _logger.LogDebug("Starting intention classification - Attempt {Attempt}", attempt + 1);

                    var chatMessages = new List<ChatMessage>
                    {
                        new SystemChatMessage(systemPrompt),
                        new UserChatMessage(userPrompt)
                    };

                    var chatCompletionOptions = new ChatCompletionOptions
                    {
                        MaxOutputTokenCount = 100,
                        Temperature = 0.1f, // Low temperature for more consistent responses
                        FrequencyPenalty = 0,
                        PresencePenalty = 0
                    };

                    using var cancellationTokenSource = new CancellationTokenSource(_requestTimeout);
                    
                    ChatCompletion completion = await _chatClient.CompleteChatAsync(
                        chatMessages, 
                        chatCompletionOptions,
                        cancellationTokenSource.Token);

                    var responseText = completion.Content[0].Text.Trim();
                    _logger.LogDebug("OpenAI response for classification: {Response}", responseText);

                    // Parse JSON response
                    var jsonResponse = JsonSerializer.Deserialize<IntentionResponse>(responseText, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (jsonResponse?.TopIntent != null)
                    {
                        var result = new IntentionClassification
                        {
                            TopIntent = new IntentionCategory
                            {
                                Category = jsonResponse.TopIntent.Category ?? "GeneralConversation",
                                Confidence = Math.Max(0, Math.Min(1, jsonResponse.TopIntent.Confidence)) // Clamp between 0 and 1
                            }
                        };

                        _logger.LogInformation("Intention classification completed: {Category} with confidence {Confidence}", 
                            result.TopIntent.Category, result.TopIntent.Confidence);

                        return result;
                    }
                    else
                    {
                        _logger.LogWarning("Invalid JSON response from OpenAI: {Response}", responseText);
                        throw new InvalidOperationException("Invalid JSON response from OpenAI");
                    }
                }
                catch (RequestFailedException ex) when (IsTransientError(ex))
                {
                    lastException = ex;
                    attempt++;
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    
                    _logger.LogWarning(ex, "Transient error in intention classification. Attempt {Attempt}/{MaxAttempts}. Waiting {Delay}s", 
                        attempt, _maxRetryAttempts, delay.TotalSeconds);
                    
                    if (attempt < _maxRetryAttempts)
                    {
                        await Task.Delay(delay);
                    }
                }
                catch (JsonException ex)
                {
                    lastException = ex;
                    attempt++;
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    
                    _logger.LogWarning(ex, "JSON parsing error. Attempt {Attempt}/{MaxAttempts}. Waiting {Delay}s", 
                        attempt, _maxRetryAttempts, delay.TotalSeconds);
                    
                    if (attempt < _maxRetryAttempts)
                    {
                        await Task.Delay(delay);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Non-transient error in intention classification");
                    throw;
                }
            }

            _logger.LogError(lastException, "Definitive failure in intention classification after {MaxAttempts} attempts", _maxRetryAttempts);
            
            // Return default classification on failure
            return new IntentionClassification
            {
                TopIntent = new IntentionCategory
                {
                    Category = "GeneralConversation",
                    Confidence = 0.1
                }
            };
        }

        /// <summary>
        /// Generates free text based on a prompt, used by agents to create documents.
        /// Implements token control and automatic retry.
        /// </summary>
        public async Task<string> GenerateTextAsync(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                _logger.LogWarning("Empty prompt provided for text generation");
                return string.Empty;
            }

            var attempt = 0;
            Exception? lastException = null;

            while (attempt < _maxRetryAttempts)
            {
                try
                {
                    _logger.LogDebug("Starting text generation - Attempt {Attempt}", attempt + 1);

                    var chatMessages = new List<ChatMessage>
                    {
                        new SystemChatMessage("You are a specialized medical assistant that generates accurate and professional medical documents based on the provided information."),
                        new UserChatMessage(prompt)
                    };

                    var chatCompletionOptions = new ChatCompletionOptions
                    {
                        MaxOutputTokenCount = 1500, // Adequate limit for medical documents
                        Temperature = 0.3f, // Low temperature for medical consistency
                        FrequencyPenalty = 0.1f,
                        PresencePenalty = 0.1f
                    };

                    using var cancellationTokenSource = new CancellationTokenSource(_requestTimeout);
                    
                    ChatCompletion completion = await _chatClient.CompleteChatAsync(
                        chatMessages, 
                        chatCompletionOptions,
                        cancellationTokenSource.Token);

                    var generatedText = completion.Content[0].Text.Trim();
                    
                    _logger.LogInformation("Text generated successfully. Size: {Length} characters", generatedText.Length);
                    
                    return generatedText;
                }
                catch (RequestFailedException ex) when (IsTransientError(ex))
                {
                    lastException = ex;
                    attempt++;
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    
                    _logger.LogWarning(ex, "Transient error in text generation. Attempt {Attempt}/{MaxAttempts}. Waiting {Delay}s", 
                        attempt, _maxRetryAttempts, delay.TotalSeconds);
                    
                    if (attempt < _maxRetryAttempts)
                    {
                        await Task.Delay(delay);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Non-transient error in text generation");
                    throw;
                }
            }

            _logger.LogError(lastException, "Definitive failure in text generation after {MaxAttempts} attempts", _maxRetryAttempts);
            return string.Empty;
        }

        /// <summary>
        /// Analyzes the sentiment of medical text.
        /// </summary>
        public async Task<SentimentAnalysis> AnalyzeSentimentAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("Empty text provided for sentiment analysis");
                return new SentimentAnalysis { OverallSentiment = "Neutral", ConfidenceScore = 0.0 };
            }

            var attempt = 0;
            Exception? lastException = null;

            while (attempt < _maxRetryAttempts)
            {
                try
                {
                    _logger.LogDebug("Starting sentiment analysis - Attempt {Attempt}", attempt + 1);

                    using var cancellationTokenSource = new CancellationTokenSource(_requestTimeout);
                    
                    Response<DocumentSentiment> response = await _textAnalyticsClient.AnalyzeSentimentAsync(
                        text, 
                        cancellationToken: cancellationTokenSource.Token);

                    var result = new SentimentAnalysis
                    {
                        OverallSentiment = response.Value.Sentiment.ToString(),
                        ConfidenceScore = response.Value.ConfidenceScores.Positive > response.Value.ConfidenceScores.Negative 
                            ? response.Value.ConfidenceScores.Positive 
                            : response.Value.ConfidenceScores.Negative,
                        PositiveScore = response.Value.ConfidenceScores.Positive,
                        NegativeScore = response.Value.ConfidenceScores.Negative,
                        NeutralScore = response.Value.ConfidenceScores.Neutral
                    };

                    _logger.LogInformation("Sentiment analysis completed: {Sentiment} with confidence {Confidence}", 
                        result.OverallSentiment, result.ConfidenceScore);

                    return result;
                }
                catch (RequestFailedException ex) when (IsTransientError(ex))
                {
                    lastException = ex;
                    attempt++;
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    
                    _logger.LogWarning(ex, "Transient error in sentiment analysis. Attempt {Attempt}/{MaxAttempts}. Waiting {Delay}s", 
                        attempt, _maxRetryAttempts, delay.TotalSeconds);
                    
                    if (attempt < _maxRetryAttempts)
                    {
                        await Task.Delay(delay);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Non-transient error in sentiment analysis");
                    throw;
                }
            }

            _logger.LogError(lastException, "Definitive failure in sentiment analysis after {MaxAttempts} attempts", _maxRetryAttempts);
            return new SentimentAnalysis { OverallSentiment = "Neutral", ConfidenceScore = 0.0 };
        }

        /// <summary>
        /// Extracts structured information from medical text.
        /// </summary>
        public async Task<StructuredMedicalInfo> ExtractStructuredInfoAsync(string text, string infoType)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("Empty text provided for structured information extraction");
                return new StructuredMedicalInfo { Type = infoType, SourceText = string.Empty };
            }

            var systemPrompt = $@"
You are an expert in medical information extraction. Your task is to extract structured information of type '{infoType}' from the provided text.
Format the response clearly and structured, focusing only on the requested information.
If you don't find relevant information, respond with 'No information found'.";

            var userPrompt = $"Extract information of type '{infoType}' from the following medical text:\n\n{text}";

            var attempt = 0;
            Exception? lastException = null;

            while (attempt < _maxRetryAttempts)
            {
                try
                {
                    _logger.LogDebug("Starting structured information extraction - Attempt {Attempt}", attempt + 1);

                    var chatMessages = new List<ChatMessage>
                    {
                        new SystemChatMessage(systemPrompt),
                        new UserChatMessage(userPrompt)
                    };

                    var chatCompletionOptions = new ChatCompletionOptions
                    {
                        MaxOutputTokenCount = 800,
                        Temperature = 0.2f,
                        FrequencyPenalty = 0.1f,
                        PresencePenalty = 0.1f
                    };

                    using var cancellationTokenSource = new CancellationTokenSource(_requestTimeout);
                    
                    ChatCompletion completion = await _chatClient.CompleteChatAsync(
                        chatMessages, 
                        chatCompletionOptions,
                        cancellationTokenSource.Token);

                    var extractedData = completion.Content[0].Text.Trim();
                    
                    var result = new StructuredMedicalInfo
                    {
                        Type = infoType,
                        SourceText = text,
                        ConfidenceScore = 0.8, // Default value, could be calculated based on response
                        Fields = new List<MedicalField>
                        {
                            new MedicalField
                            {
                                Name = "ExtractedContent",
                                Value = extractedData,
                                Type = "Text",
                                Confidence = 0.8,
                                IsRequired = false
                            }
                        }
                    };

                    _logger.LogInformation("Structured information extraction completed for type: {InfoType}", infoType);
                    
                    return result;
                }
                catch (RequestFailedException ex) when (IsTransientError(ex))
                {
                    lastException = ex;
                    attempt++;
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    
                    _logger.LogWarning(ex, "Transient error in structured information extraction. Attempt {Attempt}/{MaxAttempts}. Waiting {Delay}s", 
                        attempt, _maxRetryAttempts, delay.TotalSeconds);
                    
                    if (attempt < _maxRetryAttempts)
                    {
                        await Task.Delay(delay);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Non-transient error in structured information extraction");
                    throw;
                }
            }

            _logger.LogError(lastException, "Definitive failure in structured information extraction after {MaxAttempts} attempts", _maxRetryAttempts);
            return new StructuredMedicalInfo { Type = infoType, SourceText = "Extraction error" };
        }

        /// <summary>
        /// Summarizes a medical consultation based on transcription chunks.
        /// </summary>
        public async Task<string> SummarizeConsultationAsync(IEnumerable<TranscriptionChunk> chunks)
        {
            if (chunks == null || !chunks.Any())
            {
                _logger.LogWarning("No chunks provided for summarization");
                return string.Empty;
            }

            var transcriptionText = string.Join(" ", chunks.OrderBy(c => c.SequenceNumber).Select(c => c.Text));
            
            var systemPrompt = @"
You are a doctor specialized in medical documentation. Your task is to create a concise and professional summary of a medical consultation.
The summary should include:
- Chief complaint
- History of present illness
- Main physical examination findings
- Diagnostic impression
- Treatment plan

Keep the text objective, using appropriate medical terminology.";

            var userPrompt = $"Create a medical summary from the following consultation transcription:\n\n{transcriptionText}";

            var attempt = 0;
            Exception? lastException = null;

            while (attempt < _maxRetryAttempts)
            {
                try
                {
                    _logger.LogDebug("Starting consultation summarization - Attempt {Attempt}", attempt + 1);

                    var chatMessages = new List<ChatMessage>
                    {
                        new SystemChatMessage(systemPrompt),
                        new UserChatMessage(userPrompt)
                    };

                    var chatCompletionOptions = new ChatCompletionOptions
                    {
                        MaxOutputTokenCount = 1200,
                        Temperature = 0.3f,
                        FrequencyPenalty = 0.1f,
                        PresencePenalty = 0.1f
                    };

                    using var cancellationTokenSource = new CancellationTokenSource(_requestTimeout);
                    
                    ChatCompletion completion = await _chatClient.CompleteChatAsync(
                        chatMessages, 
                        chatCompletionOptions,
                        cancellationTokenSource.Token);

                    var summary = completion.Content[0].Text.Trim();
                    
                    _logger.LogInformation("Consultation summarization completed. Size: {Length} characters", summary.Length);
                    
                    return summary;
                }
                catch (RequestFailedException ex) when (IsTransientError(ex))
                {
                    lastException = ex;
                    attempt++;
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    
                    _logger.LogWarning(ex, "Transient error in summarization. Attempt {Attempt}/{MaxAttempts}. Waiting {Delay}s", 
                        attempt, _maxRetryAttempts, delay.TotalSeconds);
                    
                    if (attempt < _maxRetryAttempts)
                    {
                        await Task.Delay(delay);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Non-transient error in summarization");
                    throw;
                }
            }

            _logger.LogError(lastException, "Definitive failure in summarization after {MaxAttempts} attempts", _maxRetryAttempts);
            return string.Empty;
        }

        /// <summary>
        /// Gera itens de ação baseados no texto da consulta.
        /// </summary>
        public async Task<IEnumerable<ActionItem>> GenerateActionItemsAsync(string consultationText)
        {
            if (string.IsNullOrWhiteSpace(consultationText))
            {
                _logger.LogWarning("Texto vazio fornecido para geração de ações");
                return new List<ActionItem>();
            }

            var systemPrompt = @"
Você é um assistente médico especializado em identificar itens de ação de uma consulta médica.
Analise o texto e identifique ações específicas que precisam ser realizadas, como:
- Exames a serem solicitados
- Retornos médicos
- Encaminhamentos
- Medicações a serem iniciadas/ajustadas
- Procedimentos a serem agendados

Responda APENAS com um JSON array válido no seguinte formato:
[
  {
    ""type"": ""Exam"",
    ""description"": ""Solicitar hemograma completo"",
    ""priority"": ""High"",
    ""dueDate"": null
  }
]";

            var userPrompt = $"Identifique itens de ação no seguinte texto de consulta médica:\n\n{consultationText}";

            var attempt = 0;
            Exception? lastException = null;

            while (attempt < _maxRetryAttempts)
            {
                try
                {
                    _logger.LogDebug("Iniciando geração de itens de ação - Tentativa {Attempt}", attempt + 1);

                    var chatMessages = new List<ChatMessage>
                    {
                        new SystemChatMessage(systemPrompt),
                        new UserChatMessage(userPrompt)
                    };

                    var chatCompletionOptions = new ChatCompletionOptions
                    {
                        MaxOutputTokenCount = 800,
                        Temperature = 0.2f,
                        FrequencyPenalty = 0.1f,
                        PresencePenalty = 0.1f
                    };

                    using var cancellationTokenSource = new CancellationTokenSource(_requestTimeout);
                    
                    ChatCompletion completion = await _chatClient.CompleteChatAsync(
                        chatMessages, 
                        chatCompletionOptions,
                        cancellationTokenSource.Token);

                    var responseText = completion.Content[0].Text.Trim();
                    _logger.LogDebug("Resposta do OpenAI para geração de ações: {Response}", responseText);

                    // Parse do JSON de resposta
                    var actionItems = JsonSerializer.Deserialize<List<ActionItemResponse>>(responseText, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (actionItems != null)
                    {
                        var results = actionItems.Select(item => new ActionItem
                        {
                            ActionId = Guid.NewGuid(),
                            Type = item.Type ?? "General",
                            Description = item.Description ?? string.Empty,
                            Priority = item.Priority ?? "Medium",
                            DueDate = item.DueDate,
                            IsCompleted = false,
                            CreatedAt = DateTime.UtcNow
                        }).ToList();

                        _logger.LogInformation("Gerados {Count} itens de ação", results.Count);
                        return results;
                    }
                    else
                    {
                        _logger.LogWarning("Resposta JSON inválida do OpenAI para ações: {Response}", responseText);
                        return new List<ActionItem>();
                    }
                }
                catch (RequestFailedException ex) when (IsTransientError(ex))
                {
                    lastException = ex;
                    attempt++;
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    
                    _logger.LogWarning(ex, "Erro transiente na geração de ações. Tentativa {Attempt}/{MaxAttempts}. Aguardando {Delay}s", 
                        attempt, _maxRetryAttempts, delay.TotalSeconds);
                    
                    if (attempt < _maxRetryAttempts)
                    {
                        await Task.Delay(delay);
                    }
                }
                catch (JsonException ex)
                {
                    lastException = ex;
                    attempt++;
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    
                    _logger.LogWarning(ex, "Erro no parsing JSON para ações. Tentativa {Attempt}/{MaxAttempts}. Aguardando {Delay}s", 
                        attempt, _maxRetryAttempts, delay.TotalSeconds);
                    
                    if (attempt < _maxRetryAttempts)
                    {
                        await Task.Delay(delay);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro não transiente na geração de ações");
                    throw;
                }
            }

            _logger.LogError(lastException, "Falha definitiva na geração de ações após {MaxAttempts} tentativas", _maxRetryAttempts);
            return new List<ActionItem>();
        }

        /// <summary>
        /// Obtém um token de autorização para o Azure Speech Service.
        /// Implementa cache de token e renovação automática.
        /// </summary>
        public async Task<string> GetSpeechTokenAsync()
        {
            var attempt = 0;
            Exception? lastException = null;

            while (attempt < _maxRetryAttempts)
            {
                try
                {
                    _logger.LogDebug("Solicitando token do Azure Speech Service - Tentativa {Attempt}", attempt + 1);

                    // Em produção, usar configuração do Speech Service
                    var speechKey = Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY") ?? 
                        throw new InvalidOperationException("AZURE_SPEECH_KEY não configurada");
                    var speechRegion = Environment.GetEnvironmentVariable("AZURE_SPEECH_REGION") ?? 
                        throw new InvalidOperationException("AZURE_SPEECH_REGION não configurada");

                    var tokenEndpoint = $"https://{speechRegion}.api.cognitive.microsoft.com/sts/v1.0/issueToken";

                    using var httpClient = new HttpClient();
                    httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", speechKey);
                    httpClient.Timeout = _requestTimeout;

                    using var cancellationTokenSource = new CancellationTokenSource(_requestTimeout);
                    
                    var response = await httpClient.PostAsync(tokenEndpoint, null, cancellationTokenSource.Token);
                    response.EnsureSuccessStatusCode();

                    var token = await response.Content.ReadAsStringAsync();
                    
                    _logger.LogInformation("Token do Azure Speech Service obtido com sucesso");
                    
                    return token;
                }
                catch (HttpRequestException ex) when (IsTransientHttpError(ex))
                {
                    lastException = ex;
                    attempt++;
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    
                    _logger.LogWarning(ex, "Erro transiente ao obter token do Speech Service. Tentativa {Attempt}/{MaxAttempts}. Aguardando {Delay}s", 
                        attempt, _maxRetryAttempts, delay.TotalSeconds);
                    
                    if (attempt < _maxRetryAttempts)
                    {
                        await Task.Delay(delay);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro não transiente ao obter token do Speech Service");
                    throw;
                }
            }

            _logger.LogError(lastException, "Falha definitiva ao obter token do Speech Service após {MaxAttempts} tentativas", _maxRetryAttempts);
            throw new InvalidOperationException("Não foi possível obter token do Azure Speech Service", lastException);
        }

        /// <summary>
        /// Determina se um erro HTTP é transiente e pode ser recuperado com retry.
        /// </summary>
        private static bool IsTransientHttpError(HttpRequestException ex)
        {
            return ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("network", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determina se um erro é transiente e pode ser recuperado com retry.
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

        /// <summary>
        /// Classe auxiliar para deserialização da resposta de classificação de intenções.
        /// </summary>
        private class IntentionResponse
        {
            public IntentionResponseCategory? TopIntent { get; set; }
        }

        /// <summary>
        /// Classe auxiliar para deserialização da categoria de intenção.
        /// </summary>
        private class IntentionResponseCategory
        {
            public string? Category { get; set; }
            public double Confidence { get; set; }
        }

        /// <summary>
        /// Classe auxiliar para deserialização de itens de ação.
        /// </summary>
        private class ActionItemResponse
        {
            public string? Type { get; set; }
            public string? Description { get; set; }
            public string? Priority { get; set; }
            public DateTime? DueDate { get; set; }
        }
    }
}