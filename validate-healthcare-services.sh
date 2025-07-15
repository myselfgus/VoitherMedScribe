#!/bin/bash

# Script de Validação dos Serviços de Healthcare
# Este script testa a conectividade e configuração de todos os serviços integrados

set -e

echo "🏥 Validando Integração dos Serviços de Healthcare"
echo "=================================================="

# Cores para output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Função para logging
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Verificar se Azure CLI está instalado
check_azure_cli() {
    log_info "Verificando Azure CLI..."
    if command -v az &> /dev/null; then
        log_success "Azure CLI encontrado: $(az version --query '."azure-cli"' -o tsv)"
    else
        log_error "Azure CLI não encontrado. Instale: https://docs.microsoft.com/cli/azure/install-azure-cli"
        exit 1
    fi
}

# Verificar autenticação Azure
check_azure_auth() {
    log_info "Verificando autenticação Azure..."
    if az account show &> /dev/null; then
        ACCOUNT_NAME=$(az account show --query 'name' -o tsv)
        log_success "Autenticado como: $ACCOUNT_NAME"
    else
        log_error "Não autenticado no Azure. Execute: az login"
        exit 1
    fi
}

# Verificar Key Vault e secrets
check_keyvault_secrets() {
    log_info "Verificando Key Vault e secrets..."
    
    VAULT_NAME="MedScriber"
    REQUIRED_SECRETS=(
        "Azure-HealthcareNLP-ApiKey"
        "Azure-HealthcareNLP-Backup-ApiKey"
        "Azure-HealthInsights-ApiKey"
        "Azure-Search-ApiKey"
        "Azure-HealthcareApis-ClientSecret"
    )
    
    # Verificar se Key Vault existe e é acessível
    if az keyvault show --name "$VAULT_NAME" &> /dev/null; then
        log_success "Key Vault '$VAULT_NAME' acessível"
    else
        log_error "Key Vault '$VAULT_NAME' não encontrado ou sem acesso"
        return 1
    fi
    
    # Verificar cada secret
    for secret in "${REQUIRED_SECRETS[@]}"; do
        if az keyvault secret show --vault-name "$VAULT_NAME" --name "$secret" &> /dev/null; then
            VALUE=$(az keyvault secret show --vault-name "$VAULT_NAME" --name "$secret" --query 'value' -o tsv)
            if [[ "$VALUE" == placeholder-* ]]; then
                log_warning "Secret '$secret' ainda contém placeholder - precisa ser atualizado"
            else
                log_success "Secret '$secret' configurado"
            fi
        else
            log_error "Secret '$secret' não encontrado"
        fi
    done
}

# Verificar conectividade com serviços Azure
check_service_connectivity() {
    log_info "Verificando conectividade com serviços Azure..."
    
    # Health Insights
    log_info "Testando Azure Health Insights..."
    HEALTH_INSIGHTS_ENDPOINT="https://insightshealth.cognitiveservices.azure.com"
    if curl -s --head "$HEALTH_INSIGHTS_ENDPOINT" | grep "200 OK" &> /dev/null; then
        log_success "Azure Health Insights acessível"
    else
        log_warning "Azure Health Insights pode não estar acessível"
    fi
    
    # Healthcare NLP Principal
    log_info "Testando Healthcare NLP Principal..."
    HEALTHCARE_NLP_ENDPOINT="https://healthcaranlp.cognitiveservices.azure.com"
    if curl -s --head "$HEALTHCARE_NLP_ENDPOINT" | grep "200 OK" &> /dev/null; then
        log_success "Healthcare NLP Principal acessível"
    else
        log_warning "Healthcare NLP Principal pode não estar acessível"
    fi
    
    # Healthcare NLP Backup
    log_info "Testando Healthcare NLP Backup..."
    HEALTHCARE_NLP_BACKUP="https://etherim.cognitiveservices.azure.com"
    if curl -s --head "$HEALTHCARE_NLP_BACKUP" | grep "200 OK" &> /dev/null; then
        log_success "Healthcare NLP Backup acessível"
    else
        log_warning "Healthcare NLP Backup pode não estar acessível"
    fi
    
    # Cognitive Search
    log_info "Testando Azure Cognitive Search..."
    SEARCH_ENDPOINT="https://healthcaranlp-asuxj7c6w3imp6w.search.windows.net"
    if curl -s --head "$SEARCH_ENDPOINT" | grep "200 OK" &> /dev/null; then
        log_success "Azure Cognitive Search acessível"
    else
        log_warning "Azure Cognitive Search pode não estar acessível"
    fi
}

# Verificar estrutura de arquivos do projeto
check_project_structure() {
    log_info "Verificando estrutura do projeto..."
    
    REQUIRED_FILES=(
        "MedicalScribeR.Core/Services/AzureHealthInsightsService.cs"
        "MedicalScribeR.Core/Services/AzureHealthcareNLPService.cs"
        "MedicalScribeR.Core/Services/AzureHealthcareApisService.cs"
        "MedicalScribeR.Core/Services/HealthcareAIPipelineService.cs"
        "MedicalScribeR.Core/Models/HealthcareModels.cs"
        "MedicalScribeR.Web/Controllers/HealthcareAIController.cs"
    )
    
    for file in "${REQUIRED_FILES[@]}"; do
        if [[ -f "$file" ]]; then
            log_success "Arquivo encontrado: $file"
        else
            log_error "Arquivo não encontrado: $file"
        fi
    done
}

# Verificar configuração no appsettings
check_app_configuration() {
    log_info "Verificando configuração da aplicação..."
    
    CONFIG_FILE="MedicalScribeR.Web/appsettings.json"
    if [[ -f "$CONFIG_FILE" ]]; then
        log_success "Arquivo de configuração encontrado"
        
        # Verificar seções de configuração
        if grep -q "HealthcareNLP" "$CONFIG_FILE"; then
            log_success "Configuração HealthcareNLP encontrada"
        else
            log_warning "Configuração HealthcareNLP pode estar faltando"
        fi
        
        if grep -q "HealthInsights" "$CONFIG_FILE"; then
            log_success "Configuração HealthInsights encontrada"
        else
            log_warning "Configuração HealthInsights pode estar faltando"
        fi
        
        if grep -q "HealthcareApis" "$CONFIG_FILE"; then
            log_success "Configuração HealthcareApis encontrada"
        else
            log_warning "Configuração HealthcareApis pode estar faltando"
        fi
    else
        log_error "Arquivo de configuração não encontrado: $CONFIG_FILE"
    fi
}

# Verificar dependências NuGet
check_nuget_dependencies() {
    log_info "Verificando dependências NuGet..."
    
    CORE_PROJECT="MedicalScribeR.Core/MedicalScribeR.Core.csproj"
    if [[ -f "$CORE_PROJECT" ]]; then
        # Verificar packages críticos
        if grep -q "Azure.AI.TextAnalytics" "$CORE_PROJECT"; then
            log_success "Azure.AI.TextAnalytics package encontrado"
        else
            log_warning "Azure.AI.TextAnalytics package pode estar faltando"
        fi
        
        if grep -q "Azure.Search.Documents" "$CORE_PROJECT"; then
            log_success "Azure.Search.Documents package encontrado"
        else
            log_warning "Azure.Search.Documents package pode estar faltando"
        fi
    else
        log_error "Arquivo de projeto Core não encontrado"
    fi
}

# Testar build do projeto
test_project_build() {
    log_info "Testando build do projeto..."
    
    if dotnet build --configuration Release --no-restore &> build.log; then
        log_success "Build executado com sucesso"
        rm -f build.log
    else
        log_error "Falha no build. Verifique build.log para detalhes"
        echo "Últimas linhas do log de build:"
        tail -10 build.log
    fi
}

# Função principal
main() {
    echo
    log_info "Iniciando validação completa..."
    echo
    
    check_azure_cli
    echo
    
    check_azure_auth
    echo
    
    check_keyvault_secrets
    echo
    
    check_service_connectivity
    echo
    
    check_project_structure
    echo
    
    check_app_configuration
    echo
    
    check_nuget_dependencies
    echo
    
    test_project_build
    echo
    
    echo "=================================================="
    log_info "Validação concluída!"
    echo
    log_info "Próximos passos:"
    echo "1. Atualize os secrets do Key Vault com as chaves reais"
    echo "2. Configure managed identity para produção"
    echo "3. Execute o deploy da aplicação"
    echo "4. Teste os endpoints da API Healthcare"
    echo
}

# Executar validação
main "$@"
