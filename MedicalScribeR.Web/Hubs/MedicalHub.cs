using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Distributed;
using MedicalScribeR.Core.Agents;
using MedicalScribeR.Core.Interfaces;
using MedicalScribeR.Core.Models;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;

namespace MedicalScribeR.Web.Hubs
{
    /// <summary>
    /// Hub SignalR para comunica��o em tempo real durante sess�es de transcri��o m�dica.
    /// Implementa isolamento por sess�o e tratamento robusto de conex�es.
    /// </summary>
    [Authorize]
    public class MedicalHub : Hub
    {
        private readonly OrchestratorAgent _orchestrator;
        private readonly ITranscriptionRepository _repository;
        private readonly IDistributedCache _cache;
        private readonly ILogger<MedicalHub> _logger;
        
        // Mapeia ConnectionId -> SessionId para cleanup
        private static readonly ConcurrentDictionary<string, string> _connectionToSession = new();
        
        // Mapeia SessionId -> Set<ConnectionId> para broadcasting eficiente
        private static readonly ConcurrentDictionary<string, HashSet<string>> _sessionConnections = new();

        public MedicalHub(
            OrchestratorAgent orchestrator,
            ITranscriptionRepository repository,
            IDistributedCache cache,
            ILogger<MedicalHub> logger)
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Inicia uma nova sess�o de transcri��o
        /// </summary>
        public async Task StartTranscription(string sessionId, string? patientName = null, string? consultationType = null)
        {
            try
            {
                var userId = GetUserId();
                
                _logger.LogInformation("Usu�rio {UserId} iniciando sess�o {SessionId}", userId, sessionId);

                // Criar sess�o no banco se n�o existir
                var existingSession = await _repository.GetSessionAsync(sessionId);
                if (existingSession == null)
                {
                    var newSession = new TranscriptionSession
                    {
                        SessionId = sessionId,
                        UserId = userId!,
                        PatientName = patientName ?? "Paciente Anônimo",
                        ConsultationType = consultationType,
                        Status = SessionStatus.Active,
                        StartedAt = DateTime.UtcNow
                    };

                    await _repository.CreateSessionAsync(newSession);
                    _logger.LogInformation("Nova sess�o criada: {SessionId}", sessionId);
                }

                // Adicionar conex�o ao grupo da sess�o
                await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
                
                // Registrar mapeamentos para cleanup
                _connectionToSession[Context.ConnectionId] = sessionId;
                _sessionConnections.AddOrUpdate(sessionId, 
                    new HashSet<string> { Context.ConnectionId },
                    (key, existing) => { existing.Add(Context.ConnectionId); return existing; });

                // Cache da sess�o no Redis para scale-out
                var sessionData = new
                {
                    SessionId = sessionId,
                    UserId = userId,
                    PatientName = patientName,
                    ConsultationType = consultationType,
                    StartedAt = DateTime.UtcNow,
                    ConnectionId = Context.ConnectionId
                };

                await _cache.SetStringAsync(
                    $"session_{sessionId}", 
                    JsonSerializer.Serialize(sessionData),
                    new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromHours(4) }
                );

                await _cache.SetStringAsync(
                    $"connection_{Context.ConnectionId}",
                    JsonSerializer.Serialize(new { SessionId = sessionId, UserId = userId, ConnectedAt = DateTime.UtcNow }),
                    new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromHours(2) }
                );

                // Notificar o grupo que a sess�o foi iniciada
                await Clients.Group(sessionId).SendAsync("SessionStarted", sessionData);

                _logger.LogInformation("Sess�o {SessionId} iniciada com sucesso para usu�rio {UserId}", sessionId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao iniciar sess�o {SessionId}", sessionId);
                await Clients.Caller.SendAsync("Error", $"Erro ao iniciar sess�o: {ex.Message}");
            }
        }

        /// <summary>
        /// Para uma sess�o de transcri��o
        /// </summary>
        public async Task StopTranscription(string sessionId)
        {
            try
            {
                var userId = GetUserId();
                
                _logger.LogInformation("Usu�rio {UserId} parando sess�o {SessionId}", userId, sessionId);

                // Atualizar status da sess�o no banco
                var session = await _repository.GetSessionAsync(sessionId);
                if (session != null)
                {
                    session.Status = SessionStatus.Completed;
                    session.EndedAt = DateTime.UtcNow;
                    await _repository.UpdateSessionAsync(session);
                }

                // Remover conex�o do grupo
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionId);
                
                // Limpar mapeamentos
                _connectionToSession.TryRemove(Context.ConnectionId, out _);
                if (_sessionConnections.TryGetValue(sessionId, out var connections))
                {
                    connections.Remove(Context.ConnectionId);
                    if (!connections.Any())
                    {
                        _sessionConnections.TryRemove(sessionId, out _);
                    }
                }

                // Notificar o grupo que a sess�o foi parada
                await Clients.Group(sessionId).SendAsync("SessionStopped", new
                {
                    SessionId = sessionId,
                    UserId = userId,
                    EndedAt = DateTime.UtcNow
                });

                _logger.LogInformation("Sess�o {SessionId} parada com sucesso", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao parar sess�o {SessionId}", sessionId);
                await Clients.Caller.SendAsync("Error", $"Erro ao parar sess�o: {ex.Message}");
            }
        }

        /// <summary>
        /// Processa um chunk de transcri��o e ativa agentes conforme necess�rio
        /// </summary>
        public async Task ProcessTranscriptionChunk(string sessionId, TranscriptionChunk chunk)
        {
            try
            {
                if (chunk == null || string.IsNullOrWhiteSpace(chunk.Text))
                {
                    _logger.LogWarning("Chunk inv�lido recebido para sess�o {SessionId}", sessionId);
                    return;
                }

                var userId = GetUserId();
                
                _logger.LogDebug("Processando chunk para sess�o {SessionId}: {ChunkLength} caracteres", 
                    sessionId, chunk.Text.Length);

                // Enriquecer chunk com informa��es da sess�o
                chunk.SessionId = sessionId;
                chunk.Timestamp = DateTime.UtcNow;

                // Notificar imediatamente a UI sobre a nova transcri��o
                var transcriptionUpdate = new
                {
                    ChunkId = chunk.Id,
                    SessionId = sessionId,
                    Text = chunk.Text,
                    Speaker = chunk.Speaker,
                    Confidence = chunk.Confidence,
                    Timestamp = chunk.Timestamp
                };

                await Clients.Group(sessionId).SendAsync("TranscriptionUpdate", transcriptionUpdate);

                // Cache da última transcrição no Redis para reconexões
                await _cache.SetStringAsync(
                    $"latest_transcription_{sessionId}",
                    JsonSerializer.Serialize(transcriptionUpdate),
                    new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(30) }
                );

                // Processar com o orquestrador (ass�ncrono para n�o bloquear UI)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var response = await _orchestrator.ProcessTranscriptionChunk(sessionId, chunk);

                        // Notificar agentes ativados
                        if (response.TriggeredAgents.Any())
                        {
                            foreach (var agentName in response.TriggeredAgents)
                            {
                                await Clients.Group(sessionId).SendAsync("AgentActivated", new
                                {
                                    AgentName = agentName,
                                    SessionId = sessionId,
                                    Confidence = response.ConfidenceScore,
                                    Timestamp = DateTime.UtcNow,
                                    Status = $"Agente {agentName} processando..."
                                });
                            }

                            // Notificar documentos gerados
                            foreach (var document in response.GeneratedDocuments)
                            {
                                await Clients.Group(sessionId).SendAsync("DocumentGenerated", new
                                {
                                    DocumentId = document.DocumentId,
                                    SessionId = sessionId,
                                    Type = document.Type,
                                    Content = document.Content,
                                    GeneratedBy = document.GeneratedBy,
                                    CreatedAt = document.CreatedAt,
                                    Status = document.Status
                                });
                            }

                            // Notificar a��es geradas
                            foreach (var action in response.Actions)
                            {
                                await Clients.Group(sessionId).SendAsync("ActionItemGenerated", new
                                {
                                    ActionId = action.ActionId,
                                    SessionId = sessionId,
                                    Title = action.Title,
                                    Description = action.Description,
                                    Type = action.Type,
                                    Priority = action.Priority,
                                    Status = action.Status,
                                    CreatedAt = action.CreatedAt
                                });
                            }

                            // Notificar processamento conclu�do
                            await Clients.Group(sessionId).SendAsync("ProcessingCompleted", new
                            {
                                SessionId = sessionId,
                                TriggeredAgents = response.TriggeredAgents,
                                DocumentsCount = response.GeneratedDocuments.Count,
                                ActionsCount = response.Actions.Count,
                                OverallConfidence = response.ConfidenceScore,
                                ProcessedAt = DateTime.UtcNow
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro no processamento ass�ncrono do chunk para sess�o {SessionId}", sessionId);
                        
                        await Clients.Group(sessionId).SendAsync("ProcessingError", new
                        {
                            SessionId = sessionId,
                            Error = "Erro interno no processamento",
                            Timestamp = DateTime.UtcNow
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar chunk de transcri��o para sess�o {SessionId}", sessionId);
                await Clients.Caller.SendAsync("Error", $"Erro ao processar transcri��o: {ex.Message}");
            }
        }

        /// <summary>
        /// Recupera o hist�rico de uma sess�o
        /// </summary>
        public async Task GetSessionHistory(string sessionId)
        {
            try
            {
                var userId = GetUserId();
                
                var session = await _repository.GetSessionAsync(sessionId);
                if (session == null || session.UserId != userId)
                {
                    await Clients.Caller.SendAsync("Error", "Sess�o n�o encontrada ou acesso negado");
                    return;
                }

                var chunks = await _repository.GetSessionChunksAsync(sessionId);
                var documents = await _repository.GetSessionDocumentsAsync(sessionId);
                var actions = await _repository.GetSessionActionsAsync(sessionId);

                await Clients.Caller.SendAsync("SessionHistory", new
                {
                    Session = session,
                    Chunks = chunks.OrderBy(c => c.Timestamp),
                    Documents = documents.OrderBy(d => d.CreatedAt),
                    Actions = actions.OrderBy(a => a.CreatedAt)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao recuperar hist�rico da sess�o {SessionId}", sessionId);
                await Clients.Caller.SendAsync("Error", $"Erro ao recuperar hist�rico: {ex.Message}");
            }
        }

        /// <summary>
        /// Lista as sess�es do usu�rio
        /// </summary>
        public async Task GetUserSessions(int skip = 0, int take = 20)
        {
            try
            {
                var userId = GetUserId();
                var sessions = await _repository.GetUserSessionsAsync(userId, skip, take);

                await Clients.Caller.SendAsync("UserSessions", new
                {
                    Sessions = sessions.OrderByDescending(s => s.StartedAt),
                    Skip = skip,
                    Take = take,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao recuperar sess�es do usu�rio");
                await Clients.Caller.SendAsync("Error", $"Erro ao recuperar sess�es: {ex.Message}");
            }
        }

        /// <summary>
        /// Recupera dados em cache para reconexões de sessão
        /// </summary>
        public async Task GetCachedSessionData(string sessionId)
        {
            try
            {
                var userId = GetUserId();
                
                // Verificar se o usuário tem acesso à sessão
                var session = await _repository.GetSessionAsync(sessionId);
                if (session == null || session.UserId != userId)
                {
                    await Clients.Caller.SendAsync("Error", "Sessão não encontrada ou acesso negado");
                    return;
                }

                // Recuperar dados em cache
                var sessionDataJson = await _cache.GetStringAsync($"session_{sessionId}");
                var latestTranscriptionJson = await _cache.GetStringAsync($"latest_transcription_{sessionId}");

                var cachedData = new
                {
                    SessionData = sessionDataJson != null ? JsonSerializer.Deserialize<object>(sessionDataJson) : null,
                    LatestTranscription = latestTranscriptionJson != null ? JsonSerializer.Deserialize<object>(latestTranscriptionJson) : null,
                    CacheRetrievedAt = DateTime.UtcNow
                };

                await Clients.Caller.SendAsync("CachedSessionData", cachedData);
                
                _logger.LogInformation("Dados em cache recuperados para sessão {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao recuperar dados em cache para sessão {SessionId}", sessionId);
                await Clients.Caller.SendAsync("Error", $"Erro ao recuperar dados em cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Gerencia desconex�o de clientes
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {
                var connectionId = Context.ConnectionId;
                
                if (_connectionToSession.TryRemove(connectionId, out var sessionId))
                {
                    // Limpar cache Redis da conexão
                    await _cache.RemoveAsync($"connection_{connectionId}");
                    
                    // Remover do grupo
                    await Groups.RemoveFromGroupAsync(connectionId, sessionId);
                    
                    // Atualizar mapeamento de sessão
                    if (_sessionConnections.TryGetValue(sessionId, out var connections))
                    {
                        connections.Remove(connectionId);
                        if (!connections.Any())
                        {
                            _sessionConnections.TryRemove(sessionId, out _);
                            
                            // Se foi a última conexão da sessão, limpar cache da sessão
                            await _cache.RemoveAsync($"session_{sessionId}");
                            await _cache.RemoveAsync($"latest_transcription_{sessionId}");
                            
                            // Se foi a última conexão da sessão, atualizar status
                            var session = await _repository.GetSessionAsync(sessionId);
                            if (session?.Status == SessionStatus.Active)
                            {
                                session.Status = SessionStatus.Disconnected;
                                session.UpdatedAt = DateTime.UtcNow;
                                await _repository.UpdateSessionAsync(session);
                            }
                        }
                    }

                    _logger.LogInformation("Cliente desconectado da sess�o {SessionId}", sessionId);
                }

                if (exception != null)
                {
                    _logger.LogWarning(exception, "Cliente desconectado com erro");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro durante desconex�o do cliente");
            }

            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Obt�m o ID do usu�rio autenticado
        /// </summary>
        private string GetUserId()
        {
            return Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? Context.User?.FindFirst("sub")?.Value 
                ?? Context.User?.FindFirst("oid")?.Value 
                ?? throw new UnauthorizedAccessException("Usu�rio n�o autenticado");
        }
    }
}