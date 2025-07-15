# 🔐 Gerenciamento de Chaves e Secrets

## Opções para Gerenciar Secrets (do mais simples ao mais seguro):

### 1. **Arquivo .env Local** (Desenvolvimento) ⭐ RECOMENDADO PARA INÍCIO

```bash
# Copie o arquivo exemplo
cp .env.example .env

# Edite com suas chaves reais
nano .env
```

**Vantagens:**
- ✅ Simples de usar
- ✅ Não vai para o Git (já está no .gitignore)
- ✅ Funciona imediatamente

### 2. **User Secrets** (Desenvolvimento .NET)

```bash
dotnet user-secrets set "AzureAd:ClientSecret" "sua_chave_real"
dotnet user-secrets set "AzureOpenAI:ApiKey" "sua_chave_real"
```

**Vantagens:**
- ✅ Específico para .NET
- ✅ Criptografado no sistema
- ✅ Não vai para o Git

### 3. **GitHub Secrets** (CI/CD)

1. Vá para seu repositório no GitHub
2. Settings > Secrets and variables > Actions
3. Adicione suas chaves como secrets
4. Use em workflows: `${{ secrets.AZURE_OPENAI_API_KEY }}`

### 4. **Azure DevOps Variable Groups** (CI/CD)

1. Azure DevOps > Pipelines > Library
2. Crie um Variable Group
3. Adicione variáveis como "secret"
4. Link com Azure Key Vault (mais seguro)

### 5. **Azure Key Vault** (Produção) 🛡️ MAIS SEGURO

```bash
# Conectar com Key Vault
az keyvault secret set --vault-name "MeuKeyVault" --name "OpenAIKey" --value "sua_chave"
```

## Como o Sistema Funciona Agora:

1. **Ordem de Prioridade:**
   - ♻️ Arquivo `.env` (se existir)
   - 🔒 User Secrets
   - ⚙️ appsettings.json (com placeholders)
   - 🌍 Variáveis de ambiente

2. **Para Desenvolvimento:**
   ```bash
   # Opção 1: Use .env
   cp .env.example .env
   # Edite o .env com suas chaves
   
   # Opção 2: Use user secrets
   dotnet user-secrets set "AzureOpenAI:ApiKey" "sua_chave"
   ```

3. **Para Produção:**
   - Use Azure Key Vault
   - Ou variáveis de ambiente do Azure App Service
   - Ou Azure DevOps Variable Groups

## Scripts Úteis:

```bash
# Restaurar todas as dependências
dotnet restore

# Rodar o projeto
dotnet run --project MedicalScribeR.Web

# Ver secrets configurados
dotnet user-secrets list --project MedicalScribeR.Web
```
