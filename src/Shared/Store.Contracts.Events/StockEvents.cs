namespace Store.Contracts.Events;

/// <summary>Units of one product held for an order.</summary>
/// <param name="ProductId">Catalog product id.</param>
/// <param name="Quantity">Units reserved or released.</param>
public sealed record StockLine(
    Guid ProductId,
    int Quantity);

/// <summary>A product that could not be reserved because too few units were available.</summary>
/// <param name="ProductId">Catalog product id.</param>
/// <param name="Requested">Units the order asked for.</param>
/// <param name="Available">Units that were available when the reservation was attempted.</param>
public sealed record StockShortage(
    Guid ProductId,
    int Requested,
    int Available);

/// <summary>
/// Published by Inventory.Api after reserving every line of an order in one local transaction.
/// Order moves to <c>StockReserved</c> and requests the payment charge.
/// </summary>
/// <param name="MessageId">Unique id of this message.</param>
/// <param name="CorrelationId">Correlation id of the place-order request.</param>
/// <param name="OrderId">Order the stock is held for.</param>
/// <param name="OccurredAt">When the reservation was made.</param>
/// <param name="Lines">Units reserved per product.</param>
public sealed record StockReserved(
    Guid MessageId,
    string CorrelationId,
    Guid OrderId,
    DateTimeOffset OccurredAt,
    IReadOnlyList<StockLine> Lines) : IIntegrationEvent;

/// <summary>
/// Published by Inventory.Api when an order cannot be reserved in full. Nothing is reserved
/// (all-or-nothing), so Order cancels without any compensation and nothing is charged.
/// </summary>
/// <param name="MessageId">Unique id of this message.</param>
/// <param name="CorrelationId">Correlation id of the place-order request.</param>
/// <param name="OrderId">Order that could not be reserved.</param>
/// <param name="OccurredAt">When the reservation was rejected.</param>
/// <param name="Reason">Human-readable reason, e.g. "Insufficient stock".</param>
/// <param name="Shortages">Products that were short, with requested and available units.</param>
public sealed record StockReservationFailed(
    Guid MessageId,
    string CorrelationId,
    Guid OrderId,
    DateTimeOffset OccurredAt,
    string Reason,
    IReadOnlyList<StockShortage> Shortages) : IIntegrationEvent;

/// <summary>
/// Published by Order.Api as a compensation step: release the stock reserved for the order
/// (after a declined payment, or after a failure once payment was charged).
/// </summary>
/// <param name="MessageId">Unique id of this message.</param>
/// <param name="CorrelationId">Correlation id of the place-order request.</param>
/// <param name="OrderId">Order whose reservation must be released.</param>
/// <param name="OccurredAt">When the release was requested.</param>
/// <param name="Reason">Why the stock is released; stored with the reservation.</param>
public sealed record StockReleaseRequested(
    Guid MessageId,
    string CorrelationId,
    Guid OrderId,
    DateTimeOffset OccurredAt,
    string Reason) : IIntegrationEvent;

/// <summary>
/// Published by Inventory.Api after returning an order's reserved units to available stock.
/// Releasing an order that holds no active reservation still publishes this event (with no lines)
/// so the saga can always make progress.
/// </summary>
/// <param name="MessageId">Unique id of this message.</param>
/// <param name="CorrelationId">Correlation id of the place-order request.</param>
/// <param name="OrderId">Order whose reservation was released.</param>
/// <param name="OccurredAt">When the stock was released.</param>
/// <param name="Reason">Reason copied from <see cref="StockReleaseRequested"/>.</param>
/// <param name="Lines">Units returned per product.</param>
public sealed record StockReleased(
    Guid MessageId,
    string CorrelationId,
    Guid OrderId,
    DateTimeOffset OccurredAt,
    string Reason,
    IReadOnlyList<StockLine> Lines) : IIntegrationEvent;
