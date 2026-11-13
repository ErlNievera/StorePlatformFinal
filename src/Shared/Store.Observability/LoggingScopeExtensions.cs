using Microsoft.Extensions.Logging;

namespace Store.Observability;

/// <summary>
/// Logging scopes that put the correlation id on every log line written inside them. Message-template
/// scopes print as <c>CorrelationId:&lt;id&gt;</c> in the console and stay structured
/// (<c>CorrelationId</c> key) for other providers.
/// </summary>
public static class LoggingScopeExtensions
{
    /// <summary>Opens a scope with <c>CorrelationId</c>.</summary>
    public static IDisposable? BeginCorrelationScope(this ILogger logger, string correlationId)
    {
        ArgumentNullException.ThrowIfNull(logger);
        return logger.BeginScope("CorrelationId:{CorrelationId}", correlationId);
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
        return logger.BeginScope(
            "CorrelationId:{CorrelationId} MessageId:{MessageId} EventType:{EventType} Queue:{Queue}",
            correlationId,
            messageId,
            eventType,
            queue);
    }
}
