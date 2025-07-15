using System.ComponentModel.DataAnnotations;

namespace MedicalScribeR.Core.Configuration
{
    /// <summary>
    /// Configurações do Azure AI Services
    /// </summary>
    public class AzureAIServiceOptions
    {
        public const string ConfigurationSection = "AzureAI";

        [Required]
        public string Endpoint { get; set; } = string.Empty;

        [Required]
        public string Key { get; set; } = string.Empty;

        [Required]
        public string OpenAIEndpoint { get; set; } = string.Empty;

        [Required]
        public string OpenAIKey { get; set; } = string.Empty;

        [Required]
        public string DeploymentName { get; set; } = string.Empty;

        public string TextAnalyticsEndpoint { get; set; } = string.Empty;
        public string TextAnalyticsKey { get; set; } = string.Empty;
        public string ModelVersion { get; set; } = "gpt-4";
        public int MaxTokens { get; set; } = 2000;
        public float Temperature { get; set; } = 0.3f;
    }

    /// <summary>
    /// Configurações do Azure Speech Services
    /// </summary>
    public class SpeechServiceOptions
    {
        public const string ConfigurationSection = "AzureSpeech";

        [Required]
        public string Key { get; set; } = string.Empty;

        [Required]
        public string Region { get; set; } = string.Empty;

        public string Language { get; set; } = "pt-BR";
        public string Voice { get; set; } = "pt-BR-FranciscaNeural";
        public bool EnableDiarization { get; set; } = true;
        public int MaxSpeakers { get; set; } = 2;
    }

    /// <summary>
    /// Configurações dos agentes de IA
    /// </summary>
    public class AgentOptions
    {
        public const string ConfigurationSection = "Agents";

        public bool EnableSummaryAgent { get; set; } = true;
        public bool EnablePrescriptionAgent { get; set; } = true;
        public bool EnableDiagnosisAgent { get; set; } = true;
        public bool EnableActionItemAgent { get; set; } = true;

        public float ConfidenceThreshold { get; set; } = 0.7f;
        public int MaxRetries { get; set; } = 3;
        public int ProcessingDelayMs { get; set; } = 1000;
    }

    /// <summary>
    /// Configurações de processamento de transcrição
    /// </summary>
    public class TranscriptionOptions
    {
        public const string ConfigurationSection = "Transcription";

        public int ChunkSizeMs { get; set; } = 5000;
        public int OverlapMs { get; set; } = 500;
        public float SilenceThreshold { get; set; } = 0.1f;
        public bool EnableRealTimeProcessing { get; set; } = true;
        public int MaxSessionDurationMinutes { get; set; } = 120;
    }
}
