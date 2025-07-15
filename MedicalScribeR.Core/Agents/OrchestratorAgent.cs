using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MedicalScribeR.Core.Interfaces;
using MedicalScribeR.Core.Models;
using MedicalScribeR.Core.Configuration;

namespace MedicalScribeR.Core.Agents
{
    public class OrchestratorAgent
    {
        private readonly IAzureAIService _aiService;
        private readonly IEnumerable<ISpecializedAgent> _availableAgents;
        private readonly AgentConfigLoader _configLoader;
        private readonly ITranscriptionRepository _repository;
        private readonly ILogger<OrchestratorAgent> _logger;

        public OrchestratorAgent(
            IAzureAIService aiService,
            IEnumerable<ISpecializedAgent> availableAgents,
            AgentConfigLoader configLoader,
            ITranscriptionRepository repository,
            ILogger<OrchestratorAgent> logger)
        {
            _aiService = aiService;
            _availableAgents = availableAgents;
            _configLoader = configLoader;
            _repository = repository;
            _logger = logger;
        }

        public async Task<AgentResponse> ProcessTranscriptionChunk(string sessionId, TranscriptionChunk chunk)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Salva o chunk no banco
                chunk.SessionId = sessionId;
                await _repository.SaveChunkAsync(chunk);

                // Atualiza estatísticas da sessão
                await UpdateSessionStats(sessionId, (double)chunk.Confidence);

                // 1. Analisar o texto para extrair entidades e intenções
                var healthEntities = await _aiService.ExtractMedicalEntitiesAsync(chunk.Text);
                var intentions = await _aiService.ClassifyIntentionsAsync(chunk, healthEntities);

                var agentConfigs = _configLoader.LoadConfig(AppDomain.CurrentDomain.BaseDirectory);
                var agentContextsToProcess = new List<(ISpecializedAgent Agent, AgentProcessingContext Context)>();

                // 2. Determinar quais agentes devem ser ativados
                foreach (var agent in _availableAgents)
                {
                    var config = agentConfigs.FirstOrDefault(c => c.Value.AgentName == agent.Name);
                    if (config.Value == null || !config.Value.IsEnabled)
                    {
                        continue;
                    }

                    var context = new AgentProcessingContext(chunk, healthEntities, intentions, config.Value);

                    if (agent.ShouldActivate(context))
                    {
                        agentContextsToProcess.Add((agent, context));
                    }
                }

                if (!agentContextsToProcess.Any())
                {
                    await LogProcessing(sessionId, "Orchestrator", "Success", stopwatch.ElapsedMilliseconds, 
                        "Nenhum agente ativado", 0, chunk.ChunkId != Guid.Empty ? (int?)chunk.ChunkId.GetHashCode() : null);
                    
                    return new AgentResponse { Message = "Nenhum agente especializado foi ativado para este trecho." };
                }

                // 3. Executar os agentes ativados em paralelo
                var agentTasks = agentContextsToProcess.Select(async pair =>
                {
                    var agentStopwatch = Stopwatch.StartNew();
                    try
                    {
                        var result = await pair.Agent.ProcessAsync(pair.Context);
                        
                        // Salva documentos gerados
                        foreach (var doc in result.Documents)
                        {
                            doc.SessionId = sessionId;
                            await _repository.SaveDocumentAsync(doc);
                        }

                        // Salva ações geradas
                        foreach (var action in result.Actions)
                        {
                            action.SessionId = sessionId;
                            await _repository.SaveActionAsync(action);
                        }

                        await LogProcessing(sessionId, pair.Agent.Name, "Success", 
                            agentStopwatch.ElapsedMilliseconds, null, result.Confidence, chunk.ChunkId != Guid.Empty ? (int?)chunk.ChunkId.GetHashCode() : null);
                        
                        return result;
                    }
                    catch (Exception ex)
                    {
                        await LogProcessing(sessionId, pair.Agent.Name, "Error", 
                            agentStopwatch.ElapsedMilliseconds, ex.Message, 0, chunk.ChunkId != Guid.Empty ? (int?)chunk.ChunkId.GetHashCode() : null);
                        
                        return new AgentResult 
                        { 
                            ErrorMessage = ex.Message,
                            Confidence = 0
                        };
                    }
                });

                var results = await Task.WhenAll(agentTasks);

                // 4. Agregar os resultados
                var response = new AgentResponse
                {
                    TriggeredAgents = agentContextsToProcess.Select(pair => pair.Agent.Name).ToList(),
                    GeneratedDocuments = results.SelectMany(r => r.Documents).ToList(),
                    Actions = results.SelectMany(r => r.Actions).ToList(),
                    ConfidenceScore = results.Any() ? results.Average(r => r.Confidence) : 0
                };

                await LogProcessing(sessionId, "Orchestrator", "Success", stopwatch.ElapsedMilliseconds, 
                    JsonSerializer.Serialize(response), response.ConfidenceScore, chunk.ChunkId != Guid.Empty ? (int?)chunk.ChunkId.GetHashCode() : null);

                return response;
            }
            catch (Exception ex)
            {
                await LogProcessing(sessionId, "Orchestrator", "Error", stopwatch.ElapsedMilliseconds, 
                    ex.Message, 0, chunk.ChunkId != Guid.Empty ? (int?)chunk.ChunkId.GetHashCode() : null);
                
                _logger.LogError(ex, "Erro ao processar chunk da sessão {SessionId}", sessionId);
                throw;
            }
        }

        private async Task UpdateSessionStats(string sessionId, double chunkConfidence)
        {
            var session = await _repository.GetSessionAsync(sessionId);
            if (session != null)
            {
                session.TotalChunks++;
                // Remove properties that don't exist in the model
                await _repository.UpdateSessionAsync(session);
            }
        }

        private async Task LogProcessing(string sessionId, string agentName, string status, 
            double processingTimeMs, string? message, double confidence, int? chunkId)
        {
            var log = new ProcessingLog
            {
                SessionId = sessionId,
                AgentName = agentName,
                Action = status,
                Duration = TimeSpan.FromMilliseconds(processingTimeMs),
                ErrorMessage = message,
                IsSuccess = status == "Success",
                Timestamp = DateTime.UtcNow,
                Details = confidence > 0 ? $"Confidence: {confidence}" : null
            };

            await _repository.SaveProcessingLogAsync(log);
        }
    }
}