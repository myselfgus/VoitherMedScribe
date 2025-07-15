using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MedicalScribeR.Core.Interfaces;
using MedicalScribeR.Core.Models;

namespace MedicalScribeR.Core.Agents
{
    /// <summary>
    /// Um agente especializado em gerar resumos de trechos da consulta médica.
    /// </summary>
    public class SummaryAgent : ISpecializedAgent
    {
        private readonly IAzureAIService _aiService;

        public SummaryAgent(IAzureAIService aiService)
        {
            _aiService = aiService;
        }

        /// <summary>
        /// O nome deve corresponder exatamente ao nome no arquivo de configuração (ex: agents.json).
        /// </summary>
        public string Name => "SummaryAgent";

        /// <summary>
        /// Decide se o agente deve ser ativado com base no contexto.
        /// </summary>
        public bool ShouldActivate(AgentProcessingContext context)
        {
            // Lógica de ativação:
            // 1. O agente está habilitado na configuração?
            if (!context.Config.IsEnabled)
            {
                return false;
            }

            // 2. A confiança da intenção principal está acima do limiar definido na configuração?
            if (context.Intentions.TopIntent.Confidence < context.Config.ConfidenceThreshold)
            {
                return false;
            }

            // 3. A intenção detectada é uma das que disparam este agente, conforme a configuração?
            //    Ex: "Summarize", "Conclusion"
            return context.Config.TriggeringIntentions.Contains(context.Intentions.TopIntent.Category);
        }

        /// <summary>
        /// Executa a lógica para gerar um resumo usando o serviço de IA.
        /// </summary>
        public async Task<AgentResult> ProcessAsync(AgentProcessingContext context)
        {
            try
            {
                // Cria um prompt claro para o serviço de IA, solicitando um resumo.
                var prompt = $"Com base na seguinte transcrição de uma consulta médica, gere um resumo conciso em português no formato de parágrafo único:\n\n---\nTranscrição: \"{context.CurrentChunk.Text}\"\n---\n\nResumo:";

                // Chama o serviço de IA para gerar o conteúdo do documento.
                var summaryContent = await _aiService.GenerateTextAsync(prompt);

                if (string.IsNullOrWhiteSpace(summaryContent))
                {
                    return new AgentResult { ErrorMessage = "O serviço de IA não retornou um resumo.", Confidence = 0 };
                }

                // Cria o documento a ser retornado.
                var summaryDocument = new GeneratedDocument
                {
                    Type = "Resumo da Consulta",
                    Content = summaryContent.Trim(),
                    GeneratedBy = Name
                };

                return new AgentResult
                {
                    Documents = new List<GeneratedDocument> { summaryDocument },
                    Confidence = 1.0 // A confiança aqui pode ser baseada na resposta da IA, se disponível.
                };
            }
            catch (Exception ex)
            {
                // Em um cenário real, é crucial logar os detalhes do erro.
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