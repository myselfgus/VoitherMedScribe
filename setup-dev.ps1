# Script de inicialização para desenvolvimento local - MedicalScribeR (Windows)
# Execute este script no PowerShell para configurar o ambiente de desenvolvimento

param(
    [switch]$SkipDocker = $false,
    [switch]$SkipTests = $false
)

Write-Host "?? Inicializando ambiente de desenvolvimento MedicalScribeR" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green

# Verificar se .NET 9 está instalado
try {
    $dotnetVersion = dotnet --version 2>$null
    if (-not $dotnetVersion) {
        throw "dotnet não encontrado"
    }
    Write-Host "? .NET SDK versão: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "? .NET SDK não encontrado. Instale .NET 9 SDK em: https://dotnet.microsoft.com/download/dotnet/9.0" -ForegroundColor Red
    exit 1
}

# Verificar se Docker está instalado e rodando (se não for para pular)
if (-not $SkipDocker) {
    try {
        $dockerVersion = docker --version 2>$null
        if ($dockerVersion) {
            docker info 2>$null | Out-Null
            if ($LASTEXITCODE -eq 0) {
                Write-Host "? Docker está rodando" -ForegroundColor Green
            } else {
                Write-Host "?? Docker não está rodando. Inicie Docker Desktop." -ForegroundColor Yellow
            }
        } else {
            Write-Host "?? Docker não encontrado. Instale Docker Desktop para usar containers locais." -ForegroundColor Yellow
        }
    } catch {
        Write-Host "?? Erro ao verificar Docker" -ForegroundColor Yellow
    }
}

# Verificar se o arquivo de configuração existe
if (-not (Test-Path "MedicalScribeR.Web\appsettings.Development.json")) {
    Write-Host "?? Criando arquivo de configuração de desenvolvimento..." -ForegroundColor Yellow
    Copy-Item "appsettings.Development.json.template" "MedicalScribeR.Web\appsettings.Development.json"
    Write-Host "?? Configure suas chaves Azure em MedicalScribeR.Web\appsettings.Development.json" -ForegroundColor Yellow
}

# Restaurar dependências
Write-Host "?? Restaurando dependências NuGet..." -ForegroundColor Yellow
dotnet restore

if ($LASTEXITCODE -eq 0) {
    Write-Host "? Dependências restauradas com sucesso" -ForegroundColor Green
} else {
    Write-Host "? Erro ao restaurar dependências" -ForegroundColor Red
    exit 1
}

# Build da solução
Write-Host "?? Compilando solução..." -ForegroundColor Yellow
dotnet build --configuration Debug

if ($LASTEXITCODE -eq 0) {
    Write-Host "? Compilação bem-sucedida" -ForegroundColor Green
} else {
    Write-Host "? Erro na compilação" -ForegroundColor Red
    exit 1
}

# Verificar se Entity Framework tools estão instalados
$efTools = dotnet tool list -g | Select-String "dotnet-ef"
if (-not $efTools) {
    Write-Host "?? Instalando Entity Framework tools..." -ForegroundColor Yellow
    dotnet tool install --global dotnet-ef
}

# Configurar banco de dados
Write-Host "??? Configurando banco de dados..." -ForegroundColor Yellow
Set-Location "MedicalScribeR.Web"

# Verificar se há migrações pendentes
if (-not (Test-Path "Migrations")) {
    Write-Host "?? Criando migração inicial..." -ForegroundColor Yellow
    dotnet ef migrations add InitialCreate
}

# Aplicar migrações
Write-Host "?? Aplicando migrações ao banco de dados..." -ForegroundColor Yellow
dotnet ef database update

if ($LASTEXITCODE -eq 0) {
    Write-Host "? Banco de dados configurado com sucesso" -ForegroundColor Green
} else {
    Write-Host "? Erro ao configurar banco de dados" -ForegroundColor Red
    Set-Location ".."
    exit 1
}

Set-Location ".."

# Executar testes (se não for para pular)
if (-not $SkipTests) {
    Write-Host "?? Executando testes..." -ForegroundColor Yellow
    dotnet test --verbosity minimal

    if ($LASTEXITCODE -eq 0) {
        Write-Host "? Todos os testes passaram" -ForegroundColor Green
    } else {
        Write-Host "?? Alguns testes falharam - verifique a saída acima" -ForegroundColor Yellow
    }
}

# Informações finais
Write-Host ""
Write-Host "?? Ambiente de desenvolvimento configurado com sucesso!" -ForegroundColor Green
Write-Host "======================================================" -ForegroundColor Green
Write-Host ""
Write-Host "?? Para executar a aplicação:" -ForegroundColor Cyan
Write-Host "   cd MedicalScribeR.Web" -ForegroundColor White
Write-Host "   dotnet run" -ForegroundColor White
Write-Host ""
Write-Host "?? A aplicação estará disponível em:" -ForegroundColor Cyan
Write-Host "   https://localhost:7001" -ForegroundColor White
Write-Host "   http://localhost:5000" -ForegroundColor White
Write-Host ""
Write-Host "?? Para executar com Docker:" -ForegroundColor Cyan
Write-Host "   docker-compose up -d" -ForegroundColor White
Write-Host ""
Write-Host "?? Comandos úteis:" -ForegroundColor Cyan
Write-Host "   dotnet watch run                  # Executar com hot reload" -ForegroundColor White
Write-Host "   dotnet test --watch              # Executar testes com watch" -ForegroundColor White
Write-Host "   dotnet ef migrations add [name]  # Criar nova migração" -ForegroundColor White
Write-Host "   dotnet ef database update        # Aplicar migrações" -ForegroundColor White
Write-Host ""
Write-Host "?? Documentação:" -ForegroundColor Cyan
Write-Host "   README.md                        # Documentação principal" -ForegroundColor White
Write-Host "   docs\                           # Documentação técnica" -ForegroundColor White
Write-Host ""
Write-Host "?? Configuração:" -ForegroundColor Cyan
Write-Host "   Configure suas chaves Azure em: MedicalScribeR.Web\appsettings.Development.json" -ForegroundColor White
Write-Host ""
Write-Host "?? Bom desenvolvimento!" -ForegroundColor Green