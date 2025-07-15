using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using MedicalScribeR.Core.Interfaces;
using MedicalScribeR.Core.Models;
using MedicalScribeR.Core.Services;

namespace MedicalScribeR.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DocumentController : ControllerBase
    {
        private readonly ITranscriptionRepository _repository;
        private readonly IPdfGenerationService _pdfService;
        private readonly ILogger<DocumentController> _logger;

        public DocumentController(
            ITranscriptionRepository repository, 
            IPdfGenerationService pdfService,
            ILogger<DocumentController> logger)
        {
            _repository = repository;
            _pdfService = pdfService;
            _logger = logger;
        }

        [HttpGet("session/{sessionId}")]
        public async Task<IActionResult> GetSessionDocuments(string sessionId)
        {
            try
            {
                var userId = GetUserId();
                var session = await _repository.GetSessionAsync(sessionId);
                
                if (session == null)
                {
                    return NotFound(new { error = "Sessão não encontrada" });
                }

                if (session.UserId != userId)
                {
                    return Forbid();
                }

                var documents = await _repository.GetDocumentsBySessionAsync(sessionId);
                
                return Ok(documents.Select(doc => new
                {
                    doc.DocumentId,
                    doc.Type,
                    doc.Content,
                    doc.GeneratedBy,
                    doc.CreatedAt,
                    doc.Status,
                    Confidence = doc.ConfidenceScore
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar documentos da sessão {SessionId}", sessionId);
                return StatusCode(500, new { error = "Erro interno do servidor" });
            }
        }

        [HttpGet("{documentId}")]
        public async Task<IActionResult> GetDocument(Guid documentId)
        {
            try
            {
                var userId = GetUserId();
                var documents = await _repository.GetDocumentsBySessionAsync("*");
                var document = documents.FirstOrDefault(d => d.DocumentId == documentId);
                
                if (document == null)
                {
                    return NotFound(new { error = "Documento não encontrado" });
                }

                var session = await _repository.GetSessionAsync(document.SessionId);
                if (session?.UserId != userId)
                {
                    return Forbid();
                }

                return Ok(document);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar documento {DocumentId}", documentId);
                return StatusCode(500, new { error = "Erro interno do servidor" });
            }
        }

        [HttpPut("{documentId}")]
        public async Task<IActionResult> UpdateDocument(Guid documentId, [FromBody] UpdateDocumentRequest request)
        {
            try
            {
                var userId = GetUserId();
                var documents = await _repository.GetDocumentsBySessionAsync("*");
                var document = documents.FirstOrDefault(d => d.DocumentId == documentId);

                if (document == null)
                {
                    return NotFound(new { error = "Documento não encontrado" });
                }

                var session = await _repository.GetSessionAsync(document.SessionId);
                if (session?.UserId != userId)
                {
                    return Forbid();
                }

                document.Content = request.Content;
                document.UpdatedAt = DateTime.UtcNow;
                document.Status = DocumentStatus.Modified;

                await _repository.UpdateDocumentAsync(document);

                _logger.LogInformation("Documento {DocumentId} atualizado pelo usuário {UserId}", documentId, userId);

                return Ok(document);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar documento {DocumentId}", documentId);
                return StatusCode(500, new { error = "Erro interno do servidor" });
            }
        }

        [HttpPost("{documentId}/approve")]
        public async Task<IActionResult> ApproveDocument(Guid documentId)
        {
            try
            {
                var userId = GetUserId();
                var documents = await _repository.GetDocumentsBySessionAsync("*");
                var document = documents.FirstOrDefault(d => d.DocumentId == documentId);

                if (document == null)
                {
                    return NotFound(new { error = "Documento não encontrado" });
                }

                var session = await _repository.GetSessionAsync(document.SessionId);
                if (session?.UserId != userId)
                {
                    return Forbid();
                }

                document.Status = DocumentStatus.Approved;
                document.UpdatedAt = DateTime.UtcNow;
                document.IsApproved = true;
                document.ApprovedBy = userId;
                document.ApprovedAt = DateTime.UtcNow;

                await _repository.UpdateDocumentAsync(document);

                _logger.LogInformation("Documento {DocumentId} aprovado pelo usuário {UserId}", documentId, userId);

                return Ok(new { message = "Documento aprovado com sucesso" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao aprovar documento {DocumentId}", documentId);
                return StatusCode(500, new { error = "Erro interno do servidor" });
            }
        }

        [HttpPost("{documentId}/reject")]
        public async Task<IActionResult> RejectDocument(Guid documentId, [FromBody] RejectDocumentRequest request)
        {
            try
            {
                var userId = GetUserId();
                var documents = await _repository.GetDocumentsBySessionAsync("*");
                var document = documents.FirstOrDefault(d => d.DocumentId == documentId);

                if (document == null)
                {
                    return NotFound(new { error = "Documento não encontrado" });
                }

                var session = await _repository.GetSessionAsync(document.SessionId);
                if (session?.UserId != userId)
                {
                    return Forbid();
                }

                document.Status = DocumentStatus.Rejected;
                document.UpdatedAt = DateTime.UtcNow;
                document.Content += $"\n\n[REJEITADO]: {request.Reason ?? "Sem motivo especificado"}";

                await _repository.UpdateDocumentAsync(document);

                _logger.LogInformation("Documento {DocumentId} rejeitado pelo usuário {UserId}: {Reason}", 
                    documentId, userId, request.Reason);

                return Ok(new { message = "Documento rejeitado" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao rejeitar documento {DocumentId}", documentId);
                return StatusCode(500, new { error = "Erro interno do servidor" });
            }
        }

        [HttpGet("{documentId}/pdf")]
        public async Task<IActionResult> GeneratePdf(Guid documentId)
        {
            try
            {
                var userId = GetUserId();
                var documents = await _repository.GetDocumentsBySessionAsync("*");
                var document = documents.FirstOrDefault(d => d.DocumentId == documentId);

                if (document == null)
                {
                    return NotFound(new { error = "Documento não encontrado" });
                }

                var session = await _repository.GetSessionAsync(document.SessionId);
                if (session?.UserId != userId)
                {
                    return Forbid();
                }

                var doctorInfo = await GetDoctorInfoAsync(userId);
                
                // Adicionar informações do médico ao metadata
                document.Metadata = System.Text.Json.JsonSerializer.Serialize(doctorInfo);

                var pdfBytes = _pdfService.GeneratePdf(document);

                return File(pdfBytes, "application/pdf", 
                    $"{document.Type}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar PDF do documento {DocumentId}", documentId);
                return StatusCode(500, new { error = "Erro ao gerar PDF" });
            }
        }

        [HttpDelete("{documentId}")]
        public async Task<IActionResult> DeleteDocument(Guid documentId)
        {
            try
            {
                var userId = GetUserId();
                var documents = await _repository.GetDocumentsBySessionAsync("*");
                var document = documents.FirstOrDefault(d => d.DocumentId == documentId);

                if (document == null)
                {
                    return NotFound(new { error = "Documento não encontrado" });
                }

                var session = await _repository.GetSessionAsync(document.SessionId);
                if (session?.UserId != userId)
                {
                    return Forbid();
                }

                // Soft delete
                document.Status = DocumentStatus.Draft; // Usando Draft como deleted
                document.UpdatedAt = DateTime.UtcNow;
                await _repository.UpdateDocumentAsync(document);

                _logger.LogInformation("Documento {DocumentId} deletado pelo usuário {UserId}", documentId, userId);

                return Ok(new { message = "Documento deletado com sucesso" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao deletar documento {DocumentId}", documentId);
                return StatusCode(500, new { error = "Erro interno do servidor" });
            }
        }

        #region Helper Methods

        private string GetUserId()
        {
            var userId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User?.FindFirst("sub")?.Value
                ?? User?.FindFirst("oid")?.Value;
                
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogError("Não foi possível obter ID do usuário dos claims");
                throw new UnauthorizedAccessException("Usuário não autenticado ou ID não encontrado");
            }
            
            return userId;
        }

        private Task<DoctorInfo> GetDoctorInfoAsync(string userId)
        {
            try
            {
                _logger.LogInformation("Buscando informações do médico para usuário {UserId}", userId);
                
                // TODO: Implementar busca real no banco quando ITranscriptionRepository for estendido
                // var doctor = await _repository.GetDoctorByUserIdAsync(userId);
                
                // Por enquanto, extrair do Azure AD claims
                var name = User?.FindFirst(ClaimTypes.Name)?.Value 
                    ?? User?.FindFirst("name")?.Value 
                    ?? User?.FindFirst("preferred_username")?.Value;
                    
                var email = User?.FindFirst(ClaimTypes.Email)?.Value 
                    ?? User?.FindFirst("email")?.Value;
                
                if (!string.IsNullOrEmpty(name))
                {
                    _logger.LogInformation("Usando informações do Azure AD para médico {UserId}", userId);
                    
                    return Task.FromResult(new DoctorInfo
                    {
                        Name = name,
                        CRM = "CRM não cadastrado",
                        State = "Estado não informado",
                        Specialty = "Especialidade não informada",
                        Institution = "Instituição não informada",
                        Address = "Endereço não cadastrado",
                        Phone = "Telefone não cadastrado",
                        Email = email ?? "Email não informado"
                    });
                }
                
                // Se chegou até aqui, não conseguiu obter informações básicas
                _logger.LogError("Não foi possível obter informações do médico para usuário {UserId}", userId);
                throw new InvalidOperationException("Informações do médico não encontradas. Por favor, complete seu cadastro.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar informações do médico para usuário {UserId}", userId);
                throw;
            }
        }

        #endregion
    }

    #region DTOs

    public class UpdateDocumentRequest
    {
        public string Content { get; set; } = string.Empty;
    }

    public class RejectDocumentRequest
    {
        public string? Reason { get; set; }
    }

    public class DoctorInfo
    {
        public string Name { get; set; } = string.Empty;
        public string CRM { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public string Institution { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    #endregion
}
