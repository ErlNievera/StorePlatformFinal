using Store.Observability;

namespace Gateway.Api;

/// <summary>
/// Creates the correlation id at the edge: keeps a valid <c>X-Correlation-ID</c> from the client or
/// replaces it with a new one, before Ocelot runs. Ocelot's <c>RequestIdKey</c> then forwards the
/// same header to the downstream service, and the response carries it back to the client.
/// </summary>
public sealed class EdgeCorrelationMiddleware(RequestDelegate next, ILogger<EdgeCorrelationMiddleware> logger)
{
    /// <summary>Handles one request.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = Correlation.Sanitize(context.Request.Headers[Correlation.HeaderName].ToString())
            ?? Correlation.NewId();

        context.Request.Headers[Correlation.HeaderName] = correlationId;
        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[Correlation.HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginCorrelationScope(correlationId))
        {
            await next(context);
            logger.LogInformation(
                "{Method} {Path} -> {StatusCode}",
                context.Request.Method, context.Request.Path, context.Response.StatusCode);
        }
    }
}
