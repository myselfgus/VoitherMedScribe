using System;
using System.ComponentModel.DataAnnotations;

namespace MedicalScribeR.Core.Models
{
    /// <summary>
    /// Representa um chunk/trecho de transcri��o
    /// </summary>
    public class TranscriptionChunk
    {
        [Key]
        public Guid ChunkId { get; set; }
        
        // Propriedade de conveni�ncia para compatibilidade
        public Guid Id => ChunkId;
        
        [Required]
        public string SessionId { get; set; } = string.Empty;
        
        [Required]
        public string Text { get; set; } = string.Empty;
        
        public string? Speaker { get; set; }
        
        public decimal Confidence { get; set; }
        
        [Required]
        public DateTime Timestamp { get; set; }
        
        public int SequenceNumber { get; set; }
        
        public bool IsProcessed { get; set; }
        
        public string? Language { get; set; }
    }
}