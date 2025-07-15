namespace MedicalScribeR.Core.Interfaces
{
    public interface IAgentConfig
    {
        string AgentName { get; set; }
        bool IsEnabled { get; set; }
        double ConfidenceThreshold { get; set; }
        string TriggeringIntentions { get; set; }
        System.Collections.Generic.List<string> RequiredEntities { get; set; }
        string? Prompt { get; set; }
        System.DateTime LastUpdated { get; set; }
        string? Configuration { get; set; }
        int Priority { get; set; }
        bool IsAsync { get; set; }
    }
}