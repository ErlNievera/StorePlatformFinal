namespace Order.Api.Domain;

/// <summary>One entry of an order's timeline: what happened, why, and when.</summary>
public sealed class OrderStatusHistory
{
    /// <summary>Maximum length of <see cref="Reason"/>.</summary>
    public const int ReasonMaxLength = 500;

    private OrderStatusHistory()
    {
    }

    /// <summary>Creates an entry.</summary>
    public OrderStatusHistory(int sequence, OrderStatus status, string? reason, bool isCompensation, DateTimeOffset occurredAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
        Id = Guid.NewGuid();
        Sequence = sequence;
        Status = status;
        Reason = reason is null || reason.Length <= ReasonMaxLength ? reason : reason[..ReasonMaxLength];
        IsCompensation = isCompensation;
        OccurredAt = occurredAt;
    }

    /// <summary>Primary key.</summary>
    public Guid Id { get; private set; }

    /// <summary>Owning order.</summary>
    public Guid OrderId { get; private set; }

    /// <summary>Position in the order's timeline (1, 2, 3...), so steps recorded in the same instant keep their order.</summary>
    public int Sequence { get; private set; }

    /// <summary>Status after this step.</summary>
    public OrderStatus Status { get; private set; }

    /// <summary>What happened, e.g. "Stock reserved" or "Payment declined".</summary>
    public string? Reason { get; private set; }

    /// <summary>True for compensation steps (stock released, payment refunded).</summary>
    public bool IsCompensation { get; private set; }

    /// <summary>When it happened (UTC).</summary>
    public DateTimeOffset OccurredAt { get; private set; }
}
