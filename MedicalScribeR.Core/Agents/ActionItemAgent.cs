using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MedicalScribeR.Core.Interfaces;
using MedicalScribeR.Core.Models;

namespace MedicalScribeR.Core.Agents
{
    /// <summary>
    /// Agente especializado em identificar e gerar itens de a��o a partir da transcri��o.
    /// </summary>
    public class ActionItemAgent : ISpecializedAgent
    {
        private readonly IAzureAIService _aiService;

        public ActionItemAgent(IAzureAIService aiService)
        {
            _aiService = aiService;
        }

        /// <summary>
        /// O nome deve corresponder exatamente ao nome no arquivo de configura��o (agents.json).
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

            // Ativa se detectar inten��es relacionadas a a��es futuras
            bool hasActionIntent = context.Config.TriggeringIntentions.Contains(context.Intentions.TopIntent.Category) &&
                                  context.Intentions.TopIntent.Confidence >= context.Config.ConfidenceThreshold;

            // Ativa se encontrar palavras-chave relacionadas a follow-up
            string text = context.CurrentChunk.Text.ToLowerInvariant();
            bool hasActionKeywords = text.Contains("retorno") || text.Contains("agendar") || 
                                    text.Contains("pr�xima") || text.Contains("examinar") ||
                                    text.Contains("solicitar") || text.Contains("encaminhar");

            return hasActionIntent || hasActionKeywords;
        }

        /// <summary>
        /// Processa a transcri��o para gerar itens de a��o.
        /// </summary>
        public async Task<AgentResult> ProcessAsync(AgentProcessingContext context)
        {
            try
            {
                var prompt = $@"
                    Com base na seguinte transcri��o de uma consulta m�dica, identifique e liste os itens de a��o necess�rios.
                    Cada item de a��o deve incluir: tipo (Follow-up, Exame, Referral, etc.), descri��o e prioridade.
                    
                    Texto da consulta: ""{context.CurrentChunk.Text}""
                    
                    Liste os itens de a��o de forma estruturada:";

                var actionItems = await _aiService.GenerateActionItemsAsync(context.CurrentChunk.Text);

                if (!actionItems.Any())
                {
                    return new AgentResult 
                    { 
                        ErrorMessage = "Nenhum item de a��o identificado.",
                        Confidence = 0.5 
                    };
                }

                // Adicionar propriedades necess�rias aos itens de a��o
                foreach (var action in actionItems)
                {
                    action.ActionId = Guid.NewGuid();
                    action.CreatedAt = DateTime.UtcNow;
                    action.Status = ActionItemStatus.Pending;
                    
                    // Definir t�tulo baseado na descri��o se n�o existir
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
