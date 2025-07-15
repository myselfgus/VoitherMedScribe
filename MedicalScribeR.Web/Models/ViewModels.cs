using MedicalScribeR.Core.Models;

namespace MedicalScribeR.Web.Models
{
    /// <summary>
    /// ViewModel para a página principal
    /// </summary>
    public class HomeViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string WelcomeMessage { get; set; } = string.Empty;
        public List<TranscriptionSession> RecentSessions { get; set; } = new();
        public int TotalSessions { get; set; }
    }

    /// <summary>
    /// ViewModel para o dashboard
    /// </summary>
    public class DashboardViewModel
    {
        public int TotalSessions { get; set; }
        public int SessionsThisMonth { get; set; }
        public int TotalTranscriptionTime { get; set; }
        public double AverageSessionDuration { get; set; }
        public List<TranscriptionSession> RecentSessions { get; set; } = new();
        public Dictionary<string, int> SessionsByStatus { get; set; } = new();
    }

    /// <summary>
    /// ViewModel para o histórico
    /// </summary>
    public class HistoryViewModel
    {
        public List<TranscriptionSession> Sessions { get; set; } = new();
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
    }

    /// <summary>
    /// ViewModel para configurações
    /// </summary>
    public class SettingsViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public bool EnableRealTimeTranscription { get; set; }
        public bool EnableAgentNotifications { get; set; }
        public string PreferredLanguage { get; set; } = "pt-BR";
        public int AutoSaveInterval { get; set; }
    }

    /// <summary>
    /// ViewModel para a página sobre
    /// </summary>
    public class AboutViewModel
    {
        public string ApplicationName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public DateTime BuildDate { get; set; }
        public string Description { get; set; } = string.Empty;
        public List<string> Features { get; set; } = new();
    }

    /// <summary>
    /// ViewModel para erros
    /// </summary>
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }
        public string? Message { get; set; }
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}