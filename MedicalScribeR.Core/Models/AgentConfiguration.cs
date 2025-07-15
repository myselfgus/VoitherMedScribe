using System;
using System.ComponentModel.DataAnnotations;

namespace MedicalScribeR.Core.Models
{
    /// <summary>
    /// Configura��o de um agente de IA
    /// </summary>
    public class AgentConfiguration : MedicalScribeR.Core.Interfaces.IAgentConfig
    {
        [Key]
        public string AgentName { get; set; } = string.Empty;
        
        public bool IsEnabled { get; set; }
        
        public double ConfidenceThreshold { get; set; }
        public System.Collections.Generic.List<string> RequiredEntities { get; set; } = new System.Collections.Generic.List<string>();
        
        public string TriggeringIntentions { get; set; } = string.Empty; // CSV de inten��es
        
        public string? Prompt { get; set; }
        
        public DateTime LastUpdated { get; set; }
        
        public string? Configuration { get; set; } // JSON com configura��es espec�ficas
        
        public int Priority { get; set; } = 0; // Ordem de execu��o
        
        public bool IsAsync { get; set; } = true; // Execu��o ass�ncrona
    }
}