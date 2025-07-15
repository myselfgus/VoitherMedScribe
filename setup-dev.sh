#!/bin/bash

# Script de inicializa��o para desenvolvimento local - MedicalScribeR
# Execute este script para configurar o ambiente de desenvolvimento

echo "?? Inicializando ambiente de desenvolvimento MedicalScribeR"
echo "=========================================================="

# Verificar se .NET 9 est� instalado
if ! command -v dotnet &> /dev/null; then
    echo "? .NET SDK n�o encontrado. Instale .NET 9 SDK em: https://dotnet.microsoft.com/download/dotnet/9.0"
    exit 1
fi

# Verificar vers�o do .NET
DOTNET_VERSION=$(dotnet --version)
echo "? .NET SDK vers�o: $DOTNET_VERSION"

# Verificar se Docker est� instalado e rodando
if ! command -v docker &> /dev/null; then
    echo "?? Docker n�o encontrado. Instale Docker Desktop para usar containers locais."
else
    if docker info &> /dev/null; then
        echo "? Docker est� rodando"
    else
        echo "?? Docker n�o est� rodando. Inicie Docker Desktop."
    fi
fi

# Verificar se o arquivo de configura��o existe
if [ ! -f "MedicalScribeR.Web/appsettings.Development.json" ]; then
    echo "?? Criando arquivo de configura��o de desenvolvimento..."
    cp appsettings.Development.json.template MedicalScribeR.Web/appsettings.Development.json
    echo "?? Configure suas chaves Azure em MedicalScribeR.Web/appsettings.Development.json"
fi

# Restaurar depend�ncias
echo "?? Restaurando depend�ncias NuGet..."
dotnet restore

if [ $? -eq 0 ]; then
    echo "? Depend�ncias restauradas com sucesso"
else
    echo "? Erro ao restaurar depend�ncias"
    exit 1
fi

# Build da solu��o
echo "?? Compilando solu��o..."
dotnet build --configuration Debug

if [ $? -eq 0 ]; then
    echo "? Compila��o bem-sucedida"
else
    echo "? Erro na compila��o"
    exit 1
fi

# Verificar se Entity Framework tools est�o instalados
if ! dotnet tool list -g | grep -q dotnet-ef; then
    echo "?? Instalando Entity Framework tools..."
    dotnet tool install --global dotnet-ef
fi

# Configurar banco de dados
echo "??? Configurando banco de dados..."
cd MedicalScribeR.Web

# Verificar se h� migra��es pendentes
if [ ! -d "Migrations" ]; then
    echo "?? Criando migra��o inicial..."
    dotnet ef migrations add InitialCreate
fi

# Aplicar migra��es
echo "?? Aplicando migra��es ao banco de dados..."
dotnet ef database update

if [ $? -eq 0 ]; then
    echo "? Banco de dados configurado com sucesso"
else
    echo "? Erro ao configurar banco de dados"
    exit 1
fi

cd ..

# Executar testes
echo "?? Executando testes..."
dotnet test --verbosity minimal

if [ $? -eq 0 ]; then
    echo "? Todos os testes passaram"
else
    echo "?? Alguns testes falharam - verifique a sa�da acima"
fi

# Informa��es finais
echo ""
echo "?? Ambiente de desenvolvimento configurado com sucesso!"
echo "======================================================"
echo ""
echo "?? Para executar a aplica��o:"
echo "   cd MedicalScribeR.Web"
echo "   dotnet run"
echo ""
echo "?? A aplica��o estar� dispon�vel em:"
echo "   https://localhost:7001"
echo "   http://localhost:5000"
echo ""
echo "?? Para executar com Docker:"
echo "   docker-compose up -d"
echo ""
echo "?? Comandos �teis:"
echo "   dotnet watch run                  # Executar com hot reload"
echo "   dotnet test --watch              # Executar testes com watch"
echo "   dotnet ef migrations add [name]  # Criar nova migra��o"
echo "   dotnet ef database update        # Aplicar migra��es"
echo ""
echo "?? Documenta��o:"
echo "   README.md                        # Documenta��o principal"
echo "   docs/                           # Documenta��o t�cnica"
echo ""
echo "?? Configura��o:"
echo "   Configure suas chaves Azure em: MedicalScribeR.Web/appsettings.Development.json"
echo ""
echo "?? Bom desenvolvimento!"