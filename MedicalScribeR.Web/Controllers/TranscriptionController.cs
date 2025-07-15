using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MedicalScribeR.Core.Interfaces;
using MedicalScribeR.Core.Models;
using System.Security.Claims;

namespace MedicalScribeR.Web.Controllers
{
    /// <summary>
    /// Controller para gerenciamento de sess�es de transcri��o.
    /// Fornece endpoints REST para opera��es CRUD de sess�es e chunks de transcri��o.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TranscriptionController : ControllerBase
    {
        private readonly ITranscriptionRepository _repository;
        private readonly ILogger<TranscriptionController> _logger;

        public TranscriptionController(
            ITranscriptionRepository repository,
            ILogger<TranscriptionController> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Cria uma nova sess�o de transcri��o
        /// </summary>
        [HttpPost("sessions")]
        public async Task<ActionResult<TranscriptionSession>> CreateSession([FromBody] CreateSessionRequest request)
        {
            try
            {
                var userId = GetUserId();
                _logger.LogInformation("Criando nova sess�o para usu�rio {UserId}", userId);

                var session = new TranscriptionSession
                {
                    SessionId = request.SessionId ?? Guid.NewGuid().ToString(),
                    UserId = userId!,
                    PatientName = request.PatientName ?? "Paciente Anônimo",
                    PatientId = request.PatientId,
                    ConsultationType = request.ConsultationType,
                    Department = request.Department,
                    Notes = request.Notes,
                    Status = SessionStatus.Active,
                    StartedAt = DateTime.UtcNow
                };

                var createdSession = await _repository.CreateSessionAsync(session);
                
                _logger.LogInformation("Sess�o {SessionId} criada com sucesso", createdSession.SessionId);
                
                return CreatedAtAction(
                    nameof(GetSession), 
                    new { sessionId = createdSession.SessionId }, 
                    createdSession);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar sess�o");
                return StatusCode(500, new { error = "Erro interno do servidor ao criar sess�o" });
            }
        }

        /// <summary>
        /// Obt�m uma sess�o espec�fica por ID
        /// </summary>
        [HttpGet("sessions/{sessionId}")]
        public async Task<ActionResult<TranscriptionSession>> GetSession(string sessionId)
        {
            try
            {
                var userId = GetUserId();
                var session = await _repository.GetSessionAsync(sessionId);

                if (session == null)
                {
                    return NotFound(new { error = "Sess�o n�o encontrada" });
                }

                // Verificar se o usu�rio tem acesso � sess�o
                if (session.UserId != userId)
                {
                    return Forbid();
                }

                return Ok(session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar sess�o {SessionId}", sessionId);
                return StatusCode(500, new { error = "Erro interno do servidor" });
            }
        }

        /// <summary>
        /// Lista as sess�es do usu�rio autenticado
        /// </summary>
        [HttpGet("sessions")]
        public async Task<ActionResult<PagedResult<TranscriptionSession>>> GetUserSessions(
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 20,
            [FromQuery] SessionStatus? status = null,
            [FromQuery] string? searchTerm = null)
        {
            try
            {
                var userId = GetUserId();
                var skip = (page - 1) * pageSize;

                _logger.LogDebug("Buscando sess�es do usu�rio {UserId} - P�gina {Page}", userId, page);

                var sessions = await _repository.GetUserSessionsAsync(userId, skip, pageSize);
                var sessionsList = sessions.ToList();

                // Filtrar por status se especificado
                if (status.HasValue)
                {
                    sessionsList = sessionsList.Where(s => s.Status == status.Value).ToList();
                }

                // Filtrar por termo de busca se especificado
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    sessionsList = sessionsList.Where(s => 
                        (s.PatientName?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (s.ConsultationType?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        s.SessionId.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                    ).ToList();
                }

                var result = new PagedResult<TranscriptionSession>
                {
                    Data = sessionsList,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = sessionsList.Count, // Em produ��o, implementar contagem otimizada
                    HasNextPage = sessionsList.Count == pageSize,
                    HasPreviousPage = page > 1
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar sess�es do usu�rio");
                return StatusCode(500, new { error = "Erro interno do servidor" });
            }
        }

        /// <summary>
        /// Atualiza uma sess�o existente
        /// </summary>
        [HttpPut("sessions/{sessionId}")]
        public async Task<ActionResult> UpdateSession(string sessionId, [FromBody] UpdateSessionRequest request)
        {
            try
            {
                var userId = GetUserId();
                var session = await _repository.GetSessionAsync(sessionId);

                if (session == null)
                {
                    return NotFound(new { error = "Sess�o n�o encontrada" });
                }

                if (session.UserId != userId)
                {
                    return Forbid();
                }

                // Atualizar campos permitidos
                if (request.PatientName != null)
                    session.PatientName = request.PatientName;
                
                if (request.PatientId != null)
                    session.PatientId = request.PatientId;
                
                if (request.ConsultationType != null)
                    session.ConsultationType = request.ConsultationType;
                
                if (request.Department != null)
                    session.Department = request.Department;
                
                if (request.Notes != null)
                    session.Notes = request.Notes;
                
                if (request.Status.HasValue)
                    session.Status = request.Status.Value;

                session.UpdatedAt = DateTime.UtcNow;

                await _repository.UpdateSessionAsync(session);

                _logger.LogInformation("Sess�o {SessionId} atualizada com sucesso", sessionId);
                
                return Ok(session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar sess�o {SessionId}", sessionId);
                return StatusCode(500, new { error = "Erro interno do servidor" });
            }
        }

        /// <summary>
        /// Finaliza uma sess�o de transcri��o
        /// </summary>
        [HttpPost("sessions/{sessionId}/complete")]
        public async Task<ActionResult> CompleteSession(string sessionId)
        {
            try
            {
                var userId = GetUserId();
                var session = await _repository.GetSessionAsync(sessionId);

                if (session == null)
                {
                    return NotFound(new { error = "Sess�o n�o encontrada" });
                }

                if (session.UserId != userId)
                {
                    return Forbid();
                }

                session.Status = SessionStatus.Completed;
                session.EndedAt = DateTime.UtcNow;
                session.UpdatedAt = DateTime.UtcNow;

                await _repository.UpdateSessionAsync(session);

                _logger.LogInformation("Sess�o {SessionId} finalizada com sucesso", sessionId);
                
                return Ok(new { message = "Sess�o finalizada com sucesso", session });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao finalizar sess�o {SessionId}", sessionId);
                return StatusCode(500, new { error = "Erro interno do servidor" });
            }
        }

        /// <summary>
        /// Deleta uma sess�o e todos os dados relacionados
        /// </summary>
        [HttpDelete("sessions/{sessionId}")]
        public async Task<ActionResult> DeleteSession(string sessionId)
        {
            try
            {
                var userId = GetUserId();
                var session = await _repository.GetSessionAsync(sessionId);

                if (session == null)
                {
                    return NotFound(new { error = "Sess�o n�o encontrada" });
                }

                if (session.UserId != userId)
                {
                    return Forbid();
                }

                await _repository.DeleteSessionAsync(sessionId);

                _logger.LogInformation("Sess�o {SessionId} deletada com sucesso", sessionId);
                
                return Ok(new { message = "Sess�o deletada com sucesso" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao deletar sess�o {SessionId}", sessionId);
                return StatusCode(500, new { error = "Erro interno do servidor" });
            }
        }

        /// <summary>
        /// Obt�m os chunks de transcri��o de uma sess�o
        /// </summary>
        [HttpGet("sessions/{sessionId}/chunks")]
        public async Task<ActionResult<IEnumerable<TranscriptionChunk>>> GetSessionChunks(string sessionId)
        {
            try
            {
                var userId = GetUserId();
                var session = await _repository.GetSessionAsync(sessionId);

                if (session == null)
                {
                    return NotFound(new { error = "Sess�o n�o encontrada" });
                }

                if (session.UserId != userId)
                {
                    return Forbid();
                }

                var chunks = await _repository.GetSessionChunksAsync(sessionId);
                
                return Ok(chunks.OrderBy(c => c.Timestamp));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar chunks da sess�o {SessionId}", sessionId);
                return StatusCode(500, new { error = "Erro interno do servidor" });
            }
        }

        /// <summary>
        /// Obt�m estat�sticas de uma sess�o
        /// </summary>
        [HttpGet("sessions/{sessionId}/stats")]
        public async Task<ActionResult<SessionStats>> GetSessionStats(string sessionId)
        {
            try
            {
                var userId = GetUserId();
                var session = await _repository.GetSessionAsync(sessionId);

                if (session == null)
                {
                    return NotFound(new { error = "Sess�o n�o encontrada" });
                }

                if (session.UserId != userId)
                {
                    return Forbid();
                }

                var chunks = await _repository.GetSessionChunksAsync(sessionId);
                var documents = await _repository.GetSessionDocumentsAsync(sessionId);
                var actions = await _repository.GetSessionActionsAsync(sessionId);

                var chunksList = chunks.ToList();
                var documentsList = documents.ToList();
                var actionsList = actions.ToList();

                var stats = new SessionStats
                {
                    TotalChunks = chunksList.Count,
                    TotalWords = chunksList.Sum(c => c.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length),
                    AverageConfidence = chunksList.Any() ? (double)chunksList.Average(c => c.Confidence) : 0,
                    DocumentsGenerated = documentsList.Count,
                    ActionsGenerated = actionsList.Count,
                    Duration = session.EndedAt.HasValue ? 
                        (session.EndedAt.Value - session.StartedAt).TotalMinutes : 
                        (DateTime.UtcNow - session.StartedAt).TotalMinutes,
                    DocumentTypes = documentsList.GroupBy(d => d.Type)
                        .ToDictionary(g => g.Key, g => g.Count())
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar estat�sticas da sess�o {SessionId}", sessionId);
                return StatusCode(500, new { error = "Erro interno do servidor" });
            }
        }

        #region Helper Methods

        private string GetUserId()
        {
            return User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User?.FindFirst("sub")?.Value
                ?? User?.FindFirst("oid")?.Value
                ?? throw new UnauthorizedAccessException("Usu�rio n�o autenticado");
        }

        #endregion
    }

    #region DTOs

    public class CreateSessionRequest
    {
        public string? SessionId { get; set; }
        public string? PatientName { get; set; }
        public string? PatientId { get; set; }
        public string? ConsultationType { get; set; }
        public string? Department { get; set; }
        public string? Notes { get; set; }
    }

    public class UpdateSessionRequest
    {
        public string? PatientName { get; set; }
        public string? PatientId { get; set; }
        public string? ConsultationType { get; set; }
        public string? Department { get; set; }
        public string? Notes { get; set; }
        public SessionStatus? Status { get; set; }
    }

    public class PagedResult<T>
    {
        public List<T> Data { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
    }

    public class SessionStats
    {
        public int TotalChunks { get; set; }
        public int TotalWords { get; set; }
        public double AverageConfidence { get; set; }
        public int DocumentsGenerated { get; set; }
        public int ActionsGenerated { get; set; }
        public double Duration { get; set; }
        public Dictionary<string, int> DocumentTypes { get; set; } = new();
    }

    #endregion
}
