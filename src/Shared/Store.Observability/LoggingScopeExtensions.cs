using Microsoft.Extensions.Logging;

namespace Store.Observability;

/// <summary>Logging scopes that put the correlation id on every log line written inside them.</summary>
public static class LoggingScopeExtensions
{
    /// <summary>Opens a scope with <c>CorrelationId</c>.</summary>
    public static IDisposable? BeginCorrelationScope(this ILogger logger, string correlationId)
    {
        ArgumentNullException.ThrowIfNull(logger);
        return logger.BeginScope(new Dictionary<string, object?>
        {
            [Correlation.LogScopeKey] = correlationId,
        });
    }

    /// <summary>Opens a scope for a consumed message with <c>CorrelationId</c>, <c>MessageId</c>, <c>EventType</c> and <c>Queue</c>.</summary>
    public static IDisposable? BeginMessageScope(
        this ILogger logger,
        string correlationId,
        Guid messageId,
        string eventType,
        string queue)
    {
        ArgumentNullException.ThrowIfNull(logger);
        return logger.BeginScope(new Dictionary<string, object?>
        {
            [Correlation.LogScopeKey] = correlationId,
            ["MessageId"] = messageId,
            ["EventType"] = eventType,
            ["Queue"] = queue,
        });
    }
}
