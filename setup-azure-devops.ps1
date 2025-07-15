# Script de configuração inicial para Azure DevOps - MedicalScribeR
# Execute este script para configurar o repositório Azure DevOps

param(
    [Parameter(Mandatory=$true)]
    [string]$OrganizationUrl = "https://dev.azure.com/voither",
    
    [Parameter(Mandatory=$true)]
    [string]$ProjectName = "voither",
    
    [Parameter(Mandatory=$true)]
    [string]$RepositoryName = "MedicalScribeR",
    
    [Parameter(Mandatory=$false)]
    [string]$LocalPath = "."
)

Write-Host "?? Configuração do Azure DevOps para MedicalScribeR" -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Green

# Verificar se o Azure CLI está instalado
try {
    $azVersion = az --version 2>$null
    if (-not $azVersion) {
        throw "Azure CLI não encontrado"
    }
    Write-Host "? Azure CLI encontrado" -ForegroundColor Green
} catch {
    Write-Host "? Azure CLI não está instalado. Instale em: https://docs.microsoft.com/cli/azure/" -ForegroundColor Red
    exit 1
}

# Verificar se a extensão Azure DevOps está instalada
try {
    $extensions = az extension list --query "[?name=='azure-devops'].name" --output tsv 2>$null
    if (-not $extensions -or $extensions -notcontains "azure-devops") {
        Write-Host "?? Instalando extensão Azure DevOps..." -ForegroundColor Yellow
        az extension add --name azure-devops
    }
    Write-Host "? Extensão Azure DevOps está disponível" -ForegroundColor Green
} catch {
    Write-Host "? Erro ao configurar extensão Azure DevOps" -ForegroundColor Red
    exit 1
}

# Login no Azure (se necessário)
Write-Host "?? Verificando autenticação..." -ForegroundColor Yellow
try {
    $account = az account show 2>$null | ConvertFrom-Json
    if (-not $account) {
        Write-Host "?? Fazendo login no Azure..." -ForegroundColor Yellow
        az login
    }
    Write-Host "? Autenticado como: $($account.user.name)" -ForegroundColor Green
} catch {
    Write-Host "? Erro na autenticação" -ForegroundColor Red
    exit 1
}

# Configurar organização padrão
Write-Host "?? Configurando organização Azure DevOps..." -ForegroundColor Yellow
az devops configure --defaults organization=$OrganizationUrl project=$ProjectName

# Verificar se o projeto existe
Write-Host "?? Verificando projeto '$ProjectName'..." -ForegroundColor Yellow
try {
    $project = az devops project show --project $ProjectName 2>$null | ConvertFrom-Json
    if ($project) {
        Write-Host "? Projeto '$ProjectName' encontrado" -ForegroundColor Green
    }
} catch {
    Write-Host "? Projeto '$ProjectName' não encontrado. Verifique o nome do projeto." -ForegroundColor Red
    exit 1
}

# Criar repositório
Write-Host "?? Criando repositório '$RepositoryName'..." -ForegroundColor Yellow
try {
    $existingRepo = az repos show --repository $RepositoryName 2>$null
    if ($existingRepo) {
        Write-Host "?? Repositório '$RepositoryName' já existe" -ForegroundColor Yellow
    } else {
        $repo = az repos create --name $RepositoryName --project $ProjectName | ConvertFrom-Json
        Write-Host "? Repositório '$RepositoryName' criado com sucesso" -ForegroundColor Green
        Write-Host "?? URL: $($repo.webUrl)" -ForegroundColor Cyan
    }
} catch {
    Write-Host "? Erro ao criar repositório" -ForegroundColor Red
    exit 1
}

# Configurar Git local
Write-Host "?? Configurando Git local..." -ForegroundColor Yellow
Set-Location $LocalPath

# Verificar se é um repositório Git
if (-not (Test-Path ".git")) {
    Write-Host "?? Inicializando repositório Git..." -ForegroundColor Yellow
    git init
    Write-Host "? Repositório Git inicializado" -ForegroundColor Green
}

# Configurar remote
$remoteUrl = "$OrganizationUrl/$ProjectName/_git/$RepositoryName"
Write-Host "?? Configurando remote 'origin'..." -ForegroundColor Yellow

try {
    $existingRemote = git remote get-url origin 2>$null
    if ($existingRemote) {
        git remote set-url origin $remoteUrl
        Write-Host "? Remote 'origin' atualizado" -ForegroundColor Green
    } else {
        git remote add origin $remoteUrl
        Write-Host "? Remote 'origin' adicionado" -ForegroundColor Green
    }
} catch {
    Write-Host "? Erro ao configurar remote" -ForegroundColor Red
}

# Adicionar arquivos ao Git
Write-Host "?? Adicionando arquivos ao Git..." -ForegroundColor Yellow
git add .
git commit -m "feat: initial commit with project structure and Azure DevOps configuration

- Add complete .NET 9 project structure
- Add Azure DevOps pipeline configuration
- Add Docker support with multi-stage build
- Add comprehensive documentation
- Add security and monitoring setup"

# Push inicial
Write-Host "?? Fazendo push inicial..." -ForegroundColor Yellow
try {
    git branch -M main
    git push -u origin main
    Write-Host "? Push inicial realizado com sucesso" -ForegroundColor Green
} catch {
    Write-Host "? Erro no push inicial. Verifique as credenciais." -ForegroundColor Red
    Write-Host "?? Dica: Configure suas credenciais Git com 'git config --global credential.helper manager-core'" -ForegroundColor Yellow
}

# Criar branch develop
Write-Host "?? Criando branch 'develop'..." -ForegroundColor Yellow
try {
    git checkout -b develop
    git push -u origin develop
    git checkout main
    Write-Host "? Branch 'develop' criada" -ForegroundColor Green
} catch {
    Write-Host "?? Aviso: Erro ao criar branch develop" -ForegroundColor Yellow
}

# Configurar pipeline
Write-Host "?? Configurando pipeline..." -ForegroundColor Yellow
try {
    $pipeline = az pipelines create --name "MedicalScribeR-CI" --description "CI/CD Pipeline for MedicalScribeR" --repository $RepositoryName --repository-type tfsgit --branch main --yml-path azure-pipelines.yml --project $ProjectName 2>$null | ConvertFrom-Json
    if ($pipeline) {
        Write-Host "? Pipeline criado com sucesso" -ForegroundColor Green
        Write-Host "?? URL: $($pipeline.url)" -ForegroundColor Cyan
    }
} catch {
    Write-Host "?? Aviso: Configure o pipeline manualmente no Azure DevOps" -ForegroundColor Yellow
}

# Informações finais
Write-Host ""
Write-Host "?? Configuração concluída!" -ForegroundColor Green
Write-Host "================================" -ForegroundColor Green
Write-Host ""
Write-Host "?? Repositório: $remoteUrl" -ForegroundColor Cyan
Write-Host "?? Pipeline: $OrganizationUrl/$ProjectName/_build" -ForegroundColor Cyan
Write-Host "?? Project: $OrganizationUrl/$ProjectName" -ForegroundColor Cyan
Write-Host ""
Write-Host "?? Próximos passos:" -ForegroundColor Yellow
Write-Host "  1. Configure as variáveis de ambiente no Azure DevOps" -ForegroundColor White
Write-Host "  2. Configure os service connections para Azure" -ForegroundColor White
Write-Host "  3. Configure os ambientes (development, staging, production)" -ForegroundColor White
Write-Host "  4. Configure as variáveis secretas (API keys, connection strings)" -ForegroundColor White
Write-Host ""
Write-Host "?? Documentação: $remoteUrl?path=/docs" -ForegroundColor Cyan
Write-Host ""
Write-Host "?? MedicalScribeR está pronto para desenvolvimento!" -ForegroundColor Green