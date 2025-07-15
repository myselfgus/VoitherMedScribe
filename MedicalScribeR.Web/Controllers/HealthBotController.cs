using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MedicalScribeR.Core.Services;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace MedicalScribeR.Web.Controllers
{
    /// <summary>
    /// Controlador para integração com Azure Health Bot
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Doctor,MedicalProfessional,Nurse,Admin")]
    public class HealthBotController : ControllerBase
    {
        private readonly AzureHealthBotService _healthBotService;
        private readonly ILogger<HealthBotController> _logger;

        public HealthBotController(
            AzureHealthBotService healthBotService,
            ILogger<HealthBotController> logger)
        {
            _healthBotService = healthBotService ?? throw new ArgumentNullException(nameof(healthBotService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Inicia uma nova conversa com o Health Bot
        /// </summary>
        [HttpPost("start-conversation")]
        public async Task<IActionResult> StartConversation([FromBody] StartConversationRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var userId = User?.Identity?.Name ?? request.UserId;
                var userName = User?.FindFirst("name")?.Value ?? request.UserName ?? userId;

                _logger.LogInformation("Iniciando conversa Health Bot para usuário: {UserId}", userId);

                var conversation = await _healthBotService.StartConversationAsync(userId, userName);

                var response = new StartConversationResponse
                {
                    ConversationId = conversation.ConversationId,
                    Status = "Active",
                    WelcomeMessage = "Olá! Sou seu assistente de saúde. Como posso ajudá-lo hoje?",
                    CreatedAt = conversation.CreatedAt
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao iniciar conversa Health Bot");
                return StatusCode(500, new { error = "Falha ao iniciar conversa com o assistente", details = ex.Message });
            }
        }

        /// <summary>
        /// Envia mensagem para o Health Bot
        /// </summary>
        [HttpPost("send-message")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var userId = User?.Identity?.Name ?? "anonymous";

                _logger.LogInformation("Enviando mensagem para Health Bot. Conversa: {ConversationId}", request.ConversationId);

                var context = request.MedicalContext != null ? new HealthcareChatContext
                {
                    PatientInfo = request.MedicalContext.PatientInfo,
                    MedicalHistory = request.MedicalContext.MedicalHistory,
                    CurrentSymptoms = request.MedicalContext.CurrentSymptoms,
                    ConsultationType = request.MedicalContext.ConsultationType
                } : null;

                var response = await _healthBotService.SendMessageAsync(
                    request.ConversationId, 
                    request.Message, 
                    userId, 
                    context);

                var result = new SendMessageResponse
                {
                    ConversationId = response.ConversationId,
                    Messages = response.Messages.Select(m => new MessageDto
                    {
                        Id = m.Id,
                        Text = m.Text,
                        Type = m.Type,
                        From = m.From,
                        Timestamp = m.Timestamp,
                        Confidence = m.Confidence
                    }).ToList(),
                    Success = response.Success,
                    ErrorMessage = response.ErrorMessage,
                    Timestamp = response.Timestamp
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar mensagem para Health Bot");
                return StatusCode(500, new { error = "Falha ao enviar mensagem", details = ex.Message });
            }
        }

        /// <summary>
        /// Envia contexto médico enriquecido para o bot
        /// </summary>
        [HttpPost("send-medical-context")]
        public async Task<IActionResult> SendMedicalContext([FromBody] SendMedicalContextRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var userId = User?.Identity?.Name ?? "anonymous";

                _logger.LogInformation("Enviando contexto médico para Health Bot. Conversa: {ConversationId}", request.ConversationId);

                var response = await _healthBotService.SendMedicalContextAsync(
                    request.ConversationId,
                    userId,
                    request.ConsultationType,
                    request.TranscriptionText);

                var result = new SendMessageResponse
                {
                    ConversationId = response.ConversationId,
                    Messages = response.Messages.Select(m => new MessageDto
                    {
                        Id = m.Id,
                        Text = m.Text,
                        Type = m.Type,
                        From = m.From,
                        Timestamp = m.Timestamp,
                        Confidence = m.Confidence
                    }).ToList(),
                    Success = response.Success,
                    ErrorMessage = response.ErrorMessage,
                    Timestamp = response.Timestamp
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar contexto médico para Health Bot");
                return StatusCode(500, new { error = "Falha ao enviar contexto médico", details = ex.Message });
            }
        }

        /// <summary>
        /// Obtém sugestões de perguntas baseadas no contexto
        /// </summary>
        [HttpGet("suggested-questions/{conversationId}")]
        public async Task<IActionResult> GetSuggestedQuestions(
            string conversationId, 
            [FromQuery] string medicalContext = "")
        {
            try
            {
                _logger.LogInformation("Obtendo sugestões de perguntas para conversa: {ConversationId}", conversationId);

                var suggestions = await _healthBotService.GetSuggestedQuestionsAsync(conversationId, medicalContext);

                var response = new SuggestedQuestionsResponse
                {
                    ConversationId = conversationId,
                    Questions = suggestions,
                    GeneratedAt = DateTime.UtcNow
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter sugestões de perguntas");
                return StatusCode(500, new { error = "Falha ao gerar sugestões", details = ex.Message });
            }
        }

        /// <summary>
        /// Encerra uma conversa
        /// </summary>
        [HttpPost("end-conversation/{conversationId}")]
        public async Task<IActionResult> EndConversation(string conversationId)
        {
            try
            {
                _logger.LogInformation("Encerrando conversa Health Bot: {ConversationId}", conversationId);

                var success = await _healthBotService.EndConversationAsync(conversationId);

                var response = new EndConversationResponse
                {
                    ConversationId = conversationId,
                    Success = success,
                    EndedAt = DateTime.UtcNow,
                    Message = success ? "Conversa encerrada com sucesso" : "Falha ao encerrar conversa"
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao encerrar conversa Health Bot");
                return StatusCode(500, new { error = "Falha ao encerrar conversa", details = ex.Message });
            }
        }

        /// <summary>
        /// Verifica se o serviço Health Bot está funcionando
        /// </summary>
        [HttpGet("health")]
        public async Task<IActionResult> CheckHealth()
        {
            try
            {
                var isHealthy = await _healthBotService.ValidateServiceAsync();

                var response = new HealthCheckResponse
                {
                    Service = "Azure Health Bot",
                    Status = isHealthy ? "Healthy" : "Unhealthy",
                    Timestamp = DateTime.UtcNow,
                    Details = isHealthy ? "Serviço funcionando corretamente" : "Falha na comunicação com o serviço"
                };

                return isHealthy ? Ok(response) : StatusCode(503, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar saúde do Health Bot");
                return StatusCode(500, new { error = "Falha na verificação de saúde", details = ex.Message });
            }
        }
    }

    #region Request/Response Models

    public class StartConversationRequest
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
    }

    public class StartConversationResponse
    {
        public string ConversationId { get; set; }
        public string Status { get; set; }
        public string WelcomeMessage { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class SendMessageRequest
    {
        [Required]
        public string ConversationId { get; set; }

        [Required]
        [StringLength(1000, ErrorMessage = "Mensagem deve ter no máximo 1000 caracteres")]
        public string Message { get; set; }

        public MedicalContextDto MedicalContext { get; set; }
    }

    public class SendMedicalContextRequest
    {
        [Required]
        public string ConversationId { get; set; }

        [Required]
        public string SessionId { get; set; }

        public string TranscriptionText { get; set; }
        public string ConsultationType { get; set; }
    }

    public class SendMessageResponse
    {
        public string ConversationId { get; set; }
        public List<MessageDto> Messages { get; set; } = new();
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class SuggestedQuestionsResponse
    {
        public string ConversationId { get; set; }
        public List<string> Questions { get; set; } = new();
        public DateTime GeneratedAt { get; set; }
    }

    public class EndConversationResponse
    {
        public string ConversationId { get; set; }
        public bool Success { get; set; }
        public DateTime EndedAt { get; set; }
        public string Message { get; set; }
    }

    public class HealthCheckResponse
    {
        public string Service { get; set; }
        public string Status { get; set; }
        public DateTime Timestamp { get; set; }
        public string Details { get; set; }
    }

    public class MessageDto
    {
        public string Id { get; set; }
        public string Text { get; set; }
        public string Type { get; set; }
        public string From { get; set; }
        public DateTime Timestamp { get; set; }
        public float Confidence { get; set; }
    }

    public class MedicalContextDto
    {
        public object PatientInfo { get; set; }
        public string MedicalHistory { get; set; }
        public string CurrentSymptoms { get; set; }
        public string ConsultationType { get; set; }
    }

    #endregion
}
