using System;
using System.ComponentModel.DataAnnotations;

namespace MedicalScribeR.Core.Models
{
    /// <summary>
    /// Log de processamento para auditoria
    /// </summary>
    public class ProcessingLog
    {
        [Key]
        public Guid LogId { get; set; }
        
        [Required]
        public string SessionId { get; set; } = string.Empty;
        
        [Required]
        public string AgentName { get; set; } = string.Empty;
        
        [Required]
        public string Action { get; set; } = string.Empty;
        
        public string? Details { get; set; }
        
        public string? ErrorMessage { get; set; }
        
        public bool IsSuccess { get; set; }
        
        public TimeSpan Duration { get; set; }
        
        [Required]
        public DateTime Timestamp { get; set; }
        
        public string? InputData { get; set; }
        
        public string? OutputData { get; set; }
    }
}