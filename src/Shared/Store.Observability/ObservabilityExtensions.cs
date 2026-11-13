using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Store.Observability;

/// <summary>Registration and pipeline helpers used by every service.</summary>
public static class ObservabilityExtensions
{
    /// <summary>
    /// Registers the correlation id accessor and DelegatingHandler, Problem Details with a
    /// <c>correlationId</c> extension on every problem (including automatic 400 validation
    /// responses and empty 401/403/404 responses), and <see cref="StoreExceptionHandler"/>.
    /// </summary>
    public static IServiceCollection AddStoreObservability(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<ICorrelationIdAccessor, CorrelationIdAccessor>();
        services.TryAddTransient<CorrelationIdDelegatingHandler>();
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                var correlationId = context.HttpContext.RequestServices
                    .GetService<ICorrelationIdAccessor>()?.CorrelationId
                    ?? context.HttpContext.TraceIdentifier;
                context.ProblemDetails.Extensions[Correlation.ProblemDetailsKey] = correlationId;
                context.ProblemDetails.Instance ??= $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
                context.ProblemDetails.Extensions.Remove("traceId");
            };
        });
        services.AddExceptionHandler<StoreExceptionHandler>();

        return services;
    }

    /// <summary>
    /// Adds the correlation id middleware first, then the exception handler and status code pages
    /// so every error leaves the service as Problem Details with the correlation id.
    /// </summary>
    public static IApplicationBuilder UseStoreObservability(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        return app;
    }

    /// <summary>
    /// Console logging with scopes on one line per entry, so the <c>CorrelationId</c> scope shows on
    /// every line and a request can be followed across services with <c>docker compose logs | grep</c>.
    /// </summary>
    public static ILoggingBuilder AddStoreConsoleLogging(this ILoggingBuilder logging)
    {
        ArgumentNullException.ThrowIfNull(logging);

        logging.ClearProviders();
        logging.AddSimpleConsole(options =>
        {
            options.IncludeScopes = true;
            options.SingleLine = true;
            options.TimestampFormat = "HH:mm:ss.fff ";
            options.UseUtcTimestamp = true;
        });
        return logging;
    }
}
