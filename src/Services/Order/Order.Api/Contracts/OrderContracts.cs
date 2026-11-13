using System.ComponentModel.DataAnnotations;
using Order.Api.Domain;

namespace Order.Api.Contracts;

/// <summary>Schema <c>PlaceOrderLine</c>.</summary>
public sealed class PlaceOrderLine
{
    /// <summary>Catalog product id.</summary>
    [Required]
    public Guid? ProductId { get; init; }

    /// <summary>Units, 1 to 100.</summary>
    [Range(1, 100)]
    public int Quantity { get; init; }
}

/// <summary>Schema <c>PlaceOrderRequest</c>.</summary>
public sealed class PlaceOrderRequest
{
    /// <summary>1 to 50 lines.</summary>
    [Required]
    [MinLength(1)]
    [MaxLength(50)]
    public List<PlaceOrderLine> Lines { get; init; } = [];
}

/// <summary>Schema <c>OrderLine</c>.</summary>
public sealed record OrderLineResponse(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal LineTotal);

/// <summary>Schema <c>OrderStatusHistoryEntry</c>.</summary>
public sealed record OrderStatusHistoryResponse(OrderStatus Status, string? Reason, bool IsCompensation, DateTimeOffset OccurredAt);

/// <summary>Schema <c>OrderSummary</c>.</summary>
public sealed record OrderSummaryResponse(
    Guid OrderId,
    OrderStatus Status,
    decimal TotalAmount,
    string Currency,
    int ItemCount,
    string? CancellationReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Schema <c>OrderDetails</c>.</summary>
public sealed record OrderDetailsResponse(
    Guid OrderId,
    Guid CustomerId,
    OrderStatus Status,
    decimal TotalAmount,
    string Currency,
    string? CancellationReason,
    IReadOnlyList<OrderLineResponse> Lines,
    IReadOnlyList<OrderStatusHistoryResponse> History,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    /// <summary>Maps an order with lines and history loaded.</summary>
    public static OrderDetailsResponse From(CustomerOrder order) => new(
        order.OrderId,
        order.CustomerId,
        order.Status,
        order.TotalAmount,
        order.Currency,
        order.CancellationReason,
        order.Lines.Select(l => new OrderLineResponse(l.ProductId, l.ProductName, l.UnitPrice, l.Quantity, l.LineTotal)).ToList(),
        order.History.OrderBy(h => h.Sequence)
            .Select(h => new OrderStatusHistoryResponse(h.Status, h.Reason, h.IsCompensation, h.OccurredAt)).ToList(),
        order.CreatedAt,
        order.UpdatedAt);
}

/// <summary>Schema <c>OrderFailureModeRequest</c>.</summary>
public sealed class OrderFailureModeRequest
{
    /// <summary>New mode.</summary>
    [Required]
    public OrderFailureMode? Mode { get; init; }
}

/// <summary>Schema <c>OrderFailureModeResponse</c>.</summary>
public sealed record OrderFailureModeResponse(OrderFailureMode Mode, DateTimeOffset UpdatedAt)
{
    /// <summary>Maps the settings row.</summary>
    public static OrderFailureModeResponse From(OrderSettings settings) => new(settings.FailureMode, settings.UpdatedAt);
}
