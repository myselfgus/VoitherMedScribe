using System;
using System.ComponentModel.DataAnnotations;

namespace MedicalScribeR.Core.Models
{
    /// <summary>
    /// Representa uma entidade médica extraída do texto
    /// </summary>
    public class HealthcareEntity
    {
        [Key]
        public Guid EntityId { get; set; }
        
        [Required]
        public string SessionId { get; set; } = string.Empty;
        
        [Required]
        public string Text { get; set; } = string.Empty;
        
        [Required]
        public string Category { get; set; } = string.Empty; // Medication, Condition, Symptom, etc.
        
        public string? SubCategory { get; set; }
        
        public decimal ConfidenceScore { get; set; }
        
        public int Offset { get; set; }
        
        public int Length { get; set; }
        
        public string? NormalizedText { get; set; }
        
        public string? Links { get; set; } // JSON com links para ontologias médicas
        
        public DateTime ExtractedAt { get; set; }
    }
}