using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MedicalScribeR.Web;

namespace MedicalScribeR.Tests.Integration
{
    public class HealthCheckTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory = factory;

        [Fact]
        public async Task HealthCheck_ShouldReturnHealthy()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync("/health");

            // Assert
            response.IsSuccessStatusCode.Should().BeTrue();
        }
    }
}