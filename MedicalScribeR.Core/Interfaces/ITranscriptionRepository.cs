using MedicalScribeR.Core.Models;

namespace MedicalScribeR.Core.Interfaces
{
    public interface ITranscriptionRepository
    {
        Task<TranscriptionSession> CreateSessionAsync(TranscriptionSession session);
        Task<TranscriptionSession?> GetSessionAsync(string sessionId);
        Task<TranscriptionSession> UpdateSessionAsync(TranscriptionSession session);
        Task<bool> DeleteSessionAsync(string sessionId);
        
        Task<TranscriptionChunk> SaveChunkAsync(TranscriptionChunk chunk);
        Task<List<TranscriptionChunk>> GetChunksBySessionAsync(string sessionId);
        Task<IEnumerable<TranscriptionChunk>> GetSessionChunksAsync(string sessionId);
        
        Task<GeneratedDocument> SaveDocumentAsync(GeneratedDocument document);
        Task<GeneratedDocument> UpdateDocumentAsync(GeneratedDocument document);
        Task<List<GeneratedDocument>> GetDocumentsBySessionAsync(string sessionId);
        Task<IEnumerable<GeneratedDocument>> GetSessionDocumentsAsync(string sessionId);
        
        Task<ActionItem> SaveActionAsync(ActionItem action);
        Task<List<ActionItem>> GetActionsBySessionAsync(string sessionId);
        Task<IEnumerable<ActionItem>> GetSessionActionsAsync(string sessionId);
        Task<ActionItem> UpdateActionStatusAsync(Guid actionId, string status);
        
        Task<ProcessingLog> SaveProcessingLogAsync(ProcessingLog log);
        Task<List<ProcessingLog>> GetProcessingLogsBySessionAsync(string sessionId);
        
        Task<List<TranscriptionSession>> GetUserSessionsAsync(string userId, int skip = 0, int take = 50);
    }
}
