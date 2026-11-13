namespace Store.Contracts.Events;

/// <summary>One line of an order with the price captured when the order was placed.</summary>
/// <param name="ProductId">Catalog product id.</param>
/// <param name="ProductName">Product name at the time of ordering (snapshot).</param>
/// <param name="Quantity">Units ordered; at least 1.</param>
/// <param name="UnitPrice">Catalog price at the time of ordering (snapshot).</param>
public sealed record OrderLineItem(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice);

/// <summary>
/// Published by Order.Api when a customer places an order (status <c>Pending</c>).
/// Inventory consumes it and tries to reserve stock for every line.
/// </summary>
/// <param name="MessageId">Unique id of this message.</param>
/// <param name="CorrelationId">Correlation id of the place-order request.</param>
/// <param name="OrderId">The new order.</param>
/// <param name="OccurredAt">When the order was placed.</param>
/// <param name="CustomerId">Identity user id of the customer.</param>
/// <param name="Lines">Ordered products with price snapshots.</param>
/// <param name="TotalAmount">Sum of quantity x unit price over all lines.</param>
/// <param name="Currency">ISO 4217 currency code.</param>
public sealed record OrderPlaced(
    Guid MessageId,
    string CorrelationId,
    Guid OrderId,
    DateTimeOffset OccurredAt,
    Guid CustomerId,
    IReadOnlyList<OrderLineItem> Lines,
    decimal TotalAmount,
    string Currency) : IIntegrationEvent;

/// <summary>Published by Order.Api when the saga completes successfully (status <c>Confirmed</c>).</summary>
/// <param name="MessageId">Unique id of this message.</param>
/// <param name="CorrelationId">Correlation id of the place-order request.</param>
/// <param name="OrderId">The confirmed order.</param>
/// <param name="OccurredAt">When the order was confirmed.</param>
/// <param name="CustomerId">Identity user id of the customer.</param>
/// <param name="PaymentId">Payment that was charged for the order.</param>
/// <param name="TotalAmount">Amount charged.</param>
/// <param name="Currency">ISO 4217 currency code.</param>
public sealed record OrderConfirmed(
    Guid MessageId,
    string CorrelationId,
    Guid OrderId,
    DateTimeOffset OccurredAt,
    Guid CustomerId,
    Guid PaymentId,
    decimal TotalAmount,
    string Currency) : IIntegrationEvent;

/// <summary>
/// Published by Order.Api when the saga ends in <c>Cancelled</c>, after every needed
/// compensation (stock release, refund) has completed.
/// </summary>
/// <param name="MessageId">Unique id of this message.</param>
/// <param name="CorrelationId">Correlation id of the place-order request.</param>
/// <param name="OrderId">The cancelled order.</param>
/// <param name="OccurredAt">When the order was cancelled.</param>
/// <param name="CustomerId">Identity user id of the customer.</param>
/// <param name="Reason">Human-readable reason shown in the order's status history.</param>
public sealed record OrderCancelled(
    Guid MessageId,
    string CorrelationId,
    Guid OrderId,
    DateTimeOffset OccurredAt,
    Guid CustomerId,
    string Reason) : IIntegrationEvent;
