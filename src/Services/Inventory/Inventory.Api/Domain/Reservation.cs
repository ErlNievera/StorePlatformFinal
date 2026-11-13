namespace Inventory.Api.Domain;

/// <summary>State of a <see cref="Reservation"/>.</summary>
public enum ReservationStatus
{
    /// <summary>Units are held for the order.</summary>
    Reserved,

    /// <summary>Units were returned to available stock (saga compensation).</summary>
    Released,
}

/// <summary>Units of one product held by a <see cref="Reservation"/>.</summary>
public sealed class ReservationLine
{
    private ReservationLine()
    {
    }

    /// <summary>Creates a line.</summary>
    public ReservationLine(Guid productId, int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(productId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        Id = Guid.NewGuid();
        ProductId = productId;
        Quantity = quantity;
    }

    /// <summary>Primary key.</summary>
    public Guid Id { get; private set; }

    /// <summary>Owning reservation (order id).</summary>
    public Guid OrderId { get; private set; }

    /// <summary>Reserved product.</summary>
    public Guid ProductId { get; private set; }

    /// <summary>Reserved units.</summary>
    public int Quantity { get; private set; }
}

/// <summary>
/// Stock held for one order by the saga. Created all-or-nothing when <c>OrderPlaced</c> is handled;
/// released when <c>StockReleaseRequested</c> is handled.
/// </summary>
public sealed class Reservation
{
    /// <summary>Maximum length of <see cref="ReleaseReason"/>.</summary>
    public const int ReleaseReasonMaxLength = 500;

    private readonly List<ReservationLine> _lines = [];

    private Reservation()
    {
    }

    /// <summary>Creates an active reservation.</summary>
    public Reservation(Guid orderId, IEnumerable<ReservationLine> lines, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(orderId, Guid.Empty);
        ArgumentNullException.ThrowIfNull(lines);

        OrderId = orderId;
        _lines.AddRange(lines);
        if (_lines.Count == 0)
        {
            throw new ArgumentException("A reservation needs at least one line.", nameof(lines));
        }

        Status = ReservationStatus.Reserved;
        CreatedAt = now;
    }

    /// <summary>Order the units are held for (primary key).</summary>
    public Guid OrderId { get; private set; }

    /// <summary>Current state.</summary>
    public ReservationStatus Status { get; private set; }

    /// <summary>Reserved units per product.</summary>
    public IReadOnlyList<ReservationLine> Lines => _lines;

    /// <summary>When the units were reserved (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>When the units were released (UTC), if they were.</summary>
    public DateTimeOffset? ReleasedAt { get; private set; }

    /// <summary>Why the units were released, if they were.</summary>
    public string? ReleaseReason { get; private set; }

    /// <summary>Marks the reservation released. The caller returns the units on each <see cref="StockItem"/>.</summary>
    /// <exception cref="StockConflictException">The reservation was already released.</exception>
    public void MarkReleased(string reason, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (Status == ReservationStatus.Released)
        {
            throw new StockConflictException($"Reservation for order {OrderId} is already released.");
        }

        Status = ReservationStatus.Released;
        ReleasedAt = now;
        ReleaseReason = reason.Length <= ReleaseReasonMaxLength ? reason : reason[..ReleaseReasonMaxLength];
    }
}
