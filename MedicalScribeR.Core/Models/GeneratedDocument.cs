using System;
using System.ComponentModel.DataAnnotations;

namespace MedicalScribeR.Core.Models
{
    /// <summary>
    /// Enum para os status do documento
    /// </summary>
    public enum DocumentStatus
    {
        Generated,
        Modified,
        Approved,
        Rejected,
        Draft
    }

    /// <summary>
    /// Representa um documento gerado pelos agentes de IA
    /// </summary>
    public class GeneratedDocument
    {
        [Key]
        public Guid DocumentId { get; set; }
        
        [Required]
        public string SessionId { get; set; } = string.Empty;
        
        [Required]
        public string Type { get; set; } = string.Empty; // Summary, Prescription, Diagnosis, etc.
        
        [Required]
        public string Content { get; set; } = string.Empty;
        
        [Required]
        public string GeneratedBy { get; set; } = string.Empty; // Nome do agente
        
        [Required]
        public DateTime CreatedAt { get; set; }
        
        public DateTime UpdatedAt { get; set; }
        
        public DocumentStatus Status { get; set; } = DocumentStatus.Generated;
        
        public bool IsApproved { get; set; }
        
        public string? ApprovedBy { get; set; }
        
        public DateTime? ApprovedAt { get; set; }
        
        public string? ReviewedBy { get; set; }
        
        public DateTime? ReviewedAt { get; set; }
        
        public string? ValidationStatus { get; set; }
        
        public string? Version { get; set; }
        
        public decimal ConfidenceScore { get; set; }
        
        public string? Metadata { get; set; } // JSON com metadata adicional
    }
}