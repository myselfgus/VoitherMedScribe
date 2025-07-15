using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MedicalScribeR.Core.Interfaces;
using MedicalScribeR.Core.Models;
using MedicalScribeR.Web.Models;
using System.Security.Claims;
using System.Diagnostics;

namespace MedicalScribeR.Web.Controllers
{
    /// <summary>
    /// Controller principal para a interface web do MedicalScribe.
    /// Gerencia a p�gina inicial e funcionalidades b�sicas da aplica��o.
    /// </summary>
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ITranscriptionRepository _repository;

        public HomeController(
            ILogger<HomeController> logger,
            ITranscriptionRepository repository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        /// <summary>
        /// P�gina principal da aplica��o - Interface de transcri��o
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var userId = GetUserId();
                _logger.LogDebug("Usu�rio {UserId} acessando p�gina principal", userId);

                // Buscar estat�sticas b�sicas do usu�rio
                var recentSessions = await _repository.GetUserSessionsAsync(userId, 0, 5);
                
                var viewModel = new HomeViewModel
                {
                    UserId = userId,
                    UserName = GetUserName(),
                    RecentSessions = recentSessions.ToList(),
                    TotalSessions = recentSessions.Count(), // Em produ��o, implementar contagem otimizada
                    WelcomeMessage = GetWelcomeMessage()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar p�gina principal");
                return View("Error", new ErrorViewModel 
                { 
                    RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    Message = "Erro ao carregar a p�gina principal. Tente novamente."
                });
            }
        }

        /// <summary>
        /// P�gina de dashboard com estat�sticas detalhadas
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                var userId = GetUserId();
                _logger.LogDebug("Usu�rio {UserId} acessando dashboard", userId);

                // Buscar estat�sticas mais detalhadas
                var sessions = await _repository.GetUserSessionsAsync(userId, 0, 50);
                var sessionsList = sessions.ToList();

                var dashboardData = new DashboardViewModel
                {
                    TotalSessions = sessionsList.Count,
                    SessionsThisMonth = sessionsList.Count(s => s.StartedAt.Month == DateTime.Now.Month),
                    TotalTranscriptionTime = sessionsList.Sum(s => s.AudioDurationSeconds ?? 0),
                    AverageSessionDuration = sessionsList.Any() ? 
                        sessionsList.Average(s => s.AudioDurationSeconds ?? 0) : 0,
                    RecentSessions = sessionsList.Take(10).ToList(),
                    SessionsByStatus = sessionsList.GroupBy(s => s.Status)
                        .ToDictionary(g => g.Key.ToString(), g => g.Count())
                };

                return View(dashboardData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar dashboard");
                return View("Error", new ErrorViewModel 
                { 
                    RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    Message = "Erro ao carregar o dashboard. Tente novamente."
                });
            }
        }

        /// <summary>
        /// P�gina de hist�rico de sess�es
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> History(int page = 1, int pageSize = 20)
        {
            try
            {
                var userId = GetUserId();
                _logger.LogDebug("Usu�rio {UserId} acessando hist�rico - P�gina {Page}", userId, page);

                var skip = (page - 1) * pageSize;
                var sessions = await _repository.GetUserSessionsAsync(userId, skip, pageSize);
                var sessionsList = sessions.ToList();

                var historyViewModel = new HistoryViewModel
                {
                    Sessions = sessionsList,
                    CurrentPage = page,
                    PageSize = pageSize,
                    HasNextPage = sessionsList.Count == pageSize,
                    HasPreviousPage = page > 1
                };

                return View(historyViewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar hist�rico");
                return View("Error", new ErrorViewModel 
                { 
                    RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    Message = "Erro ao carregar o hist�rico. Tente novamente."
                });
            }
        }

        /// <summary>
        /// P�gina de configura��es do usu�rio
        /// </summary>
        [HttpGet]
        public IActionResult Settings()
        {
            try
            {
                var userId = GetUserId();
                var userName = GetUserName();

                var settingsViewModel = new SettingsViewModel
                {
                    UserId = userId,
                    UserName = userName,
                    // Configura��es padr�o - em produ��o, buscar do banco
                    EnableRealTimeTranscription = true,
                    EnableAgentNotifications = true,
                    PreferredLanguage = "pt-BR",
                    AutoSaveInterval = 30
                };

                return View(settingsViewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar configura��es");
                return View("Error", new ErrorViewModel 
                { 
                    RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    Message = "Erro ao carregar as configura��es. Tente novamente."
                });
            }
        }

        /// <summary>
        /// P�gina sobre o sistema
        /// </summary>
        [HttpGet]
        public IActionResult About()
        {
            var aboutViewModel = new AboutViewModel
            {
                ApplicationName = "MedicalScribe",
                Version = "1.0.0",
                BuildDate = DateTime.Now, // Em produ��o, usar data real do build
                Description = "Sistema de transcri��o e documenta��o m�dica inteligente",
                Features = new List<string>
                {
                    "Transcri��o em tempo real",
                    "Agentes especializados de IA",
                    "Gera��o autom�tica de documentos m�dicos",
                    "Integra��o com Azure AI Services",
                    "Interface otimizada para profissionais de sa�de"
                }
            };

            return View(aboutViewModel);
        }

        /// <summary>
        /// P�gina de ajuda e documenta��o
        /// </summary>
        [HttpGet]
        public IActionResult Help()
        {
            return View();
        }

        /// <summary>
        /// Endpoint para verificar status da aplica��o
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Health()
        {
            return Json(new 
            { 
                status = "healthy", 
                timestamp = DateTime.UtcNow,
                version = "1.0.0"
            });
        }

        /// <summary>
        /// P�gina de erro personalizada
        /// </summary>
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel 
            { 
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier 
            });
        }

        /// <summary>
        /// P�gina de erro de autoriza��o
        /// </summary>
        [HttpGet]
        public IActionResult AccessDenied()
        {
            _logger.LogWarning("Acesso negado para usu�rio {UserId}", GetUserId());
            
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                Message = "Voc� n�o tem permiss�o para acessar este recurso."
            });
        }

        #region Helper Methods

        /// <summary>
        /// Obt�m o ID do usu�rio autenticado
        /// </summary>
        private string GetUserId()
        {
            return User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User?.FindFirst("sub")?.Value
                ?? User?.FindFirst("oid")?.Value
                ?? throw new UnauthorizedAccessException("Usu�rio n�o autenticado");
        }

        /// <summary>
        /// Obt�m o nome do usu�rio autenticado
        /// </summary>
        private string GetUserName()
        {
            return User?.FindFirst(ClaimTypes.Name)?.Value
                ?? User?.FindFirst("name")?.Value
                ?? User?.FindFirst(ClaimTypes.Email)?.Value
                ?? "Usu�rio";
        }

        /// <summary>
        /// Gera mensagem de boas-vindas personalizada
        /// </summary>
        private string GetWelcomeMessage()
        {
            var hour = DateTime.Now.Hour;
            var greeting = hour switch
            {
                >= 5 and < 12 => "Bom dia",
                >= 12 and < 18 => "Boa tarde",
                _ => "Boa noite"
            };

            return $"{greeting}, {GetUserName()}! Bem-vindo ao MedicalScribe.";
        }

        #endregion
    }
}
