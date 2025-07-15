using MedicalScribeR.Core.Models;

namespace MedicalScribeR.Core.Interfaces
{
    /// <summary>
    /// Interface para serviços de IA Azure consolidados para produção.
    /// Integra Text Analytics for Health, OpenAI e Speech Services.
    /// </summary>
    public interface IAzureAIService
    {
        // Extração de entidades médicas com Azure Text Analytics for Health
        Task<IReadOnlyList<HealthcareEntity>> ExtractMedicalEntitiesAsync(string text);
        
        // Classificação de intenções médicas usando OpenAI
        Task<IntentionClassification> ClassifyIntentionsAsync(
            TranscriptionChunk chunk, 
            IEnumerable<HealthcareEntity> entities);
        
        // Geração de texto especializado em contexto médico brasileiro
        Task<string> GenerateTextAsync(string prompt);
        
        // Análise de sentimentos médicos
        Task<SentimentAnalysis> AnalyzeSentimentAsync(string text);
        
        // Extração de informações estruturadas (prescrições, diagnósticos, etc.)
        Task<StructuredMedicalInfo> ExtractStructuredInfoAsync(string text, string infoType);
        
        // Summarização inteligente de consultas
        Task<string> SummarizeConsultationAsync(IEnumerable<TranscriptionChunk> chunks);
        
        // Geração de ações baseadas em contexto
        Task<IEnumerable<ActionItem>> GenerateActionItemsAsync(string consultationText);
    }
}