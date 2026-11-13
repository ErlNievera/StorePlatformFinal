namespace Store.Observability;

/// <summary>
/// Adds the current correlation id to outgoing <see cref="HttpClient"/> requests, so a call from
/// one service to another (through a generated client) stays in the same trace.
/// Register with <c>AddHttpMessageHandler&lt;CorrelationIdDelegatingHandler&gt;()</c>.
/// </summary>
public sealed class CorrelationIdDelegatingHandler(ICorrelationIdAccessor accessor) : DelegatingHandler
{
    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!request.Headers.Contains(Correlation.HeaderName))
        {
            var correlationId = accessor.CorrelationId ?? Correlation.NewId();
            accessor.CorrelationId ??= correlationId;
            request.Headers.TryAddWithoutValidation(Correlation.HeaderName, correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
