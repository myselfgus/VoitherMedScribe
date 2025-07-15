using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MedicalScribeR.Core.Interfaces;
using MedicalScribeR.Core.Models;

namespace MedicalScribeR.Core.Agents
{
    /// <summary>
    /// Agente especializado em gerar prescrições médicas a partir da transcrição.
    /// </summary>
    public class PrescriptionAgent : ISpecializedAgent
    {
        private readonly IAzureAIService _aiService;

        public PrescriptionAgent(IAzureAIService aiService)
        {
            _aiService = aiService;
        }

        /// <summary>
        /// O nome deve corresponder exatamente ao nome no arquivo de configuração (agents.json).
        /// </summary>
        public string Name => "PrescriptionAgent";

        /// <summary>
        /// Decide se o agente deve ser ativado.
        /// </summary>
        public bool ShouldActivate(AgentProcessingContext context)
        {
            if (!context.Config.IsEnabled)
            {
                return false;
            }

            // Ativa se a intenção de prescrever for alta o suficiente
            bool hasPrescriptionIntent = context.Config.TriggeringIntentions.Contains(context.Intentions.TopIntent.Category) &&
                                         context.Intentions.TopIntent.Confidence >= context.Config.ConfidenceThreshold;

            // Ativa se encontrar entidades de medicação, mesmo com intenção baixa
            bool hasMedicationEntity = context.Entities.Any(e => context.Config.RequiredEntities.Contains(e.Category));

            return hasPrescriptionIntent || hasMedicationEntity;
        }

        /// <summary>
        /// Processa a transcrição para gerar um documento de prescrição.
        /// </summary>
        public async Task<AgentResult> ProcessAsync(AgentProcessingContext context)
        {
            try
            {
                var medicationEntities = context.Entities
                    .Where(e => e.Category == "MedicationName" || e.Category == "Dosage" || e.Category == "Frequency")
                    .Select(e => $"{e.Category}: {e.Text}");

                var prompt = $@"
                    Com base na seguinte transcrição de uma consulta médica e nas entidades extraídas, gere uma prescrição médica formal em português.
                    A prescrição deve ser clara, concisa e seguir o formato padrão (Nome do Medicamento, Dosagem, Instruções de Uso).
                    Se alguma informação estiver faltando, indique com ""[INFORMAÇÃO FALTANTE]"".

                    ---
                    Transcrição: ""{context.CurrentChunk.Text}""
                    Entidades Relevantes: {string.Join(", ", medicationEntities)}
                    ---

                    Prescrição Formatada:";

                var prescriptionContent = await _aiService.GenerateTextAsync(prompt);

                if (string.IsNullOrWhiteSpace(prescriptionContent))
                {
                    return new AgentResult { ErrorMessage = "O serviço de IA não retornou conteúdo para a prescrição.", Confidence = 0 };
                }

                var prescriptionDocument = new GeneratedDocument
                {
                    Type = "Prescrição Médica",
                    Content = prescriptionContent.Trim(),
                    GeneratedBy = Name
                };

                return new AgentResult
                {
                    Documents = new List<GeneratedDocument> { prescriptionDocument },
                    Confidence = context.Intentions.TopIntent.Confidence
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Falha no {Name}: {ex.Message}");
                return new AgentResult
                {
                    ErrorMessage = $"Erro ao processar o {Name}.",
                    Confidence = 0
                };
            }
        }
    }
}
