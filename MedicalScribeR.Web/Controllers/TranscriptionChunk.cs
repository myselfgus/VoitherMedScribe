using System.ComponentModel.DataAnnotations;

namespace Voitmed.Core.Models
{
    public class TranscriptionChunk
    {
        public int Id { get; set; }
        
        [Required]
        public string SessionId { get; set; } = string.Empty;
        
        [Required]
        public string Text { get; set; } = string.Empty;
        
        [StringLength(20)]
        public string Speaker { get; set; } = "Unknown";
        
        [StringLength(255)]
        public string PatientName { get; set; } = string.Empty;
        
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        public double Confidence { get; set; }
        
        public int SequenceNumber { get; set; }
        
        public bool IsProcessed { get; set; } = false;
    }
}