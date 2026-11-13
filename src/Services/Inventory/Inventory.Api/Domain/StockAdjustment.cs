namespace Inventory.Api.Domain;

/// <summary>Audit record of one admin stock adjustment.</summary>
public sealed class StockAdjustment
{
    /// <summary>Maximum length of <see cref="Reason"/>.</summary>
    public const int ReasonMaxLength = 200;

    /// <summary>Maximum length of <see cref="AdjustedBy"/>.</summary>
    public const int AdjustedByMaxLength = 256;

    private StockAdjustment()
    {
    }

    /// <summary>Records an adjustment that was applied to <paramref name="item"/>.</summary>
    public StockAdjustment(StockItem item, int delta, string reason, string? adjustedBy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        Id = Guid.NewGuid();
        ProductId = item.ProductId;
        Delta = delta;
        Reason = reason.Trim();
        QuantityOnHandAfter = item.QuantityOnHand;
        AdjustedBy = adjustedBy;
        CreatedAt = now;
    }

    /// <summary>Primary key.</summary>
    public Guid Id { get; private set; }

    /// <summary>Adjusted product.</summary>
    public Guid ProductId { get; private set; }

    /// <summary>Units added (positive) or removed (negative).</summary>
    public int Delta { get; private set; }

    /// <summary>Why the stock changed.</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>On-hand quantity right after the adjustment.</summary>
    public int QuantityOnHandAfter { get; private set; }

    /// <summary>Admin who made the change (JWT subject or email), when known.</summary>
    public string? AdjustedBy { get; private set; }

    /// <summary>When the adjustment was made (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private set; }
}
