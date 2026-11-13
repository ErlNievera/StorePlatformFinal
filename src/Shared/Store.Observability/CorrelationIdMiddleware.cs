using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Store.Observability;

/// <summary>
/// Reads <c>X-Correlation-ID</c> from the request (the gateway always sets one) or creates one,
/// makes it available through <see cref="ICorrelationIdAccessor"/> and <c>HttpContext.TraceIdentifier</c>,
/// returns it in the response header, and wraps the rest of the pipeline in a logging scope.
/// </summary>
public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ICorrelationIdAccessor accessor,
    ILogger<CorrelationIdMiddleware> logger)
{
    /// <summary>Handles one request.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = Correlation.Sanitize(context.Request.Headers[Correlation.HeaderName].ToString())
            ?? Correlation.NewId();

        accessor.CorrelationId = correlationId;
        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[Correlation.HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginCorrelationScope(correlationId))
        {
            await next(context);
        }
    }
}
