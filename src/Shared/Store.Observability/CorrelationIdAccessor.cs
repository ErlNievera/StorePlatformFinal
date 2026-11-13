namespace Store.Observability;

/// <summary>
/// The correlation id of the work currently running: the HTTP request being served or the
/// RabbitMQ message being consumed. Flows across <c>await</c> like <see cref="System.Threading.AsyncLocal{T}"/>.
/// </summary>
public interface ICorrelationIdAccessor
{
    /// <summary>Current correlation id, or <see langword="null"/> outside a request or message.</summary>
    string? CorrelationId { get; set; }
}

/// <summary><see cref="ICorrelationIdAccessor"/> backed by <see cref="AsyncLocal{T}"/>.</summary>
public sealed class CorrelationIdAccessor : ICorrelationIdAccessor
{
    private static readonly AsyncLocal<string?> Current = new();

    /// <inheritdoc />
    public string? CorrelationId
    {
        get => Current.Value;
        set => Current.Value = value;
    }
}
