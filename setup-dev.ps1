# Script de inicializa��o para desenvolvimento local - MedicalScribeR (Windows)
# Execute este script no PowerShell para configurar o ambiente de desenvolvimento

param(
    [switch]$SkipDocker = $false,
    [switch]$SkipTests = $false
)

Write-Host "?? Inicializando ambiente de desenvolvimento MedicalScribeR" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green

# Verificar se .NET 9 est� instalado
try {
    $dotnetVersion = dotnet --version 2>$null
    if (-not $dotnetVersion) {
        throw "dotnet n�o encontrado"
    }
    Write-Host "? .NET SDK vers�o: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "? .NET SDK n�o encontrado. Instale .NET 9 SDK em: https://dotnet.microsoft.com/download/dotnet/9.0" -ForegroundColor Red
    exit 1
}

# Verificar se Docker est� instalado e rodando (se n�o for para pular)
if (-not $SkipDocker) {
    try {
        $dockerVersion = docker --version 2>$null
        if ($dockerVersion) {
            docker info 2>$null | Out-Null
            if ($LASTEXITCODE -eq 0) {
                Write-Host "? Docker est� rodando" -ForegroundColor Green
            } else {
                Write-Host "?? Docker n�o est� rodando. Inicie Docker Desktop." -ForegroundColor Yellow
            }
        } else {
            Write-Host "?? Docker n�o encontrado. Instale Docker Desktop para usar containers locais." -ForegroundColor Yellow
        }
    } catch {
        Write-Host "?? Erro ao verificar Docker" -ForegroundColor Yellow
    }
}

# Verificar se o arquivo de configura��o existe
if (-not (Test-Path "MedicalScribeR.Web\appsettings.Development.json")) {
    Write-Host "?? Criando arquivo de configura��o de desenvolvimento..." -ForegroundColor Yellow
    Copy-Item "appsettings.Development.json.template" "MedicalScribeR.Web\appsettings.Development.json"
    Write-Host "?? Configure suas chaves Azure em MedicalScribeR.Web\appsettings.Development.json" -ForegroundColor Yellow
}

# Restaurar depend�ncias
Write-Host "?? Restaurando depend�ncias NuGet..." -ForegroundColor Yellow
dotnet restore

if ($LASTEXITCODE -eq 0) {
    Write-Host "? Depend�ncias restauradas com sucesso" -ForegroundColor Green
} else {
    Write-Host "? Erro ao restaurar depend�ncias" -ForegroundColor Red
    exit 1
}

# Build da solu��o
Write-Host "?? Compilando solu��o..." -ForegroundColor Yellow
dotnet build --configuration Debug

if ($LASTEXITCODE -eq 0) {
    Write-Host "? Compila��o bem-sucedida" -ForegroundColor Green
} else {
    Write-Host "? Erro na compila��o" -ForegroundColor Red
    exit 1
}

# Verificar se Entity Framework tools est�o instalados
$efTools = dotnet tool list -g | Select-String "dotnet-ef"
if (-not $efTools) {
    Write-Host "?? Instalando Entity Framework tools..." -ForegroundColor Yellow
    dotnet tool install --global dotnet-ef
}

# Configurar banco de dados
Write-Host "??? Configurando banco de dados..." -ForegroundColor Yellow
Set-Location "MedicalScribeR.Web"

# Verificar se h� migra��es pendentes
if (-not (Test-Path "Migrations")) {
    Write-Host "?? Criando migra��o inicial..." -ForegroundColor Yellow
    dotnet ef migrations add InitialCreate
}

# Aplicar migra��es
Write-Host "?? Aplicando migra��es ao banco de dados..." -ForegroundColor Yellow
dotnet ef database update

if ($LASTEXITCODE -eq 0) {
    Write-Host "? Banco de dados configurado com sucesso" -ForegroundColor Green
} else {
    Write-Host "? Erro ao configurar banco de dados" -ForegroundColor Red
    Set-Location ".."
    exit 1
}

Set-Location ".."

# Executar testes (se n�o for para pular)
if (-not $SkipTests) {
    Write-Host "?? Executando testes..." -ForegroundColor Yellow
    dotnet test --verbosity minimal

    if ($LASTEXITCODE -eq 0) {
        Write-Host "? Todos os testes passaram" -ForegroundColor Green
    } else {
        Write-Host "?? Alguns testes falharam - verifique a sa�da acima" -ForegroundColor Yellow
    }
}

# Informa��es finais
Write-Host ""
Write-Host "?? Ambiente de desenvolvimento configurado com sucesso!" -ForegroundColor Green
Write-Host "======================================================" -ForegroundColor Green
Write-Host ""
Write-Host "?? Para executar a aplica��o:" -ForegroundColor Cyan
Write-Host "   cd MedicalScribeR.Web" -ForegroundColor White
Write-Host "   dotnet run" -ForegroundColor White
Write-Host ""
Write-Host "?? A aplica��o estar� dispon�vel em:" -ForegroundColor Cyan
Write-Host "   https://localhost:7001" -ForegroundColor White
Write-Host "   http://localhost:5000" -ForegroundColor White
Write-Host ""
Write-Host "?? Para executar com Docker:" -ForegroundColor Cyan
Write-Host "   docker-compose up -d" -ForegroundColor White
Write-Host ""
Write-Host "?? Comandos �teis:" -ForegroundColor Cyan
Write-Host "   dotnet watch run                  # Executar com hot reload" -ForegroundColor White
Write-Host "   dotnet test --watch              # Executar testes com watch" -ForegroundColor White
Write-Host "   dotnet ef migrations add [name]  # Criar nova migra��o" -ForegroundColor White
Write-Host "   dotnet ef database update        # Aplicar migra��es" -ForegroundColor White
Write-Host ""
Write-Host "?? Documenta��o:" -ForegroundColor Cyan
Write-Host "   README.md                        # Documenta��o principal" -ForegroundColor White
Write-Host "   docs\                           # Documenta��o t�cnica" -ForegroundColor White
Write-Host ""
Write-Host "?? Configura��o:" -ForegroundColor Cyan
Write-Host "   Configure suas chaves Azure em: MedicalScribeR.Web\appsettings.Development.json" -ForegroundColor White
Write-Host ""
Write-Host "?? Bom desenvolvimento!" -ForegroundColor Green