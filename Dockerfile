# Dockerfile para MedicalScribeR
# Multi-stage build para otimização de tamanho

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copiar arquivos de projeto para restore de dependências
COPY ["MedicalScribeR.Web/MedicalScribeR.Web.csproj", "MedicalScribeR.Web/"]
COPY ["MedicalScribeR.Core/MedicalScribeR.Core.csproj", "MedicalScribeR.Core/"]
COPY ["MedicalScribeR.Infrastructure/MedicalScribeR.Infrastructure.csproj", "MedicalScribeR.Infrastructure/"]

# Restore dependências
RUN dotnet restore "MedicalScribeR.Web/MedicalScribeR.Web.csproj"

# Copiar todo o código fonte
COPY . .

# Build da aplicação
WORKDIR "/src/MedicalScribeR.Web"
RUN dotnet build "MedicalScribeR.Web.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "MedicalScribeR.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Instalar dependências do sistema necessárias
RUN apt-get update && apt-get install -y \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Criar usuário não-root para segurança
RUN addgroup --gid 1001 --system appgroup && \
    adduser --uid 1001 --system --gid 1001 appuser

# Copiar arquivos publicados
COPY --from=publish /app/publish .

# Configurar permissões
RUN chown -R appuser:appgroup /app
USER appuser

# Configurar variáveis de ambiente
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_USE_POLLING_FILE_WATCHER=true

# Expor porta
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Ponto de entrada
ENTRYPOINT ["dotnet", "MedicalScribeR.Web.dll"]