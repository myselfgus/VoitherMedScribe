using System;
using System.ComponentModel.DataAnnotations;

namespace MedicalScribeR.Core.Models
{
    /// <summary>
    /// Enum para os status da sess�o de transcri��o
    /// </summary>
    public enum SessionStatus
    {
        Active,
        Completed,
        Cancelled,
        Paused,
        Error,
        Disconnected
    }

    /// <summary>
    /// Representa uma sess�o de transcri��o m�dica
    /// </summary>
    public class TranscriptionSession
    {
        [Key]
        public string SessionId { get; set; } = string.Empty;
        
        [Required]
        public string UserId { get; set; } = string.Empty;
        
        public string PatientName { get; set; } = string.Empty;
        
        public string? PatientId { get; set; }
        
        public string? Department { get; set; }
        
        [Required]
        public SessionStatus Status { get; set; } = SessionStatus.Active;
        
        [Required]
        public DateTime StartedAt { get; set; }
        
        public DateTime? CompletedAt { get; set; }
        
        public DateTime? EndedAt { get; set; }
        
        public DateTime UpdatedAt { get; set; }
        
        public string? Notes { get; set; }
        
        public string? ConsultationType { get; set; }
        
        public int TotalChunks { get; set; }
        
        public int TotalDocuments { get; set; }
        
        public int? AudioDurationSeconds { get; set; }
        
        // Propriedade de conveni�ncia para compatibilidade com c�digo existente
        public string StatusString => Status.ToString();
    }
}