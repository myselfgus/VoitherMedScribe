using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MedicalScribeR.Core.Interfaces;
using MedicalScribeR.Core.Models;

namespace MedicalScribeR.Core.Agents
{
    /// <summary>
    /// Agente especializado em identificar e gerar itens de ação a partir da transcrição.
    /// </summary>
    public class ActionItemAgent : ISpecializedAgent
    {
        private readonly IAzureAIService _aiService;

        public ActionItemAgent(IAzureAIService aiService)
        {
            _aiService = aiService;
        }

        /// <summary>
        /// O nome deve corresponder exatamente ao nome no arquivo de configuração (agents.json).
        /// </summary>
        public string Name => "ActionItemAgent";

        /// <summary>
        /// Decide se o agente deve ser ativado.
        /// </summary>
        public bool ShouldActivate(AgentProcessingContext context)
        {
            if (!context.Config.IsEnabled)
            {
                return false;
            }

            // Ativa se detectar intenções relacionadas a ações futuras
            bool hasActionIntent = context.Config.TriggeringIntentions.Contains(context.Intentions.TopIntent.Category) &&
                                  context.Intentions.TopIntent.Confidence >= context.Config.ConfidenceThreshold;

            // Ativa se encontrar palavras-chave relacionadas a follow-up
            string text = context.CurrentChunk.Text.ToLowerInvariant();
            bool hasActionKeywords = text.Contains("retorno") || text.Contains("agendar") || 
                                    text.Contains("próxima") || text.Contains("examinar") ||
                                    text.Contains("solicitar") || text.Contains("encaminhar");

            return hasActionIntent || hasActionKeywords;
        }

        /// <summary>
        /// Processa a transcrição para gerar itens de ação.
        /// </summary>
        public async Task<AgentResult> ProcessAsync(AgentProcessingContext context)
        {
            try
            {
                var prompt = $@"
                    Com base na seguinte transcrição de uma consulta médica, identifique e liste os itens de ação necessários.
                    Cada item de ação deve incluir: tipo (Follow-up, Exame, Referral, etc.), descrição e prioridade.
                    
                    Texto da consulta: ""{context.CurrentChunk.Text}""
                    
                    Liste os itens de ação de forma estruturada:";

                var actionItems = await _aiService.GenerateActionItemsAsync(context.CurrentChunk.Text);

                if (!actionItems.Any())
                {
                    return new AgentResult 
                    { 
                        ErrorMessage = "Nenhum item de ação identificado.",
                        Confidence = 0.5 
                    };
                }

                // Adicionar propriedades necessárias aos itens de ação
                foreach (var action in actionItems)
                {
                    action.ActionId = Guid.NewGuid();
                    action.CreatedAt = DateTime.UtcNow;
                    action.Status = ActionItemStatus.Pending;
                    
                    // Definir título baseado na descrição se não existir
                    if (string.IsNullOrEmpty(action.Title))
                    {
                        action.Title = action.Description.Length > 50 
                            ? action.Description.Substring(0, 47) + "..."
                            : action.Description;
                    }
                }

                var result = new AgentResult
                {
                    Actions = actionItems.ToList(),
                    Confidence = 0.85
                };

                return result;
            }
            catch (Exception ex)
            {
                return new AgentResult
                {
                    ErrorMessage = $"Erro no ActionItemAgent: {ex.Message}",
                    Confidence = 0
                };
            }
        }
    }
}
