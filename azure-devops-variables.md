# Configura��o de Vari�veis para Azure DevOps - MedicalScribeR
# Este arquivo cont�m as vari�veis que devem ser configuradas no Azure DevOps

# =============================================================================
# VARI�VEIS DE BIBLIOTECA (Library Variables)
# Configure estas vari�veis em Azure DevOps > Pipelines > Library
# =============================================================================

# Grupo de Vari�veis: MedicalScribeR-Common
# ----------------------------------------------
azureSubscription: "Azure-Connection"  # Nome da service connection
resourceGroupName: "rg-medicalscriber"
webAppName: "medicalscriber"

# Grupo de Vari�veis: MedicalScribeR-Development
# -----------------------------------------------
DevConnectionString: "Server=tcp:medicalscriber-dev.database.windows.net,1433;Initial Catalog=medicalscriber-dev;Persist Security Info=False;User ID=your-username;Password=your-password;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
DevOpenAIEndpoint: "https://medicalscriber-dev-openai.openai.azure.com/"
DevOpenAIKey: "$(DevOpenAIKey-Secret)"  # Refer�ncia a vari�vel secreta
DevTextAnalyticsEndpoint: "https://medicalscriber-dev-textanalytics.cognitiveservices.azure.com/"
DevTextAnalyticsKey: "$(DevTextAnalyticsKey-Secret)"
DevSpeechKey: "$(DevSpeechKey-Secret)"
DevSpeechRegion: "brazilsouth"

# Grupo de Vari�veis: MedicalScribeR-Staging
# -------------------------------------------
StagingConnectionString: "Server=tcp:medicalscriber-staging.database.windows.net,1433;Initial Catalog=medicalscriber-staging;Persist Security Info=False;User ID=your-username;Password=your-password;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
StagingOpenAIEndpoint: "https://medicalscriber-staging-openai.openai.azure.com/"
StagingOpenAIKey: "$(StagingOpenAIKey-Secret)"
StagingTextAnalyticsEndpoint: "https://medicalscriber-staging-textanalytics.cognitiveservices.azure.com/"
StagingTextAnalyticsKey: "$(StagingTextAnalyticsKey-Secret)"
StagingSpeechKey: "$(StagingSpeechKey-Secret)"
StagingSpeechRegion: "brazilsouth"

# Grupo de Vari�veis: MedicalScribeR-Production
# ----------------------------------------------
ProductionConnectionString: "Server=tcp:medicalscriber-prod.database.windows.net,1433;Initial Catalog=medicalscriber-prod;Persist Security Info=False;User ID=your-username;Password=your-password;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
ProductionOpenAIEndpoint: "https://medicalscriber-prod-openai.openai.azure.com/"
ProductionOpenAIKey: "$(ProductionOpenAIKey-Secret)"
ProductionTextAnalyticsEndpoint: "https://medicalscriber-prod-textanalytics.cognitiveservices.azure.com/"
ProductionTextAnalyticsKey: "$(ProductionTextAnalyticsKey-Secret)"
ProductionSpeechKey: "$(ProductionSpeechKey-Secret)"
ProductionSpeechRegion: "brazilsouth"

# =============================================================================
# VARI�VEIS SECRETAS (Secret Variables)
# Configure estas como vari�veis secretas no Azure DevOps
# =============================================================================

# Development Secrets
# -------------------
DevOpenAIKey-Secret: "your-dev-openai-key"
DevTextAnalyticsKey-Secret: "your-dev-textanalytics-key"  
DevSpeechKey-Secret: "your-dev-speech-key"
DevDatabasePassword-Secret: "your-dev-database-password"

# Staging Secrets
# ---------------
StagingOpenAIKey-Secret: "your-staging-openai-key"
StagingTextAnalyticsKey-Secret: "your-staging-textanalytics-key"
StagingSpeechKey-Secret: "your-staging-speech-key"
StagingDatabasePassword-Secret: "your-staging-database-password"

# Production Secrets
# ------------------
ProductionOpenAIKey-Secret: "your-production-openai-key"
ProductionTextAnalyticsKey-Secret: "your-production-textanalytics-key"
ProductionSpeechKey-Secret: "your-production-speech-key"
ProductionDatabasePassword-Secret: "your-production-database-password"

# Azure AD Secrets
# ----------------
AzureAD-TenantId-Secret: "your-tenant-id"
AzureAD-ClientId-Secret: "your-client-id"
AzureAD-ClientSecret-Secret: "your-client-secret"

# =============================================================================
# COMANDOS PARA CONFIGURA��O VIA CLI
# Execute estes comandos para configurar as vari�veis via Azure CLI
# =============================================================================

# Definir organiza��o e projeto
# az devops configure --defaults organization=https://dev.azure.com/voither project=voither

# Criar grupos de vari�veis
# az pipelines variable-group create --name "MedicalScribeR-Common" --variables azureSubscription="Azure-Connection" resourceGroupName="rg-medicalscriber" webAppName="medicalscriber"

# Criar vari�veis secretas
# az pipelines variable-group variable create --group-id [ID] --name "DevOpenAIKey-Secret" --value "your-key" --secret true

# =============================================================================
# CONFIGURA��O DE SERVICE CONNECTIONS
# Configure estas service connections no Azure DevOps
# =============================================================================

# Azure Resource Manager Service Connection
# -----------------------------------------
# Nome: Azure-Connection
# Tipo: Azure Resource Manager
# Escopo: Subscription
# Subscription: sua-subscription-azure
# Resource Group: rg-medicalscriber

# =============================================================================
# CONFIGURA��O DE AMBIENTES
# Configure estes ambientes no Azure DevOps > Pipelines > Environments
# =============================================================================

# Ambientes a criar:
# - development (auto-deploy)
# - staging (requires approval)
# - production (requires approval + multiple approvers)

# =============================================================================
# CONFIGURA��O DE APROVA��ES
# =============================================================================

# Development: Nenhuma aprova��o necess�ria
# Staging: Aprova��o do Tech Lead
# Production: Aprova��o do Tech Lead + Product Owner

# =============================================================================
# EXEMPLOS DE COMANDOS AZURE CLI PARA CONFIGURA��O
# =============================================================================

<#
# Login e configura��o inicial
az login
az devops configure --defaults organization=https://dev.azure.com/voither project=voither

# Criar service connection (requer configura��o manual no portal)
# az devops service-endpoint azurerm create --azure-rm-service-principal-id [client-id] --azure-rm-subscription-id [subscription-id] --azure-rm-subscription-name [subscription-name] --azure-rm-tenant-id [tenant-id] --name "Azure-Connection"

# Criar grupos de vari�veis
az pipelines variable-group create --name "MedicalScribeR-Common" --description "Common variables for MedicalScribeR"
az pipelines variable-group create --name "MedicalScribeR-Development" --description "Development environment variables"
az pipelines variable-group create --name "MedicalScribeR-Staging" --description "Staging environment variables"  
az pipelines variable-group create --name "MedicalScribeR-Production" --description "Production environment variables"

# Adicionar vari�veis aos grupos (exemplo)
az pipelines variable-group variable create --group-id [common-group-id] --name "azureSubscription" --value "Azure-Connection"
az pipelines variable-group variable create --group-id [dev-group-id] --name "DevOpenAIKey" --value "your-dev-key" --secret true

# Criar ambientes
az pipelines environment create --name "development"
az pipelines environment create --name "staging"  
az pipelines environment create --name "production"
#>

# =============================================================================
# CHECKLIST DE CONFIGURA��O
# =============================================================================

<#
? 1. Service Connection 'Azure-Connection' criada
? 2. Grupos de vari�veis criados:
     ? MedicalScribeR-Common
     ? MedicalScribeR-Development  
     ? MedicalScribeR-Staging
     ? MedicalScribeR-Production
? 3. Vari�veis secretas configuradas
? 4. Ambientes criados (development, staging, production)
? 5. Aprova��es configuradas para staging e production
? 6. Pipeline conectado ao reposit�rio
? 7. Build executada com sucesso
? 8. Deploy para development funcionando
? 9. Monitoring e logging configurados
? 10. Documenta��o atualizada
#>