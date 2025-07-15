using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace MedicalScribeR.Web.Middleware
{
    /// <summary>
    /// Middleware para logging de requests HTTP e medição de performance
    /// </summary>
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var requestId = Guid.NewGuid().ToString();
            
            // Log da requisição
            _logger.LogInformation("Request {RequestId} started: {Method} {Path} from {RemoteIpAddress}",
                requestId,
                context.Request.Method,
                context.Request.Path,
                context.Connection.RemoteIpAddress);

            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Request {RequestId} failed with unhandled exception", requestId);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                
                // Log da resposta
                _logger.LogInformation("Request {RequestId} completed: {StatusCode} in {ElapsedMilliseconds}ms",
                    requestId,
                    context.Response.StatusCode,
                    stopwatch.ElapsedMilliseconds);
            }
        }
    }
}