namespace MedicalScribeR.Core.Models
{
    public class AgentResponse
    {
        public List<string> TriggeredAgents { get; set; } = new List<string>();
        public List<GeneratedDocument> GeneratedDocuments { get; set; } = new List<GeneratedDocument>();
        public List<ActionItem> Actions { get; set; } = new List<ActionItem>();
        public double ConfidenceScore { get; set; }
        public string? Message { get; set; }
    }
}