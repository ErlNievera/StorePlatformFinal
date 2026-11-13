using System.ComponentModel.DataAnnotations;
using Inventory.Api.Domain;

namespace Inventory.Api.Contracts;

/// <summary>Schema <c>StockItem</c> in inventory.v1.yaml.</summary>
public sealed record StockItemResponse(
    Guid ProductId,
    int QuantityOnHand,
    int QuantityReserved,
    int QuantityAvailable,
    DateTimeOffset UpdatedAt)
{
    /// <summary>Maps an entity.</summary>
    public static StockItemResponse From(StockItem item) =>
        new(item.ProductId, item.QuantityOnHand, item.QuantityReserved, item.QuantityAvailable, item.UpdatedAt);
}

/// <summary>Schema <c>StockAdjustmentRequest</c>.</summary>
public sealed class StockAdjustmentRequest : IValidatableObject
{
    /// <summary>Units to add (positive) or remove (negative); zero is rejected.</summary>
    [Range(-100000, 100000)]
    public int Delta { get; init; }

    /// <summary>Why the stock changed.</summary>
    [Required]
    [StringLength(StockAdjustment.ReasonMaxLength, MinimumLength = 1)]
    public string Reason { get; init; } = string.Empty;

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Delta == 0)
        {
            yield return new ValidationResult("Delta must not be zero.", [nameof(Delta)]);
        }
    }
}

/// <summary>Schema <c>ReservationLine</c>.</summary>
public sealed record ReservationLineResponse(Guid ProductId, int Quantity);

/// <summary>Schema <c>Reservation</c>.</summary>
public sealed record ReservationResponse(
    Guid OrderId,
    ReservationStatus Status,
    IReadOnlyList<ReservationLineResponse> Lines,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReleasedAt,
    string? ReleaseReason)
{
    /// <summary>Maps an entity.</summary>
    public static ReservationResponse From(Reservation reservation) => new(
        reservation.OrderId,
        reservation.Status,
        reservation.Lines.Select(l => new ReservationLineResponse(l.ProductId, l.Quantity)).ToList(),
        reservation.CreatedAt,
        reservation.ReleasedAt,
        reservation.ReleaseReason);
}
