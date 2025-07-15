# CORREÇÃO CRÍTICA DOS PROBLEMAS DE AUTENTICAÇÃO

## PROBLEMA IDENTIFICADO
O sistema não consegue fazer login devido a configurações conflitantes entre:
- Program.cs (espera `/Account/Login`)
- AccountController.cs (implementa `/Account/SignIn`)

## CORREÇÃO IMEDIATA NECESSÁRIA

### 1. Corrigir Program.cs (linhas 85-87)
```csharp
// ALTERAR DE:
options.LoginPath = "/Account/Login";
options.LogoutPath = "/Account/Logout";
options.AccessDeniedPath = "/Account/AccessDenied";

// PARA:
options.LoginPath = "/Account/SignIn";
options.LogoutPath = "/Account/SignOut";
options.AccessDeniedPath = "/Account/AccessDenied";
```

### 2. Corrigir configuração AzureAd
O problema está na configuração do Domain no appsettings.json.

**OPÇÃO A: Usar domínio personalizado**
```json
"AzureAd": {
  "Domain": "medicalscriber.com",
  "CallbackPath": "/signin-oidc"
}
```

**OPÇÃO B: Configurar corretamente para Container Apps**
```json
"AzureAd": {
  "Instance": "https://login.microsoftonline.com/",
  "TenantId": "c97cb1fb-d321-401b-b372-417a528542ba",
  "ClientId": "a7105204-01cd-4d90-9ea3-b2f476c3ca41",
  "CallbackPath": "/signin-oidc",
  "SignedOutCallbackPath": "/signout-callback-oidc"
}
```

### 3. Recursos Azure Necessários (rg-medicalscribe)
```
✅ App Service / Container App
✅ SQL Database Server + Database  
✅ Redis Cache
✅ SignalR Service
✅ Cognitive Services (Text Analytics)
✅ OpenAI Service
✅ Speech Service
✅ Application Insights
✅ Azure AD App Registration
```

### 4. Connection Strings Limpas
Todas as connection strings estão hardcoded. Devem ser configuradas via:
- Azure Key Vault (recomendado)
- Environment Variables
- Azure App Configuration

## PRÓXIMOS PASSOS
1. Aplicar correções no Program.cs
2. Verificar recursos Azure existentes
3. Reconfigurar connection strings
4. Testar fluxo de autenticação