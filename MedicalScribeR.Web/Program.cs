using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Identity.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.CognitiveServices.Speech;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;
using System.Security.Claims;
using MedicalScribeR.Core.Agents;
using MedicalScribeR.Core.Interfaces;
using MedicalScribeR.Core.Configuration;
using MedicalScribeR.Core.Services;
using MedicalScribeR.Core.Models;
using MedicalScribeR.Infrastructure.Data;
using MedicalScribeR.Infrastructure.Services;
using MedicalScribeR.Infrastructure.Repositories;
using MedicalScribeR.Web.Hubs;
using MedicalScribeR.Web.Middleware;

// Carregar arquivo .env se existir (desenvolvimento)
if (File.Exists(".env"))
{
    DotNetEnv.Env.Load();
}

var builder = WebApplication.CreateBuilder(args);

// Configurar Azure Key Vault em produção
if (builder.Environment.IsProduction())
{
    var keyVaultUrl = "https://medscriber.vault.azure.net/";
    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultUrl),
        new Azure.Identity.DefaultAzureCredential(),
        new Azure.Extensions.AspNetCore.Configuration.Secrets.AzureKeyVaultConfigurationOptions
        {
            Manager = new Azure.Extensions.AspNetCore.Configuration.Secrets.AzureKeyVaultSecretManager(),
            ReloadInterval = TimeSpan.FromMinutes(30) // Recarregar secrets a cada 30 minutos
        });
}

// 1. Configurar autenticação com Microsoft Entra ID (Azure AD) - RELEASE READY 2025
// Configurar autenticação com Microsoft Entra ID (Azure AD)
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(options =>
    {
        builder.Configuration.Bind("AzureAd", options);
        options.UseTokenLifetime = true;
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.MapInboundClaims = false;
        options.BackchannelTimeout = TimeSpan.FromSeconds(60);
        options.RemoteSignOutPath = "/signout-oidc";
        options.SignedOutRedirectUri = "/";
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Scope.Add("User.Read");
        options.Events = new OpenIdConnectEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogError("Authentication failed: {Error}", context.Exception?.Message);
                context.Response.Redirect("/error?message=authentication_failed");
                context.HandleResponse();
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogInformation("Token validated for user: {User}", 
                    context.Principal?.Identity?.Name ?? "Unknown");
                return Task.CompletedTask;
            },
            OnRedirectToIdentityProvider = context =>
            {
                return Task.CompletedTask;
            },
            OnRemoteFailure = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogError("Remote authentication failure: {Error}", context.Failure?.Message);
                context.Response.Redirect("/error?message=remote_auth_failed");
                context.HandleResponse();
                return Task.CompletedTask;
            }
        };
    }, options =>
    {
        builder.Configuration.Bind("AzureAd", options);
    });

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.MaxAge = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(1);
    options.LoginPath = "/Account/SignIn";
    options.LogoutPath = "/Account/SignOut";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Events.OnValidatePrincipal = async context =>
    {
        var userPrincipal = context.Principal;
        if (userPrincipal?.Identity?.IsAuthenticated == true)
        {
            var expiryClaim = userPrincipal.FindFirst("exp");
            if (expiryClaim != null && long.TryParse(expiryClaim.Value, out var exp))
            {
                var expiryTime = DateTimeOffset.FromUnixTimeSeconds(exp);
                if (expiryTime < DateTimeOffset.UtcNow)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync();
                }
            }
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("MedicalProfessional", policy =>
        policy.RequireAssertion(context =>
        {
            if (context.User.HasClaim("extension_UserType", "Doctor") ||
                context.User.HasClaim("extension_UserType", "Nurse") ||
                context.User.HasClaim("extension_UserType", "MedicalProfessional"))
                return true;
            
            if (context.User.IsInRole("Doctor") ||
                context.User.IsInRole("Nurse") ||
                context.User.IsInRole("MedicalProfessional"))
                return true;
            
            var email = context.User.FindFirst(ClaimTypes.Email)?.Value;
            if (!string.IsNullOrEmpty(email))
            {
                var medicalDomains = new[] { "@hospital.com", "@clinica.com", "@crm.com" };
                return medicalDomains.Any(domain => email.EndsWith(domain, StringComparison.OrdinalIgnoreCase));
            }
            
            return false;
        }));
    
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireAssertion(context =>
            context.User.HasClaim("extension_UserType", "Admin") ||
            context.User.IsInRole("Admin") ||
            context.User.IsInRole("GlobalAdmin") ||
            context.User.HasClaim(ClaimTypes.Role, "Admin")));
    
    options.AddPolicy("DoctorOnly", policy =>
        policy.RequireAssertion(context =>
            context.User.HasClaim("extension_UserType", "Doctor") ||
            context.User.IsInRole("Doctor")));
    
    options.AddPolicy("AuthenticatedUser", policy =>
        policy.RequireAuthenticatedUser());
});

// 2. Configurar Entity Framework
builder.Services.AddDbContext<MedicalScribeDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null)
    ));

// 3. Configurar SignalR com Azure SignalR Service e Redis Backplane
var signalRConnectionString = builder.Configuration["Azure:SignalR:ConnectionString"];
var redisConnectionString = builder.Configuration["Azure:Redis:ConnectionString"];

if (!string.IsNullOrEmpty(signalRConnectionString) && builder.Environment.IsProduction())
{
    // Produção: Usar Azure SignalR Service
    builder.Services.AddSignalR(options =>
    {
        options.EnableDetailedErrors = builder.Environment.IsDevelopment();
        options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
        options.HandshakeTimeout = TimeSpan.FromSeconds(15);
        options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB
    })
    .AddAzureSignalR(options =>
    {
        options.ConnectionString = signalRConnectionString;
        options.ServerStickyMode = Microsoft.Azure.SignalR.ServerStickyMode.Required;
    });
    
    // Adicionar Redis para cache distribuído se disponível
    if (!string.IsNullOrEmpty(redisConnectionString))
    {
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = "MedicalScribeR";
        });
    }
}
else
{
    // Desenvolvimento: SignalR local
    builder.Services.AddSignalR(options =>
    {
        options.EnableDetailedErrors = builder.Environment.IsDevelopment();
        options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    });
}

// 4. Configurar opções de serviços externos
builder.Services.Configure<AzureAIServiceOptions>(
    builder.Configuration.GetSection("AzureAI"));
builder.Services.Configure<SpeechServiceOptions>(
    builder.Configuration.GetSection("AzureSpeech"));

// 5. Registrar serviços de infraestrutura
builder.Services.AddScoped<ITranscriptionRepository, TranscriptionRepository>();
builder.Services.AddSingleton<IAzureAIService, MedicalScribeR.Core.Services.AzureAIService>();
builder.Services.AddSingleton<AgentConfigLoader>();
builder.Services.AddSingleton<IPdfGenerationService, PdfGenerationService>();
builder.Services.AddScoped<AzureMLService>();

// 6. Registrar agentes especializados
builder.Services.AddScoped<PrescriptionAgent>();
builder.Services.AddScoped<SummaryAgent>();
builder.Services.AddScoped<ActionItemAgent>();

// Registrar como ISpecializedAgent para injeção automática
builder.Services.AddScoped<ISpecializedAgent>(provider => provider.GetRequiredService<PrescriptionAgent>());
builder.Services.AddScoped<ISpecializedAgent>(provider => provider.GetRequiredService<SummaryAgent>());
builder.Services.AddScoped<ISpecializedAgent>(provider => provider.GetRequiredService<ActionItemAgent>());

// 7. Registrar orquestrador
builder.Services.AddScoped<OrchestratorAgent>();

// 8. Configurar controllers e outros serviços
builder.Services.AddControllers();

// 9. Configurar HttpClientFactory para chamadas HTTP
builder.Services.AddHttpClient();

// 9a. Configurar Azure ML Service
builder.Services.AddHttpClient<MedicalScribeR.Core.Services.AzureMLService>();
builder.Services.AddSingleton<MedicalScribeR.Core.Services.AzureMLService>();

// 10. Configurar logging avançado
builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
    logging.AddDebug();
    
    if (builder.Environment.IsProduction())
    {
        logging.AddApplicationInsights(
            configureTelemetryConfiguration: (config) => 
                config.ConnectionString = builder.Configuration.GetConnectionString("ApplicationInsights"),
            configureApplicationInsightsLoggerOptions: (options) => 
            {
                options.IncludeScopes = true;
                options.TrackExceptionsAsExceptionTelemetry = true;
            });
        
        // Adicionar EventLog apenas no Windows
        if (OperatingSystem.IsWindows())
        {
            logging.AddEventLog();
        }
    }
    
    // Configurar níveis de log por namespace
    logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
    logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Information);
    logging.AddFilter("MedicalScribeR", LogLevel.Debug);
});

// 11. Configurar Application Insights para produção
if (builder.Environment.IsProduction())
{
    builder.Services.AddApplicationInsightsTelemetry(options =>
    {
        options.ConnectionString = builder.Configuration.GetConnectionString("ApplicationInsights");
        options.EnableAdaptiveSampling = true;
        options.EnableQuickPulseMetricStream = true;
        options.EnableAuthenticationTrackingJavaScript = true;
        options.EnableRequestTrackingTelemetryModule = true;
        options.EnableDependencyTrackingTelemetryModule = true;
    });
}

// 12. Configurar CORS para desenvolvimento e produção
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins("https://medicalscriber-web.salmonsky-f64ea152.brazilsouth.azurecontainerapps.io")
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
    });
}
else
{
    // Produção: CORS mais restritivo
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() 
                ?? new[] { "https://medicalscriber.azurewebsites.net" };
            
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
    });
}

// 13. Configurar Rate Limiting para produção
if (builder.Environment.IsProduction())
{
    builder.Services.AddRateLimiter(options =>
    {
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                factory: partition => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 10
                }));
        
        options.OnRejected = async (context, token) =>
        {
            context.HttpContext.Response.StatusCode = 429;
            await context.HttpContext.Response.WriteAsync("Rate limit exceeded. Try again later.", cancellationToken: token);
        };
    });
}

var app = builder.Build();

// Pipeline de configuração HTTP
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseCors();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseCors();
    
    // Rate limiting em produção
    app.UseRateLimiter();
}

// Forçar HTTPS em produção
if (app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

app.UseDefaultFiles(new DefaultFilesOptions { DefaultFileNames = new List<string> { "voither-index.html" } });
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Cache de arquivos estáticos por 1 dia em produção
        if (app.Environment.IsProduction())
        {
            ctx.Context.Response.Headers["Cache-Control"] = "public,max-age=86400";
        }
    }
});

app.UseRouting();

// Autenticação e autorização
app.UseAuthentication();
app.UseAuthorization();

// Mapear Hub do SignalR
app.MapHub<MedicalHub>("/medicalhub");

// Mapear controllers
app.MapControllers();

// REMOVIDO: Endpoint duplicado /api/speech-token - agora usando SpeechTokenController

app.MapPost("/api/generate-pdf", (GeneratedDocument document, IPdfGenerationService pdfService) =>
{
    try
    {
        var pdfBytes = pdfService.GeneratePdf(document);
        return Results.File(pdfBytes, "application/pdf", 
            $"{document.Type}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Erro ao gerar PDF: {ex.Message}");
    }
});

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { 
    status = "Healthy", 
    timestamp = DateTime.UtcNow,
    version = "1.0.0"
}));

// Garantir que o banco de dados seja criado
using (var scope = app.Services.CreateScope())
{
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<MedicalScribeDbContext>();
        if (app.Environment.IsDevelopment())
        {
            context.Database.EnsureCreated();
        }
        else
        {
            context.Database.Migrate();
        }
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Falha ao inicializar banco de dados - continuando sem persistência");
    }
}

app.Run();

// Classes de configuração
public class SpeechServiceOptions
{
    public string Key { get; set; } = "BzfV4z7V4LUPDYzDt6a0wwssEC0ZLQmm67j3qttu78raHErEoy7KJQQJ99BGACZoyfiXJ3w3AAAYACOG4swr";
    public string Region { get; set; } = "brazilsouth";
}

public class AzureAIServiceOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string OpenAIEndpoint { get; set; } = string.Empty;
    public string OpenAIKey { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = "gpt-4o";
}

// Tornar a classe Program pública para permitir acesso dos testes
public partial class Program { }