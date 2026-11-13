namespace Store.Contracts.Events;

/// <summary>
/// Common shape of every event published on the <c>store.events</c> topic exchange.
/// </summary>
/// <remarks>
/// <para><see cref="MessageId"/> is unique per published message and is what consumers record in
/// their <c>ProcessedMessages</c> table to make handling idempotent.</para>
/// <para><see cref="CorrelationId"/> is the <c>X-Correlation-ID</c> of the HTTP request that started
/// the flow (normally the customer's "place order" call through the gateway). Every event the saga
/// produces for that order carries the same value so logs can be joined across services.</para>
/// </remarks>
public interface IIntegrationEvent
{
    /// <summary>Unique id of this message; used for consumer idempotency.</summary>
    Guid MessageId { get; }

    /// <summary>Correlation id of the request that started the flow.</summary>
    string CorrelationId { get; }

    /// <summary>Order the event belongs to.</summary>
    Guid OrderId { get; }

    /// <summary>When the publisher produced the event (UTC).</summary>
    DateTimeOffset OccurredAt { get; }
}
