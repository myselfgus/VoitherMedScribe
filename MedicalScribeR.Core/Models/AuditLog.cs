using System;
using System.ComponentModel.DataAnnotations;

namespace MedicalScribeR.Core.Models
{
    /// <summary>
    /// Log de auditoria para rastreamento de ações
    /// </summary>
    public class AuditLog
    {
        [Key]
        public Guid LogId { get; set; }
        
        [Required]
        public string UserId { get; set; } = string.Empty;
        
        [Required]
        public string Action { get; set; } = string.Empty;
        
        public string? EntityType { get; set; }
        
        public string? EntityId { get; set; }
        
        public string? Details { get; set; } // JSON com detalhes da ação
        
        [Required]
        public DateTime Timestamp { get; set; }
        
        public string? IpAddress { get; set; }
        
        public string? UserAgent { get; set; }
        
        public string? SessionId { get; set; }
    }
}