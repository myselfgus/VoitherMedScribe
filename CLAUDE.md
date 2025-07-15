# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Development Commands

### Build and Run
```bash
# Restore dependencies
dotnet restore

# Build solution
dotnet build --configuration Release

# Run web application (development) - HTTP on port 5118
dotnet run --project MedicalScribeR.Web

# Run with HTTPS - ports 7238 (HTTPS) and 5118 (HTTP)
dotnet run --project MedicalScribeR.Web --launch-profile https

# Run with Entity Framework database update
dotnet ef database update --project MedicalScribeR.Web
```

### Testing
```bash
# Run all tests
dotnet test

# Run tests with code coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test categories
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration
```

### Docker Development
```bash
# Run full stack with Docker Compose
docker-compose up -d

# Build and run just the web application
docker build -t medicalscriber-web .
docker run -p 8080:8080 medicalscriber-web
```

### Database Management
```bash
# Add new migration
dotnet ef migrations add <MigrationName> --project MedicalScribeR.Infrastructure --startup-project MedicalScribeR.Web

# Update database to latest migration
dotnet ef database update --project MedicalScribeR.Web

# Generate SQL script for deployment
dotnet ef script --project MedicalScribeR.Web
```

## Architecture Overview

This is a .NET 9 medical transcription application using Clean Architecture with specialized AI agents for processing medical conversations.

### Core Architecture Layers

**MedicalScribeR.Core** - Domain layer containing:
- `Agents/` - Specialized AI agents (SummaryAgent, PrescriptionAgent, ActionItemAgent, OrchestratorAgent)
- `Models/` - Domain entities and value objects
- `Interfaces/` - Contracts and abstractions
- `Services/` - Core business services (AzureAIService, AzureMLService)
- `Configuration/` - Agent configuration management

**MedicalScribeR.Infrastructure** - Infrastructure layer containing:
- `Data/MedicalScribeDbContext.cs` - Entity Framework context
- `Repositories/` - Data access implementations
- `Services/` - Infrastructure services (PdfGenerationService)

**MedicalScribeR.Web** - Presentation layer containing:
- `Controllers/` - API controllers and web controllers
- `Hubs/` - SignalR hubs for real-time communication
- `Middleware/` - Custom middleware (exception handling, request logging)
- `wwwroot/` - Static web assets and frontend code

**MedicalScribeR.Tests** - Test project with unit and integration tests

### Key Components

**Agent System**: Multi-agent architecture where the `OrchestratorAgent` coordinates specialized agents:
- Agents are configured via `agents.json` with confidence thresholds and triggering intentions
- Each agent implements `ISpecializedAgent` and processes specific aspects of medical transcriptions
- Agents can generate documents and action items based on analyzed transcription content

**Real-time Processing**: Uses SignalR for real-time transcription updates with Azure SignalR Service in production

**Authentication**: Microsoft Entra ID (Azure AD) integration with role-based authorization for medical professionals

**Data Storage**: Entity Framework Core with SQL Server, Redis for caching, Azure blob storage for documents

## Technology Stack

- **.NET 9** - Primary framework
- **Entity Framework Core 9** - ORM
- **SignalR** - Real-time communication
- **Azure AI Services** - Speech, OpenAI, Text Analytics
- **Microsoft Identity** - Authentication
- **Docker** - Containerization
- **Azure DevOps** - CI/CD pipelines

## Configuration Requirements

The application requires several Azure service configurations in `appsettings.json`:

- `AzureAI` section with OpenAI endpoint, key, and deployment settings
- `AzureSpeech` section with Speech service key and region
- `ConnectionStrings` for database and Application Insights
- `AzureAd` section for authentication setup

Agent behavior is controlled via `MedicalScribeR.Core/Configuration/agents.json` where you can:
- Enable/disable specific agents
- Set confidence thresholds
- Configure required entities and triggering intentions

## Development Notes

- The solution uses clean architecture principles with clear separation of concerns
- Agent configuration is dynamic and loaded at runtime from JSON files
- The system supports both local development (with Docker Compose) and Azure cloud deployment
- Authentication policies are configured for different user types (doctors, nurses, admins)
- Rate limiting and CORS are configured differently for development vs production environments
- All agents inherit from `ISpecializedAgent` and follow a common processing pattern
- The `OrchestratorAgent` coordinates multiple specialized agents and aggregates their results
- SignalR is used for real-time transcription streaming with fallback to local SignalR in development

## Testing Approach

- Unit tests focus on individual agent logic and domain services
- Integration tests verify the full transcription processing pipeline
- The test project references the main projects and uses in-memory database for testing
- Tests are categorized for selective execution (Unit, Integration)