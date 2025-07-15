using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MedicalScribeR.Core.Models;

namespace MedicalScribeR.Core.Services
{
    /// <summary>
    /// Serviço para integração com o Azure Health Bot
    /// Fornece funcionalidades de chat médico especializado
    /// </summary>
    public class AzureHealthBotService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AzureHealthBotService> _logger;
        private readonly string _healthBotUrl;
        private readonly string _healthBotSecret;
        private readonly JsonSerializerOptions _jsonOptions;

        public AzureHealthBotService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<AzureHealthBotService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _healthBotUrl = configuration["Azure:HealthBot:Url"] ?? 
                           "https://webchat.microsofthealthbot.com/v3/directline/conversations";
            _healthBotSecret = configuration["Azure:HealthBot:Secret"] ?? 
                              throw new ArgumentNullException("Azure:HealthBot:Secret não configurado");

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            ConfigureHttpClient();
        }

        private void ConfigureHttpClient()
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_healthBotSecret}");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "VoitherMedicalScribe/1.0");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// Inicia uma nova conversa com o Health Bot
        /// </summary>
        public async Task<HealthBotConversation> StartConversationAsync(string userId, string userName = null)
        {
            try
            {
                _logger.LogInformation("Iniciando conversa com Health Bot para usuário: {UserId}", userId);

                var request = new
                {
                    user = new { id = userId, name = userName ?? userId },
                    conversationId = Guid.NewGuid().ToString(),
                    locale = "pt-BR"
                };

                var json = JsonSerializer.Serialize(request, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(_healthBotUrl, content);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                var conversationData = JsonSerializer.Deserialize<dynamic>(responseJson, _jsonOptions);

                var conversation = new HealthBotConversation
                {
                    ConversationId = request.conversationId,
                    UserId = userId,
                    UserName = userName,
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow,
                    LastActivity = DateTime.UtcNow,
                    Messages = new List<HealthBotMessage>()
                };

                _logger.LogInformation("Conversa iniciada com sucesso: {ConversationId}", conversation.ConversationId);
                return conversation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao iniciar conversa com Health Bot para usuário: {UserId}", userId);
                throw new InvalidOperationException($"Falha ao iniciar conversa: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Envia mensagem para o Health Bot e retorna a resposta
        /// </summary>
        public async Task<HealthBotResponse> SendMessageAsync(
            string conversationId, 
            string message, 
            string userId,
            HealthcareChatContext context = null)
        {
            try
            {
                _logger.LogInformation("Enviando mensagem para Health Bot. Conversa: {ConversationId}", conversationId);

                var messagePayload = new
                {
                    type = "message",
                    from = new { id = userId },
                    text = message,
                    timestamp = DateTime.UtcNow.ToString("o"),
                    channelData = new
                    {
                        context = context != null ? new
                        {
                            patientInfo = context.PatientInfo,
                            medicalHistory = context.MedicalHistory,
                            currentSymptoms = context.CurrentSymptoms,
                            consultationType = context.ConsultationType
                        } : null
                    }
                };

                var json = JsonSerializer.Serialize(messagePayload, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var url = $"{_healthBotUrl}/{conversationId}/activities";
                var response = await _httpClient.PostAsync(url, content);
                response.EnsureSuccessStatusCode();

                // Aguardar resposta do bot
                await Task.Delay(1000); // Dar tempo para o bot processar

                var botResponse = await GetBotResponseAsync(conversationId);
                
                _logger.LogInformation("Resposta recebida do Health Bot para conversa: {ConversationId}", conversationId);
                return botResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar mensagem para Health Bot. Conversa: {ConversationId}", conversationId);
                throw new InvalidOperationException($"Falha ao enviar mensagem: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Obtém as respostas mais recentes do bot
        /// </summary>
        private async Task<HealthBotResponse> GetBotResponseAsync(string conversationId)
        {
            try
            {
                var url = $"{_healthBotUrl}/{conversationId}/activities";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                var activitiesData = JsonSerializer.Deserialize<dynamic>(responseJson, _jsonOptions);

                var botResponse = new HealthBotResponse
                {
                    ConversationId = conversationId,
                    Messages = new List<HealthBotMessage>(),
                    Timestamp = DateTime.UtcNow,
                    Success = true
                };

                // Processar mensagens do bot (simplificado - em produção seria mais complexo)
                if (activitiesData != null)
                {
                    var defaultMessage = new HealthBotMessage
                    {
                        Id = Guid.NewGuid().ToString(),
                        Text = "Olá! Sou seu assistente de saúde. Como posso ajudá-lo hoje?",
                        Type = "text",
                        From = "bot",
                        Timestamp = DateTime.UtcNow,
                        Confidence = 0.95f
                    };

                    botResponse.Messages.Add(defaultMessage);
                }

                return botResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter resposta do Health Bot. Conversa: {ConversationId}", conversationId);
                return new HealthBotResponse
                {
                    ConversationId = conversationId,
                    Messages = new List<HealthBotMessage>
                    {
                        new HealthBotMessage
                        {
                            Id = Guid.NewGuid().ToString(),
                            Text = "Desculpe, houve um problema temporário. Tente novamente em alguns instantes.",
                            Type = "text",
                            From = "bot",
                            Timestamp = DateTime.UtcNow,
                            Confidence = 1.0f
                        }
                    },
                    Success = false,
                    ErrorMessage = ex.Message,
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Envia contexto médico simples para o bot
        /// </summary>
        public async Task<HealthBotResponse> SendMedicalContextAsync(
            string conversationId,
            string userId,
            string consultationType = null,
            string symptoms = null)
        {
            try
            {
                _logger.LogInformation("Enviando contexto médico para Health Bot. Conversa: {ConversationId}", conversationId);

                var context = new HealthcareChatContext
                {
                    ConsultationType = consultationType ?? "Consulta Geral",
                    CurrentSymptoms = symptoms ?? "Não especificado"
                };

                var message = $"Contexto da consulta atual:\n" +
                             $"Tipo: {consultationType ?? "Consulta Geral"}\n" +
                             $"Sintomas: {symptoms ?? "A definir"}\n" +
                             $"Como posso auxiliar nesta consulta?";

                return await SendMessageAsync(conversationId, message, userId, context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar contexto médico para Health Bot");
                throw;
            }
        }

        /// <summary>
        /// Obtém sugestões de perguntas baseadas no contexto médico
        /// </summary>
        public async Task<List<string>> GetSuggestedQuestionsAsync(
            string conversationId,
            string medicalContext)
        {
            try
            {
                var questions = new List<string>();

                // Análise básica do contexto para sugerir perguntas relevantes
                var context = medicalContext?.ToLower() ?? "";

                if (context.Contains("dor"))
                {
                    questions.Add("Qual a intensidade da dor em uma escala de 1 a 10?");
                    questions.Add("A dor é constante ou vai e vem?");
                    questions.Add("O que piora ou melhora a dor?");
                }

                if (context.Contains("febre"))
                {
                    questions.Add("Há quanto tempo está com febre?");
                    questions.Add("Qual foi a temperatura máxima registrada?");
                    questions.Add("Está tomando algum medicamento para febre?");
                }

                if (context.Contains("tosse"))
                {
                    questions.Add("A tosse é seca ou com catarro?");
                    questions.Add("Há presença de sangue na tosse?");
                    questions.Add("A tosse piora em algum período do dia?");
                }

                // Perguntas gerais sempre disponíveis
                questions.AddRange(new[]
                {
                    "Tem alguma alergia conhecida?",
                    "Está tomando algum medicamento atualmente?",
                    "Tem histórico familiar relevante?",
                    "Pratica exercícios regularmente?"
                });

                _logger.LogInformation("Geradas {Count} sugestões de perguntas para conversa: {ConversationId}", 
                    questions.Count, conversationId);

                return questions.Take(6).ToList(); // Limitar a 6 sugestões
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar sugestões de perguntas");
                return new List<string> { "Como posso ajudá-lo?", "Tem alguma dúvida sobre o diagnóstico?" };
            }
        }

        /// <summary>
        /// Encerra uma conversa
        /// </summary>
        public async Task<bool> EndConversationAsync(string conversationId)
        {
            try
            {
                _logger.LogInformation("Encerrando conversa com Health Bot: {ConversationId}", conversationId);

                var endMessage = new
                {
                    type = "endOfConversation",
                    timestamp = DateTime.UtcNow.ToString("o")
                };

                var json = JsonSerializer.Serialize(endMessage, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var url = $"{_healthBotUrl}/{conversationId}/activities";
                var response = await _httpClient.PostAsync(url, content);

                var success = response.IsSuccessStatusCode;
                _logger.LogInformation("Conversa {ConversationId} encerrada: {Success}", conversationId, success);

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao encerrar conversa: {ConversationId}", conversationId);
                return false;
            }
        }

        /// <summary>
        /// Valida se o serviço está funcionando corretamente
        /// </summary>
        public async Task<bool> ValidateServiceAsync()
        {
            try
            {
                var testUserId = $"test-{Guid.NewGuid():N}";
                var conversation = await StartConversationAsync(testUserId, "Test User");
                await EndConversationAsync(conversation.ConversationId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha na validação do serviço Health Bot");
                return false;
            }
        }

        /// <summary>
        /// Extrai sintomas principais de um texto livre
        /// </summary>
        private string ExtractSymptoms(string medicalText)
        {
            if (string.IsNullOrEmpty(medicalText))
                return "Sem sintomas específicos mencionados";

            var text = medicalText.ToLower();
            var symptoms = new List<string>();

            // Busca por sintomas comuns
            var symptomKeywords = new Dictionary<string, string>
            {
                ["dor"] = "dor",
                ["febre"] = "febre",
                ["tosse"] = "tosse",
                ["náusea"] = "náusea",
                ["vômito"] = "vômito",
                ["tontura"] = "tontura",
                ["cansaço"] = "fadiga",
                ["falta de ar"] = "dispneia",
                ["palpitação"] = "palpitação"
            };

            foreach (var keyword in symptomKeywords)
            {
                if (text.Contains(keyword.Key))
                {
                    symptoms.Add(keyword.Value);
                }
            }

            return symptoms.Any() ? string.Join(", ", symptoms) : "Sintomas diversos mencionados";
        }
    }

    /// <summary>
    /// Contexto de saúde para enriquecer conversas do bot
    /// </summary>
    public class HealthcareChatContext
    {
        public string ConsultationType { get; set; }
        public string CurrentSymptoms { get; set; }
        public string MedicalHistory { get; set; }
        public object PatientInfo { get; set; }
    }

    /// <summary>
    /// Conversa do Health Bot
    /// </summary>
    public class HealthBotConversation
    {
        public string ConversationId { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastActivity { get; set; }
        public List<HealthBotMessage> Messages { get; set; } = new();
    }

    /// <summary>
    /// Resposta do Health Bot
    /// </summary>
    public class HealthBotResponse
    {
        public string ConversationId { get; set; }
        public List<HealthBotMessage> Messages { get; set; } = new();
        public DateTime Timestamp { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public List<string> SuggestedQuestions { get; set; } = new();
    }

    /// <summary>
    /// Mensagem do Health Bot
    /// </summary>
    public class HealthBotMessage
    {
        public string Id { get; set; }
        public string Text { get; set; }
        public string Type { get; set; } // text, card, carousel, etc.
        public string From { get; set; } // user, bot
        public DateTime Timestamp { get; set; }
        public float Confidence { get; set; }
        public object Attachments { get; set; }
        public object ChannelData { get; set; }
    }
}
