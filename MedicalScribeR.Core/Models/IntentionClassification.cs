namespace MedicalScribeR.Core.Models
{
    public class IntentionClassification
    {
        public IntentionCategory TopIntent { get; set; } = new IntentionCategory();
        
        public List<IntentionCategory> AllIntentions { get; set; } = new List<IntentionCategory>();
        
        public double OverallConfidence { get; set; }
        
        public DateTime ClassifiedAt { get; set; } = DateTime.UtcNow;
        
        public string? ProcessingNotes { get; set; }
        
        // Propriedades específicas para compatibilidade com código existente
        public double Prescrever => GetIntentionScore("Prescrever");
        public double Agendar => GetIntentionScore("Agendar");
        public double Diagnosticar => GetIntentionScore("Diagnosticar");
        public double Receitar => GetIntentionScore("Receitar");
        public double Encaminhar => GetIntentionScore("Encaminhar");
        
        private double GetIntentionScore(string intentionName)
        {
            return AllIntentions.FirstOrDefault(i => i.Category == intentionName)?.Confidence ?? 0.0;
        }
    }
    
    public class IntentionCategory
    {
        public string Category { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public string? Description { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }
}