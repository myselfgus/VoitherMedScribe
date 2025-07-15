using MedicalScribeR.Core.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MedicalScribeR.Core.Interfaces
{
    /// <summary>
    /// Encapsula todo o estado relevante para o processamento de um trecho de transcrição,
    /// simplificando a comunicação entre o orquestrador e os agentes.
    /// </summary>
    public class AgentProcessingContext
    {
        public TranscriptionChunk CurrentChunk { get; }
        public IReadOnlyList<HealthcareEntity> Entities { get; }
        public IntentionClassification Intentions { get; }
        public IAgentConfig Config { get; }

        public AgentProcessingContext(TranscriptionChunk chunk, IReadOnlyList<HealthcareEntity> entities, IntentionClassification intentions, IAgentConfig config)
        {
            CurrentChunk = chunk;
            Entities = entities;
            Intentions = intentions;
            Config = config;
        }
    }

    /// <summary>
    /// Define o contrato para um agente especializado que pode ser ativado
    /// para processar a transcrição e gerar resultados específicos.
    /// </summary>
    public interface ISpecializedAgent
    {
        /// <summary>
        /// Nome único do agente, correspondente ao nome no arquivo de configuração.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Determina se o agente deve ser ativado com base no contexto atual.
        /// A lógica de ativação (intenções, entidades, confiança) é encapsulada aqui.
        /// </summary>
        bool ShouldActivate(AgentProcessingContext context);

        /// <summary>
        /// Executa a lógica principal do agente para processar os dados e gerar resultados.
        /// </summary>
        Task<AgentResult> ProcessAsync(AgentProcessingContext context);
    }

    /// <summary>
    /// Carrega a configuração de um agente a partir de uma fonte externa (ex: JSON).
    /// </summary>
    

    /// <summary>
    /// Representa o resultado do processamento de um agente.
    /// </summary>
    public class AgentResult
    {
        public List<GeneratedDocument> Documents { get; set; } = new List<GeneratedDocument>();
        public List<ActionItem> Actions { get; set; } = new List<ActionItem>();
        public double Confidence { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
