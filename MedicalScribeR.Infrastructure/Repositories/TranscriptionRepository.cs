using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MedicalScribeR.Core.Models;
using MedicalScribeR.Core.Interfaces;
using MedicalScribeR.Infrastructure.Data;

namespace MedicalScribeR.Infrastructure.Repositories
{
    /// <summary>
    /// Implementação do repositório para gerenciar dados de transcrição no banco de dados.
    /// Utiliza Entity Framework Core com melhores práticas de performance e tratamento de erros.
    /// </summary>
    public class TranscriptionRepository : ITranscriptionRepository
    {
        private readonly MedicalScribeDbContext _context;
        private readonly ILogger<TranscriptionRepository> _logger;

        public TranscriptionRepository(MedicalScribeDbContext context, ILogger<TranscriptionRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Session Management

        /// <summary>
        /// Cria uma nova sessão de transcrição no banco de dados.
        /// </summary>
        public async Task<TranscriptionSession> CreateSessionAsync(TranscriptionSession session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            try
            {
                _logger.LogDebug("Criando nova sessão de transcrição: {SessionId}", session.SessionId);

                // Garante que a data de criação seja UTC
                session.StartedAt = DateTime.UtcNow;

                _context.TranscriptionSessions.Add(session);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Sessão de transcrição criada com sucesso: {SessionId}", session.SessionId);

                return session;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar sessão de transcrição: {SessionId}", session.SessionId);
                throw;
            }
        }

        /// <summary>
        /// Recupera uma sessão de transcrição pelo ID da sessão.
        /// </summary>
        public async Task<TranscriptionSession?> GetSessionAsync(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException("SessionId não pode ser vazio", nameof(sessionId));

            try
            {
                _logger.LogDebug("Buscando sessão de transcrição: {SessionId}", sessionId);

                var session = await _context.TranscriptionSessions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId);

                if (session != null)
                {
                    _logger.LogDebug("Sessão encontrada: {SessionId} (Status: {Status})", sessionId, session.Status);
                }
                else
                {
                    _logger.LogWarning("Sessão não encontrada: {SessionId}", sessionId);
                }

                return session;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar sessão de transcrição: {SessionId}", sessionId);
                throw;
            }
        }

        /// <summary>
        /// Atualiza uma sessão de transcrição existente.
        /// </summary>
        public async Task<TranscriptionSession> UpdateSessionAsync(TranscriptionSession session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            try
            {
                _logger.LogDebug("Atualizando sessão de transcrição: {SessionId}", session.SessionId);

                var existingSession = await _context.TranscriptionSessions
                    .FirstOrDefaultAsync(s => s.SessionId == session.SessionId);

                if (existingSession == null)
                {
                    throw new InvalidOperationException($"Sessão não encontrada: {session.SessionId}");
                }

                // Atualiza apenas campos modificáveis
                existingSession.Status = session.Status;
                existingSession.PatientName = session.PatientName;
                existingSession.CompletedAt = session.CompletedAt;
                existingSession.Notes = session.Notes;
                existingSession.ConsultationType = session.ConsultationType;
                existingSession.TotalChunks = session.TotalChunks;
                existingSession.TotalDocuments = session.TotalDocuments;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Sessão atualizada com sucesso: {SessionId}", session.SessionId);

                return existingSession;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar sessão de transcrição: {SessionId}", session.SessionId);
                throw;
            }
        }

        /// <summary>
        /// Remove uma sessão de transcrição e todos os dados relacionados.
        /// </summary>
        public async Task<bool> DeleteSessionAsync(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException("SessionId não pode ser vazio", nameof(sessionId));

            try
            {
                _logger.LogDebug("Removendo sessão de transcrição: {SessionId}", sessionId);

                var session = await _context.TranscriptionSessions
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId);

                if (session == null)
                {
                    _logger.LogWarning("Tentativa de remover sessão inexistente: {SessionId}", sessionId);
                    return false;
                }

                // Remove todos os dados relacionados à sessão
                await RemoveRelatedDataAsync(sessionId);

                _context.TranscriptionSessions.Remove(session);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Sessão removida com sucesso: {SessionId}", sessionId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao remover sessão de transcrição: {SessionId}", sessionId);
                throw;
            }
        }

        #endregion

        #region Chunk Management

        /// <summary>
        /// Salva um chunk de transcrição no banco de dados.
        /// </summary>
        public async Task<TranscriptionChunk> SaveChunkAsync(TranscriptionChunk chunk)
        {
            if (chunk == null)
                throw new ArgumentNullException(nameof(chunk));

            try
            {
                _logger.LogDebug("Salvando chunk de transcrição: {SessionId} - Sequence {SequenceNumber}", 
                    chunk.SessionId, chunk.SequenceNumber);

                chunk.Timestamp = DateTime.UtcNow;

                _context.TranscriptionChunks.Add(chunk);
                await _context.SaveChangesAsync();

                _logger.LogDebug("Chunk salvo com sucesso: ID {ChunkId}", chunk.ChunkId);

                return chunk;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao salvar chunk de transcrição: {SessionId}", chunk.SessionId);
                throw;
            }
        }

        /// <summary>
        /// Recupera todos os chunks de uma sessão de transcrição (método alternativo).
        /// </summary>
        public async Task<IEnumerable<TranscriptionChunk>> GetSessionChunksAsync(string sessionId)
        {
            return await GetChunksBySessionAsync(sessionId);
        }

        /// <summary>
        /// Recupera todos os chunks de uma sessão de transcrição.
        /// </summary>
        public async Task<List<TranscriptionChunk>> GetChunksBySessionAsync(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException("SessionId não pode ser vazio", nameof(sessionId));

            try
            {
                _logger.LogDebug("Buscando chunks da sessão: {SessionId}", sessionId);

                var chunks = await _context.TranscriptionChunks
                    .AsNoTracking()
                    .Where(c => c.SessionId == sessionId)
                    .OrderBy(c => c.SequenceNumber)
                    .ToListAsync();

                _logger.LogDebug("Encontrados {Count} chunks para a sessão: {SessionId}", chunks.Count, sessionId);

                return chunks;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar chunks da sessão: {SessionId}", sessionId);
                throw;
            }
        }

        #endregion

        #region Document Management

        /// <summary>
        /// Salva um documento gerado no banco de dados.
        /// </summary>
        public async Task<GeneratedDocument> SaveDocumentAsync(GeneratedDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            try
            {
                _logger.LogDebug("Salvando documento gerado: {SessionId} - Tipo {Type}", 
                    document.SessionId, document.Type);

                document.CreatedAt = DateTime.UtcNow;

                _context.GeneratedDocuments.Add(document);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Documento salvo com sucesso: ID {DocumentId} - Tipo {Type}", 
                    document.DocumentId, document.Type);

                return document;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao salvar documento: {SessionId}", document.SessionId);
                throw;
            }
        }

        /// <summary>
        /// Atualiza um documento existente no banco de dados.
        /// </summary>
        public async Task<GeneratedDocument> UpdateDocumentAsync(GeneratedDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            try
            {
                _logger.LogDebug("Atualizando documento: {DocumentId}", document.DocumentId);

                document.UpdatedAt = DateTime.UtcNow;

                _context.GeneratedDocuments.Update(document);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Documento atualizado com sucesso: {DocumentId}", document.DocumentId);

                return document;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar documento: {DocumentId}", document.DocumentId);
                throw;
            }
        }

        /// <summary>
        /// Recupera todos os documentos gerados para uma sessão (método alternativo).
        /// </summary>
        public async Task<IEnumerable<GeneratedDocument>> GetSessionDocumentsAsync(string sessionId)
        {
            return await GetDocumentsBySessionAsync(sessionId);
        }

        /// <summary>
        /// Recupera todos os documentos gerados para uma sessão.
        /// </summary>
        public async Task<List<GeneratedDocument>> GetDocumentsBySessionAsync(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException("SessionId não pode ser vazio", nameof(sessionId));

            try
            {
                _logger.LogDebug("Buscando documentos da sessão: {SessionId}", sessionId);

                var documents = await _context.GeneratedDocuments
                    .AsNoTracking()
                    .Where(d => d.SessionId == sessionId)
                    .OrderByDescending(d => d.CreatedAt)
                    .ToListAsync();

                _logger.LogDebug("Encontrados {Count} documentos para a sessão: {SessionId}", documents.Count, sessionId);

                return documents;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar documentos da sessão: {SessionId}", sessionId);
                throw;
            }
        }

        #endregion

        #region Action Item Management

        /// <summary>
        /// Salva um item de ação no banco de dados.
        /// </summary>
        public async Task<ActionItem> SaveActionAsync(ActionItem action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            try
            {
                _logger.LogDebug("Salvando item de ação: {SessionId} - {Type}", 
                    action.SessionId, action.Type);

                action.CreatedAt = DateTime.UtcNow;

                _context.ActionItems.Add(action);
                await _context.SaveChangesAsync();

                _logger.LogDebug("Item de ação salvo com sucesso: ID {ActionId}", action.ActionId);

                return action;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao salvar item de ação: {SessionId}", action.SessionId);
                throw;
            }
        }

        /// <summary>
        /// Recupera todos os itens de ação de uma sessão.
        /// </summary>
        public async Task<List<ActionItem>> GetActionsBySessionAsync(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException("SessionId não pode ser vazio", nameof(sessionId));

            try
            {
                _logger.LogDebug("Buscando itens de ação da sessão: {SessionId}", sessionId);

                var actions = await _context.ActionItems
                    .AsNoTracking()
                    .Where(a => a.SessionId == sessionId)
                    .OrderBy(a => a.Priority)
                    .ThenByDescending(a => a.CreatedAt)
                    .ToListAsync();

                _logger.LogDebug("Encontrados {Count} itens de ação para a sessão: {SessionId}", actions.Count, sessionId);

                return actions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar itens de ação da sessão: {SessionId}", sessionId);
                throw;
            }
        }

        /// <summary>
        /// Recupera todos os itens de ação de uma sessão de transcrição (método alternativo).
        /// </summary>
        public async Task<IEnumerable<ActionItem>> GetSessionActionsAsync(string sessionId)
        {
            return await GetActionsBySessionAsync(sessionId);
        }

        /// <summary>
        /// Atualiza o status de um item de ação.
        /// </summary>
        public async Task<ActionItem> UpdateActionStatusAsync(Guid actionId, string status)
        {
            if (string.IsNullOrWhiteSpace(status))
                throw new ArgumentException("Status não pode ser vazio", nameof(status));

            try
            {
                _logger.LogDebug("Atualizando status do item de ação: {ActionId} para {Status}", actionId, status);

                var action = await _context.ActionItems
                    .FirstOrDefaultAsync(a => a.ActionId == actionId);

                if (action == null)
                {
                    throw new InvalidOperationException($"Item de ação não encontrado: {actionId}");
                }

                action.IsCompleted = status.Equals("Completed", StringComparison.OrdinalIgnoreCase);
                if (action.IsCompleted)
                {
                    action.CompletedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Status do item de ação atualizado: {ActionId} -> {Status}", actionId, status);

                return action;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar status do item de ação: {ActionId}", actionId);
                throw;
            }
        }

        #endregion

        #region Processing Log Management

        /// <summary>
        /// Salva um log de processamento no banco de dados.
        /// </summary>
        public async Task<ProcessingLog> SaveProcessingLogAsync(ProcessingLog log)
        {
            if (log == null)
                throw new ArgumentNullException(nameof(log));

            try
            {
                _logger.LogDebug("Salvando log de processamento: {SessionId} - {AgentName}", 
                    log.SessionId, log.AgentName);

                log.Timestamp = DateTime.UtcNow;

                _context.ProcessingLogs.Add(log);
                await _context.SaveChangesAsync();

                return log;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao salvar log de processamento: {SessionId}", log.SessionId);
                throw;
            }
        }

        /// <summary>
        /// Recupera logs de processamento de uma sessão.
        /// </summary>
        public async Task<List<ProcessingLog>> GetProcessingLogsBySessionAsync(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException("SessionId não pode ser vazio", nameof(sessionId));

            try
            {
                _logger.LogDebug("Buscando logs de processamento da sessão: {SessionId}", sessionId);

                var logs = await _context.ProcessingLogs
                    .AsNoTracking()
                    .Where(l => l.SessionId == sessionId)
                    .OrderByDescending(l => l.Timestamp)
                    .ToListAsync();

                _logger.LogDebug("Encontrados {Count} logs para a sessão: {SessionId}", logs.Count, sessionId);

                return logs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar logs da sessão: {SessionId}", sessionId);
                throw;
            }
        }

        #endregion

        #region User Session Management

        /// <summary>
        /// Recupera sessões de um usuário com paginação.
        /// </summary>
        public async Task<List<TranscriptionSession>> GetUserSessionsAsync(string userId, int skip = 0, int take = 50)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("UserId não pode ser vazio", nameof(userId));

            try
            {
                _logger.LogDebug("Buscando sessões do usuário: {UserId} (Skip: {Skip}, Take: {Take})", 
                    userId, skip, take);

                var sessions = await _context.TranscriptionSessions
                    .AsNoTracking()
                    .Where(s => s.UserId == userId)
                    .OrderByDescending(s => s.StartedAt)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();

                _logger.LogDebug("Encontradas {Count} sessões para o usuário: {UserId}", sessions.Count, userId);

                return sessions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar sessões do usuário: {UserId}", userId);
                throw;
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Remove todos os dados relacionados a uma sessão.
        /// </summary>
        private async Task RemoveRelatedDataAsync(string sessionId)
        {
            try
            {
                // Remove chunks
                var chunks = await _context.TranscriptionChunks
                    .Where(c => c.SessionId == sessionId)
                    .ToListAsync();
                _context.TranscriptionChunks.RemoveRange(chunks);

                // Remove documentos
                var documents = await _context.GeneratedDocuments
                    .Where(d => d.SessionId == sessionId)
                    .ToListAsync();
                _context.GeneratedDocuments.RemoveRange(documents);

                // Remove itens de ação
                var actions = await _context.ActionItems
                    .Where(a => a.SessionId == sessionId)
                    .ToListAsync();
                _context.ActionItems.RemoveRange(actions);

                // Remove logs
                var logs = await _context.ProcessingLogs
                    .Where(l => l.SessionId == sessionId)
                    .ToListAsync();
                _context.ProcessingLogs.RemoveRange(logs);

                _logger.LogDebug("Dados relacionados removidos: {ChunkCount} chunks, {DocumentCount} documentos, {ActionCount} ações, {LogCount} logs", 
                    chunks.Count, documents.Count, actions.Count, logs.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao remover dados relacionados da sessão: {SessionId}", sessionId);
                throw;
            }
        }

        #endregion
    }
}
