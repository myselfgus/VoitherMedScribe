#!/bin/bash

# Script de inicialização para desenvolvimento local - MedicalScribeR
# Execute este script para configurar o ambiente de desenvolvimento

echo "?? Inicializando ambiente de desenvolvimento MedicalScribeR"
echo "=========================================================="

# Verificar se .NET 9 está instalado
if ! command -v dotnet &> /dev/null; then
    echo "? .NET SDK não encontrado. Instale .NET 9 SDK em: https://dotnet.microsoft.com/download/dotnet/9.0"
    exit 1
fi

# Verificar versão do .NET
DOTNET_VERSION=$(dotnet --version)
echo "? .NET SDK versão: $DOTNET_VERSION"

# Verificar se Docker está instalado e rodando
if ! command -v docker &> /dev/null; then
    echo "?? Docker não encontrado. Instale Docker Desktop para usar containers locais."
else
    if docker info &> /dev/null; then
        echo "? Docker está rodando"
    else
        echo "?? Docker não está rodando. Inicie Docker Desktop."
    fi
fi

# Verificar se o arquivo de configuração existe
if [ ! -f "MedicalScribeR.Web/appsettings.Development.json" ]; then
    echo "?? Criando arquivo de configuração de desenvolvimento..."
    cp appsettings.Development.json.template MedicalScribeR.Web/appsettings.Development.json
    echo "?? Configure suas chaves Azure em MedicalScribeR.Web/appsettings.Development.json"
fi

# Restaurar dependências
echo "?? Restaurando dependências NuGet..."
dotnet restore

if [ $? -eq 0 ]; then
    echo "? Dependências restauradas com sucesso"
else
    echo "? Erro ao restaurar dependências"
    exit 1
fi

# Build da solução
echo "?? Compilando solução..."
dotnet build --configuration Debug

if [ $? -eq 0 ]; then
    echo "? Compilação bem-sucedida"
else
    echo "? Erro na compilação"
    exit 1
fi

# Verificar se Entity Framework tools estão instalados
if ! dotnet tool list -g | grep -q dotnet-ef; then
    echo "?? Instalando Entity Framework tools..."
    dotnet tool install --global dotnet-ef
fi

# Configurar banco de dados
echo "??? Configurando banco de dados..."
cd MedicalScribeR.Web

# Verificar se há migrações pendentes
if [ ! -d "Migrations" ]; then
    echo "?? Criando migração inicial..."
    dotnet ef migrations add InitialCreate
fi

# Aplicar migrações
echo "?? Aplicando migrações ao banco de dados..."
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
    echo "?? Alguns testes falharam - verifique a saída acima"
fi

# Informações finais
echo ""
echo "?? Ambiente de desenvolvimento configurado com sucesso!"
echo "======================================================"
echo ""
echo "?? Para executar a aplicação:"
echo "   cd MedicalScribeR.Web"
echo "   dotnet run"
echo ""
echo "?? A aplicação estará disponível em:"
echo "   https://localhost:7001"
echo "   http://localhost:5000"
echo ""
echo "?? Para executar com Docker:"
echo "   docker-compose up -d"
echo ""
echo "?? Comandos úteis:"
echo "   dotnet watch run                  # Executar com hot reload"
echo "   dotnet test --watch              # Executar testes com watch"
echo "   dotnet ef migrations add [name]  # Criar nova migração"
echo "   dotnet ef database update        # Aplicar migrações"
echo ""
echo "?? Documentação:"
echo "   README.md                        # Documentação principal"
echo "   docs/                           # Documentação técnica"
echo ""
echo "?? Configuração:"
echo "   Configure suas chaves Azure em: MedicalScribeR.Web/appsettings.Development.json"
echo ""
echo "?? Bom desenvolvimento!"