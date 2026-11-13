using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace Store.Contracts.Events;

/// <summary>
/// Routing keys used on the <c>store.events</c> topic exchange, one per event type.
/// Publishers use <see cref="For{TEvent}"/>; consumers bind queues to these keys and use
/// <see cref="TryGetEventType"/> to know which record to deserialize.
/// </summary>
public static class EventRoutingKeys
{
    /// <summary>Routing key of <see cref="Events.OrderPlaced"/>.</summary>
    public const string OrderPlaced = "order.placed";

    /// <summary>Routing key of <see cref="Events.OrderConfirmed"/>.</summary>
    public const string OrderConfirmed = "order.confirmed";

    /// <summary>Routing key of <see cref="Events.OrderCancelled"/>.</summary>
    public const string OrderCancelled = "order.cancelled";

    /// <summary>Routing key of <see cref="Events.StockReserved"/>.</summary>
    public const string StockReserved = "stock.reserved";

    /// <summary>Routing key of <see cref="Events.StockReservationFailed"/>.</summary>
    public const string StockReservationFailed = "stock.reservation-failed";

    /// <summary>Routing key of <see cref="Events.StockReleaseRequested"/>.</summary>
    public const string StockReleaseRequested = "stock.release-requested";

    /// <summary>Routing key of <see cref="Events.StockReleased"/>.</summary>
    public const string StockReleased = "stock.released";

    /// <summary>Routing key of <see cref="Events.ChargePaymentRequested"/>.</summary>
    public const string ChargePaymentRequested = "payment.charge-requested";

    /// <summary>Routing key of <see cref="Events.PaymentCharged"/>.</summary>
    public const string PaymentCharged = "payment.charged";

    /// <summary>Routing key of <see cref="Events.PaymentFailed"/>.</summary>
    public const string PaymentFailed = "payment.failed";

    /// <summary>Routing key of <see cref="Events.RefundPaymentRequested"/>.</summary>
    public const string RefundPaymentRequested = "payment.refund-requested";

    /// <summary>Routing key of <see cref="Events.PaymentRefunded"/>.</summary>
    public const string PaymentRefunded = "payment.refunded";

    private static readonly FrozenDictionary<Type, string> KeysByType = new Dictionary<Type, string>
    {
        [typeof(Events.OrderPlaced)] = OrderPlaced,
        [typeof(Events.OrderConfirmed)] = OrderConfirmed,
        [typeof(Events.OrderCancelled)] = OrderCancelled,
        [typeof(Events.StockReserved)] = StockReserved,
        [typeof(Events.StockReservationFailed)] = StockReservationFailed,
        [typeof(Events.StockReleaseRequested)] = StockReleaseRequested,
        [typeof(Events.StockReleased)] = StockReleased,
        [typeof(Events.ChargePaymentRequested)] = ChargePaymentRequested,
        [typeof(Events.PaymentCharged)] = PaymentCharged,
        [typeof(Events.PaymentFailed)] = PaymentFailed,
        [typeof(Events.RefundPaymentRequested)] = RefundPaymentRequested,
        [typeof(Events.PaymentRefunded)] = PaymentRefunded,
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, Type> TypesByKey =
        KeysByType.ToFrozenDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);

    /// <summary>Every routing key, for example to bind an audit queue.</summary>
    public static IReadOnlyCollection<string> All => TypesByKey.Keys;

    /// <summary>Gets the routing key of an event type.</summary>
    /// <typeparam name="TEvent">A record from this assembly.</typeparam>
    public static string For<TEvent>() where TEvent : IIntegrationEvent => For(typeof(TEvent));

    /// <summary>Gets the routing key of an event type.</summary>
    /// <param name="eventType">A record type from this assembly.</param>
    /// <exception cref="ArgumentException">The type is not a known integration event.</exception>
    public static string For(Type eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        return KeysByType.TryGetValue(eventType, out var key)
            ? key
            : throw new ArgumentException($"'{eventType.FullName}' is not a known integration event.", nameof(eventType));
    }

    /// <summary>Finds the event record type published with a routing key.</summary>
    /// <param name="routingKey">Routing key of a received message.</param>
    /// <param name="eventType">The matching record type, when found.</param>
    /// <returns><see langword="true"/> when the key belongs to a known event.</returns>
    public static bool TryGetEventType(string routingKey, [NotNullWhen(true)] out Type? eventType) =>
        TypesByKey.TryGetValue(routingKey, out eventType);
}
