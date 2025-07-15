using Xunit;
using FluentAssertions;
using Moq;
using MedicalScribeR.Core.Agents;
using MedicalScribeR.Core.Interfaces;
using MedicalScribeR.Core.Models;
using System.Collections.Generic;

namespace MedicalScribeR.Tests.Agents
{
    public class SummaryAgentTests
    {
        private readonly Mock<IAzureAIService> _mockAIService;
        private readonly SummaryAgent _summaryAgent;

        public SummaryAgentTests()
        {
            _mockAIService = new Mock<IAzureAIService>();
            _summaryAgent = new SummaryAgent(_mockAIService.Object);
        }

        [Fact]
        public void Name_ShouldReturnCorrectAgentName()
        {
            // Act
            var name = _summaryAgent.Name;

            // Assert
            name.Should().Be("SummaryAgent");
        }

        [Fact]
        public void ShouldActivate_WhenConfigIsDisabled_ShouldReturnFalse()
        {
            // Arrange
            var chunk = new TranscriptionChunk { Text = "Test text" };
            var entities = new List<HealthcareEntity>();
            var intentions = new IntentionClassification { TopIntent = new IntentionCategory { Category = "General", Confidence = 0.9 } };
            var config = new AgentConfig { IsEnabled = false };
            
            var context = new AgentProcessingContext(chunk, entities, intentions, config);

            // Act
            var result = _summaryAgent.ShouldActivate(context);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task ProcessAsync_WithValidInput_ShouldReturnSummaryDocument()
        {
            // Arrange
            var chunk = new TranscriptionChunk { Text = "Paciente apresenta dor de cabeça" };
            var entities = new List<HealthcareEntity>();
            var intentions = new IntentionClassification { TopIntent = new IntentionCategory { Category = "General", Confidence = 0.9 } };
            var config = new AgentConfig { IsEnabled = true };
            
            var context = new AgentProcessingContext(chunk, entities, intentions, config);

            _mockAIService.Setup(x => x.GenerateTextAsync(It.IsAny<string>()))
                         .ReturnsAsync("Resumo: Paciente com cefaleia");

            // Act
            var result = await _summaryAgent.ProcessAsync(context);

            // Assert
            result.Should().NotBeNull();
            result.Documents.Should().HaveCount(1);
            result.Documents.First().Type.Should().Be("Resumo da Consulta");
            result.Documents.First().Content.Should().Be("Resumo: Paciente com cefaleia");
        }
    }
}