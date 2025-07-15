# MedicalScribeR - Sistema de Transcrição Médica Inteligente

![VOITHER MedicalScribe](https://raw.githubusercontent.com/voither/medicalscriber/main/docs/images/logo.png)

## 🏥 Visão Geral

MedicalScribeR é uma plataforma avançada de documentação médica que utiliza Inteligência Artificial para transcrever consultas em tempo real e gerar automaticamente documentos médicos estruturados.

### 🚀 Status do Projeto

[![Build Status](https://dev.azure.com/voither/voither/_apis/build/status/MedicalScribeR-CI?branchName=main)](https://dev.azure.com/voither/voither/_build/latest?definitionId=YourPipelineId&branchName=main)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=voither_medicalscriber&metric=alert_status)](https://sonarcloud.io/dashboard?id=voither_medicalscriber)
[![Azure DevOps coverage](https://img.shields.io/azure-devops/coverage/voither/voither/YourPipelineId)](https://dev.azure.com/voither/voither/_build/latest?definitionId=YourPipelineId&branchName=main)

## ✨ Funcionalidades Principais

- **🎤 Transcrição em Tempo Real**: Captura e transcreve conversas médicas usando Azure Speech Services
- **🤖 Agentes de IA Especializados**: Sistema multi-agente que processa diferentes aspectos da consulta
- **📋 Geração Automática de Documentos**: Criação de resumos, prescrições e relatórios
- **💻 Interface Web Moderna**: Interface responsiva e intuitiva para profissionais de saúde
- **🔐 Autenticação Microsoft**: Integração com Azure AD para segurança empresarial
- **💾 Armazenamento Seguro**: Persistência de dados com Entity Framework Core
- **📊 Dashboards e Relatórios**: Visualização de métricas e análises

## 🏗️ Arquitetura Técnica

### Stack Tecnológica

- **.NET 9**: Framework principal
- **ASP.NET Core Razor Pages**: Web framework
- **Entity Framework Core 9**: ORM para acesso a dados
- **SignalR**: Comunicação em tempo real
- **Azure AI Services**: Speech, OpenAI, Text Analytics
- **Microsoft Identity**: Autenticação e autorização
- **Docker**: Containerização
- **Azure DevOps**: CI/CD e gestão de projeto

### Estrutura da Solução
MedicalScribeR/
├── 📁 MedicalScribeR.Core/           # Domínio e lógica de negócio
│   ├── 🤖 Agents/                    # Agentes de IA especializados
│   ├── 🔌 Interfaces/                # Contratos e abstrações
│   ├── 📦 Models/                    # Modelos de domínio
│   └── ⚙️ Services/                  # Serviços centrais
├── 📁 MedicalScribeR.Infrastructure/ # Implementações de infraestrutura
│   ├── 🗄️ Data/                      # Contexto do banco de dados
│   ├── 📊 Repositories/              # Repositórios de dados
│   └── 🔧 Services/                  # Serviços de infraestrutura
├── 📁 MedicalScribeR.Web/           # Aplicação web
│   ├── 🎮 Controllers/              # Controllers da API
│   ├── 🔄 Hubs/                     # SignalR Hubs
│   ├── 👀 Views/                    # Views Razor
│   └── 🌐 wwwroot/                  # Recursos estáticos
└── 📁 MedicalScribeR.Tests/         # Testes automatizados
    ├── 🧪 Unit/                     # Testes unitários
    └── 🔗 Integration/              # Testes de integração
## 🚀 Início Rápido

### Pré-requisitos

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) ou [VS Code](https://code.visualstudio.com/)
- Conta Azure com serviços de IA habilitados

### 🐳 Executar com Docker
# Clone o repositório
git clone https://voither@dev.azure.com/voither/voither/_git/MedicalScribeR
cd MedicalScribeR

# Execute com Docker Compose
docker-compose up -d

# Acesse a aplicação
http://localhost:8080
### 🏃‍♂️ Executar Localmente
# Restaurar dependências
dotnet restore

# Configurar banco de dados
dotnet ef database update --project MedicalScribeR.Web

# Executar aplicação
dotnet run --project MedicalScribeR.Web
## ⚙️ Configuração

### Variáveis de Ambiente

Crie um arquivo `appsettings.Development.json`:
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=MedicalScribeR;Trusted_Connection=true;"
  },
  "AzureAI": {
    "OpenAIEndpoint": "https://your-openai.openai.azure.com/",
    "OpenAIKey": "your-openai-key",
    "TextAnalyticsEndpoint": "https://your-textanalytics.cognitiveservices.azure.com/",
    "TextAnalyticsKey": "your-textanalytics-key",
    "SpeechKey": "your-speech-key",
    "SpeechRegion": "brazilsouth"
  },
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id"
  }
}
## 🧪 Testes
# Executar todos os testes
dotnet test

# Testes com coverage
dotnet test --collect:"XPlat Code Coverage"

# Testes por categoria
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration
## 📦 Deploy

### Azure Web App

O deploy é automatizado via Azure DevOps Pipelines:

- **Develop** → Deploy automático para ambiente de desenvolvimento
- **Release branches** → Deploy para staging
- **Main** → Deploy para produção

### Ambientes

| Ambiente | URL | Branch | Status |
|----------|-----|---------|--------|
| Development | https://medicalscriber-dev.azurewebsites.net | develop | [![Dev Status](https://img.shields.io/website?url=https%3A//medicalscriber-dev.azurewebsites.net)](https://medicalscriber-dev.azurewebsites.net) |
| Staging | https://medicalscriber-staging.azurewebsites.net | release/* | [![Staging Status](https://img.shields.io/website?url=https%3A//medicalscriber-staging.azurewebsites.net)](https://medicalscriber-staging.azurewebsites.net) |
| Production | https://medicalscriber.azurewebsites.net | main | [![Production Status](https://img.shields.io/website?url=https%3A//medicalscriber.azurewebsites.net)](https://medicalscriber.azurewebsites.net) |

## 🤝 Contribuindo

### Workflow de Desenvolvimento

1. **Feature Branch**: Crie uma branch a partir de `develop`git checkout develop
git pull origin develop
git checkout -b feature/nova-funcionalidade
2. **Desenvolvimento**: Implemente a funcionalidade com testes# Faça suas alterações
dotnet test  # Certifique-se que os testes passam
3. **Pull Request**: Crie PR para `develop`
   - ✅ Todos os testes passando
   - ✅ Code review aprovado
   - ✅ Pipeline de CI passou

4. **Release**: Merge de `develop` para `main` via release branch

### Padrões de Código

- **C# Coding Standards**: Seguir [Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/inside-a-program/coding-conventions)
- **Commit Messages**: Usar [Conventional Commits](https://www.conventionalcommits.org/)
- **Testes**: Cobertura mínima de 80%

### Estrutura de Commits
feat: adiciona novo agente de diagnóstico
fix: corrige erro na transcrição em tempo real
docs: atualiza documentação da API
test: adiciona testes para PrescriptionAgent
## 🔒 Segurança

- **Autenticação**: Azure Active Directory
- **Autorização**: Role-based access control (RBAC)
- **Criptografia**: TLS 1.3 para dados em trânsito
- **Compliance**: LGPD, GDPR, HIPAA ready
- **Auditoria**: Logs detalhados de todas as operações

## 📊 Monitoramento

### Application Insights

- **Performance**: Tempo de resposta, throughput
- **Errors**: Tracking de exceções e falhas
- **Dependencies**: Monitoramento de serviços externos
- **Usage**: Analytics de uso da aplicação

### Health Checks

- Endpoint: `/health`
- Monitora: Database, Azure AI Services, SignalR

## 📚 Documentação

- [📖 Documentação da API](docs/api.md)
- [🏗️ Guia de Arquitetura](docs/architecture.md)
- [🔧 Guia de Deploy](docs/deployment.md)
- [🧪 Guia de Testes](docs/testing.md)
- [🔐 Guia de Segurança](docs/security.md)

## 📞 Suporte

- **Issues**: [Azure DevOps Work Items](https://dev.azure.com/voither/voither/_workitems/)
- **Wiki**: [Project Wiki](https://dev.azure.com/voither/voither/_wiki/)
- **Email**: medicalscriber@voither.com

## 📄 Licença

Este projeto é propriedade da **VOITHER** e está licenciado sob os termos definidos no contrato de licenciamento proprietário.

## 👥 Equipe

- **Tech Lead**: [Nome]
- **Arquiteto de Soluções**: [Nome]
- **Especialista em IA**: [Nome]
- **DevOps Engineer**: [Nome]

---

**🏥 MedicalScribeR** - Transformando a documentação médica com Inteligência Artificial

*Desenvolvido com ❤️ pela equipe VOITHER*
