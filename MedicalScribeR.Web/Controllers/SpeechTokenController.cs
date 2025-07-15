using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Security.Claims;

namespace MedicalScribeR.Web.Controllers
{
    [ApiController]
    [Route("api/speech-token")]
    [Authorize]
    public class SpeechTokenController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<SpeechTokenController> _logger;
        private readonly TimeSpan _requestTimeout = TimeSpan.FromSeconds(30);

        public SpeechTokenController(
            IConfiguration configuration, 
            IHttpClientFactory httpClientFactory,
            ILogger<SpeechTokenController> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Validar configuração na inicialização
            ValidateConfiguration();
        }
        
        private void ValidateConfiguration()
        {
            var region = _configuration["Azure:Speech:Region"];
            var apiKey = _configuration["Azure:Speech:ApiKey"]; // Corrigido: usar "ApiKey" conforme appsettings.json
            
            _logger.LogInformation("=== AZURE SPEECH CONFIGURATION VALIDATION ===");
            _logger.LogInformation("Region configured: {Region}", !string.IsNullOrEmpty(region) ? region : "NOT SET");
            _logger.LogInformation("ApiKey configured: {ApiKeyStatus}", !string.IsNullOrEmpty(apiKey) ? "SET" : "NOT SET");
            
            if (string.IsNullOrEmpty(region))
            {
                _logger.LogError("Azure Speech Region is not configured!");
            }
            
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogError("Azure Speech ApiKey is not configured!");
            }
        }

        [HttpGet("whisper-config")]
        public IActionResult GetWhisperConfig()
        {
            try
            {
                var userId = GetUserId();
                _logger.LogInformation("User {UserId} requesting Whisper configuration", userId);

                var whisperEndpoint = _configuration["Azure:Whisper:Endpoint"];
                var whisperApiKey = _configuration["Azure:Whisper:ApiKey"];
                var whisperDeployment = _configuration["Azure:Whisper:DeploymentName"];

                if (string.IsNullOrEmpty(whisperEndpoint) || string.IsNullOrEmpty(whisperApiKey))
                {
                    return BadRequest(new { 
                        error = "Whisper configuration missing",
                        details = "Whisper endpoint or API key not configured"
                    });
                }

                return Ok(new {
                    endpoint = whisperEndpoint,
                    apiKey = whisperApiKey,
                    deploymentName = whisperDeployment ?? "whisper",
                    success = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Whisper configuration for user {UserId}", GetUserId());
                return StatusCode(500, new { 
                    error = "Internal server error",
                    message = ex.Message
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetToken()
        {
            const int maxRetries = 3;
            const int baseDelay = 1000; // 1 segundo
            
            try
            {
                var userId = GetUserId();
                _logger.LogInformation("=== SPEECH TOKEN REQUEST ===");
                _logger.LogInformation("User {UserId} requesting Speech Service token", userId);

                var speechKey = _configuration["Azure:Speech:ApiKey"]; // Corrigido para usar ApiKey
                var speechRegion = _configuration["Azure:Speech:Region"];

                if (string.IsNullOrEmpty(speechKey) || string.IsNullOrEmpty(speechRegion))
                {
                    _logger.LogError("CRITICAL: Speech Service configuration missing!");
                    _logger.LogError("SpeechKey present: {SpeechKeyPresent}", !string.IsNullOrEmpty(speechKey));
                    _logger.LogError("SpeechRegion present: {SpeechRegionPresent}", !string.IsNullOrEmpty(speechRegion));
                    
                    return BadRequest(new { 
                        error = "Credenciais do Azure Speech Service estão ausentes ou inválidas.",
                        details = "Speech configuration is missing from appsettings.json"
                    });
                }

                var tokenEndpoint = $"https://{speechRegion}.api.cognitive.microsoft.com/sts/v1.0/issueToken";
                _logger.LogInformation("Token endpoint: {TokenEndpoint}", tokenEndpoint);

                // Retry logic com exponential backoff
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        _logger.LogInformation("Speech token attempt {Attempt}/{MaxRetries}", attempt, maxRetries);
                        
                        using var httpClient = _httpClientFactory.CreateClient();
                        httpClient.Timeout = _requestTimeout;
                        httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", speechKey);

                        var response = await httpClient.PostAsync(tokenEndpoint, null);
                        
                        _logger.LogInformation("Speech API response status: {StatusCode}", response.StatusCode);

                        if (response.IsSuccessStatusCode)
                        {
                            var token = await response.Content.ReadAsStringAsync();
                            _logger.LogInformation("Speech token obtained successfully for user {UserId}", userId);
                            
                            return Ok(new { 
                                token = token, 
                                region = speechRegion,
                                success = true
                            });
                        }
                        else
                        {
                            var errorContent = await response.Content.ReadAsStringAsync();
                            _logger.LogWarning("Speech API error on attempt {Attempt}: {StatusCode} - {ErrorContent}", 
                                attempt, response.StatusCode, errorContent);
                            
                            if (attempt == maxRetries)
                            {
                                return BadRequest(new { 
                                    error = "Falha ao obter token do Azure Speech Service",
                                    statusCode = (int)response.StatusCode,
                                    details = errorContent
                                });
                            }
                        }
                    }
                    catch (HttpRequestException httpEx)
                    {
                        _logger.LogWarning("HTTP error on attempt {Attempt}: {Error}", attempt, httpEx.Message);
                        if (attempt == maxRetries) throw;
                    }
                    catch (TaskCanceledException timeoutEx)
                    {
                        _logger.LogWarning("Timeout on attempt {Attempt}: {Error}", attempt, timeoutEx.Message);
                        if (attempt == maxRetries) throw;
                    }
                    
                    // Exponential backoff delay
                    if (attempt < maxRetries)
                    {
                        var delay = baseDelay * (int)Math.Pow(2, attempt - 1);
                        _logger.LogInformation("Waiting {Delay}ms before retry", delay);
                        await Task.Delay(delay);
                    }
                }

                return BadRequest(new { error = "All retry attempts failed" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CRITICAL ERROR: Unexpected exception in GetToken for user {UserId}", GetUserId());
                return StatusCode(500, new { 
                    error = "Erro interno do servidor", 
                    message = ex.Message,
                    type = ex.GetType().Name
                });
            }
        }

        private string GetUserId()
        {
            return User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User?.FindFirst("sub")?.Value
                ?? User?.FindFirst("oid")?.Value
                ?? "anonymous";
        }
    }
}
