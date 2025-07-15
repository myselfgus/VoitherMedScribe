# Script PowerShell para criar infraestrutura Azure para MedicalScribeR
# Execute este script no Azure Cloud Shell ou PowerShell com Az Module

param(
    [Parameter(Mandatory=$true)]
    [string]$SubscriptionId = "2290fbe4-e0ae-46e4-9bdd-dd5f7b5397d5",
    
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroupName = "rg-medicalscribe",
    
    [Parameter(Mandatory=$false)]
    [string]$Location = "Brazil South",
    
    [Parameter(Mandatory=$false)]
    [string]$AppName = "medicalscriber"
)

Write-Host "🚀 Iniciando deploy da infraestrutura Azure para MedicalScribeR..." -ForegroundColor Green

# 1. Login e seleção da subscription
Write-Host "📋 Configurando subscription..." -ForegroundColor Yellow
Connect-AzAccount
Set-AzContext -SubscriptionId $SubscriptionId

# 2. Criar Resource Group
Write-Host "📁 Criando Resource Group..." -ForegroundColor Yellow
$rg = New-AzResourceGroup -Name $ResourceGroupName -Location $Location -Force
Write-Host "✅ Resource Group criado: $($rg.ResourceGroupName)" -ForegroundColor Green

# 3. Criar SQL Server e Database
Write-Host "🗄️ Criando SQL Server e Database..." -ForegroundColor Yellow
$sqlAdminUser = "medicaladmin"
$sqlAdminPassword = ConvertTo-SecureString "MedicalScribe2024!@#" -AsPlainText -Force
$sqlServerName = "$AppName-sql-$(Get-Random -Minimum 1000 -Maximum 9999)"
$databaseName = "medicalscriber-db"

$sqlServer = New-AzSqlServer -ResourceGroupName $ResourceGroupName `
    -ServerName $sqlServerName `
    -Location $Location `
    -SqlAdministratorCredentials (New-Object System.Management.Automation.PSCredential($sqlAdminUser, $sqlAdminPassword))

$database = New-AzSqlDatabase -ResourceGroupName $ResourceGroupName `
    -ServerName $sqlServerName `
    -DatabaseName $databaseName `
    -RequestedServiceObjectiveName "S0"

# Configurar firewall do SQL Server para permitir Azure Services
New-AzSqlServerFirewallRule -ResourceGroupName $ResourceGroupName `
    -ServerName $sqlServerName `
    -FirewallRuleName "AllowAzureServices" `
    -StartIpAddress "0.0.0.0" `
    -EndIpAddress "0.0.0.0"

Write-Host "✅ SQL Server criado: $sqlServerName" -ForegroundColor Green

# 4. Criar Redis Cache
Write-Host "⚡ Criando Redis Cache..." -ForegroundColor Yellow
$redisName = "$AppName-redis-$(Get-Random -Minimum 1000 -Maximum 9999)"
$redis = New-AzRedisCache -ResourceGroupName $ResourceGroupName `
    -Name $redisName `
    -Location $Location `
    -Sku "Basic" `
    -Size "C0"

Write-Host "✅ Redis Cache criado: $redisName" -ForegroundColor Green

# 5. Criar SignalR Service
Write-Host "📡 Criando SignalR Service..." -ForegroundColor Yellow
$signalRName = "$AppName-signalr-$(Get-Random -Minimum 1000 -Maximum 9999)"

# Usando Azure CLI para SignalR (mais confiável que PowerShell)
az signalr create --name $signalRName `
    --resource-group $ResourceGroupName `
    --location "Brazil South" `
    --sku Free_F1 `
    --unit-count 1 `
    --service-mode Default

Write-Host "✅ SignalR Service criado: $signalRName" -ForegroundColor Green

# 6. Criar Cognitive Services
Write-Host "🧠 Criando Cognitive Services..." -ForegroundColor Yellow

# Text Analytics
$textAnalyticsName = "$AppName-textanalytics"
$textAnalytics = New-AzCognitiveServicesAccount -ResourceGroupName $ResourceGroupName `
    -Name $textAnalyticsName `
    -Type "TextAnalytics" `
    -SkuName "S0" `
    -Location $Location

# Speech Service
$speechName = "$AppName-speech"
$speech = New-AzCognitiveServicesAccount -ResourceGroupName $ResourceGroupName `
    -Name $speechName `
    -Type "SpeechServices" `
    -SkuName "S0" `
    -Location $Location

Write-Host "✅ Cognitive Services criados" -ForegroundColor Green

# 7. Criar OpenAI Service
Write-Host "🤖 Criando OpenAI Service..." -ForegroundColor Yellow
$openAIName = "$AppName-openai"

# OpenAI via Azure CLI (não disponível em PowerShell ainda)
az cognitiveservices account create `
    --name $openAIName `
    --resource-group $ResourceGroupName `
    --location "East US" `
    --kind "OpenAI" `
    --sku "S0"

# Deploy GPT-4o model
az cognitiveservices account deployment create `
    --name $openAIName `
    --resource-group $ResourceGroupName `
    --deployment-name "gpt-4o" `
    --model-name "gpt-4o" `
    --model-version "2024-05-13" `
    --model-format "OpenAI" `
    --sku-capacity 10 `
    --sku-name "Standard"

Write-Host "✅ OpenAI Service criado: $openAIName" -ForegroundColor Green

# 8. Criar Application Insights
Write-Host "📊 Criando Application Insights..." -ForegroundColor Yellow
$appInsightsName = "$AppName-insights"
$appInsights = New-AzApplicationInsights -ResourceGroupName $ResourceGroupName `
    -Name $appInsightsName `
    -Location $Location

Write-Host "✅ Application Insights criado: $appInsightsName" -ForegroundColor Green

# 9. Criar App Service Plan e Web App
Write-Host "🌐 Criando App Service..." -ForegroundColor Yellow
$appServicePlanName = "$AppName-plan"
$webAppName = "$AppName-web-$(Get-Random -Minimum 1000 -Maximum 9999)"

$appServicePlan = New-AzAppServicePlan -ResourceGroupName $ResourceGroupName `
    -Name $appServicePlanName `
    -Location $Location `
    -Tier "Standard" `
    -WorkerSize "Small"

$webApp = New-AzWebApp -ResourceGroupName $ResourceGroupName `
    -Name $webAppName `
    -Location $Location `
    -AppServicePlan $appServicePlanName

Write-Host "✅ Web App criado: $webAppName" -ForegroundColor Green

# 10. Obter chaves e connection strings
Write-Host "🔑 Obtendo chaves e connection strings..." -ForegroundColor Yellow

# SQL Connection String
$sqlConnectionString = "Server=tcp:$($sqlServer.FullyQualifiedDomainName),1433;Initial Catalog=$databaseName;Persist Security Info=False;User ID=$sqlAdminUser;Password=MedicalScribe2024!@#;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

# Redis Connection String
$redisKeys = Get-AzRedisCacheKey -ResourceGroupName $ResourceGroupName -Name $redisName
$redisConnectionString = "$($redis.HostName):$($redis.SslPort),password=$($redisKeys.PrimaryKey),ssl=True,abortConnect=False"

# SignalR Connection String
$signalRKeys = az signalr key list --name $signalRName --resource-group $ResourceGroupName --query "primaryKey" -o tsv
$signalRConnectionString = "Endpoint=https://$signalRName.service.signalr.net;AccessKey=$signalRKeys;Version=1.0;"

# Cognitive Services Keys
$textAnalyticsKey = (Get-AzCognitiveServicesAccountKey -ResourceGroupName $ResourceGroupName -Name $textAnalyticsName).Key1
$speechKey = (Get-AzCognitiveServicesAccountKey -ResourceGroupName $ResourceGroupName -Name $speechName).Key1

# OpenAI Key
$openAIKey = az cognitiveservices account keys list --name $openAIName --resource-group $ResourceGroupName --query "key1" -o tsv

# Application Insights
$appInsightsConnectionString = $appInsights.ConnectionString

# 11. Configurar App Settings
Write-Host "⚙️ Configurando App Settings..." -ForegroundColor Yellow

$appSettings = @{
    "ConnectionStrings:DefaultConnection" = $sqlConnectionString
    "ConnectionStrings:ApplicationInsights" = $appInsightsConnectionString
    "Azure:OpenAI:Endpoint" = "https://$openAIName.openai.azure.com/"
    "Azure:OpenAI:ApiKey" = $openAIKey
    "Azure:OpenAI:DeploymentName" = "gpt-4o"
    "Azure:TextAnalytics:Endpoint" = "https://$textAnalyticsName.cognitiveservices.azure.com/"
    "Azure:TextAnalytics:ApiKey" = $textAnalyticsKey
    "Azure:Speech:Region" = "brazilsouth"
    "Azure:Speech:ApiKey" = $speechKey
    "Azure:SignalR:ConnectionString" = $signalRConnectionString
    "Azure:Redis:ConnectionString" = $redisConnectionString
    "APPLICATIONINSIGHTS_CONNECTION_STRING" = $appInsightsConnectionString
}

Set-AzWebApp -ResourceGroupName $ResourceGroupName -Name $webAppName -AppSettings $appSettings

Write-Host "✅ App Settings configurados" -ForegroundColor Green

# 12. Resultado final
Write-Host "`n🎉 INFRAESTRUTURA AZURE CRIADA COM SUCESSO!" -ForegroundColor Green
Write-Host "`n📋 RESUMO DOS RECURSOS CRIADOS:" -ForegroundColor Yellow
Write-Host "SQL Server: $sqlServerName" -ForegroundColor White
Write-Host "Database: $databaseName" -ForegroundColor White
Write-Host "Redis Cache: $redisName" -ForegroundColor White
Write-Host "SignalR Service: $signalRName" -ForegroundColor White
Write-Host "Text Analytics: $textAnalyticsName" -ForegroundColor White
Write-Host "Speech Service: $speechName" -ForegroundColor White
Write-Host "OpenAI Service: $openAIName" -ForegroundColor White
Write-Host "Application Insights: $appInsightsName" -ForegroundColor White
Write-Host "Web App: $webAppName" -ForegroundColor White
Write-Host "Web App URL: https://$webAppName.azurewebsites.net" -ForegroundColor Green

Write-Host "`n🔑 PRÓXIMOS PASSOS:" -ForegroundColor Yellow
Write-Host "1. Configure o Azure AD App Registration com a URL: https://$webAppName.azurewebsites.net" -ForegroundColor White
Write-Host "2. Atualize o appsettings.json com as configurações do Azure AD" -ForegroundColor White
Write-Host "3. Faça o deploy da aplicação para: $webAppName" -ForegroundColor White

# Salvar informações em arquivo
$outputInfo = @"
# MedicalScribeR - Informações da Infraestrutura Azure

## Connection Strings
- SQL: $sqlConnectionString
- Redis: $redisConnectionString
- SignalR: $signalRConnectionString
- Application Insights: $appInsightsConnectionString

## Service Endpoints
- OpenAI: https://$openAIName.openai.azure.com/
- Text Analytics: https://$textAnalyticsName.cognitiveservices.azure.com/
- Web App: https://$webAppName.azurewebsites.net

## Keys
- Text Analytics: $textAnalyticsKey
- Speech: $speechKey
- OpenAI: $openAIKey

## Configurar Azure AD
- Redirect URI: https://$webAppName.azurewebsites.net/signin-oidc
- Logout URI: https://$webAppName.azurewebsites.net/signout-callback-oidc
"@

$outputInfo | Out-File -FilePath "azure-infrastructure-info.txt" -Encoding UTF8
Write-Host "`n💾 Informações salvas em: azure-infrastructure-info.txt" -ForegroundColor Green