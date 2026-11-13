namespace Payment.Api.Domain;

/// <summary>State of an <see cref="OrderPayment"/>.</summary>
public enum PaymentStatus
{
    /// <summary>Money was taken.</summary>
    Charged,

    /// <summary>The charge was refused; no money was taken.</summary>
    Declined,

    /// <summary>A previous charge was returned during compensation.</summary>
    Refunded,
}

/// <summary>
/// The payment attempt for one order. There is at most one per order: the charge request is
/// handled once (idempotently) and its outcome is kept here.
/// </summary>
public sealed class OrderPayment
{
    /// <summary>Maximum length of the failure and refund reasons.</summary>
    public const int ReasonMaxLength = 500;

    private OrderPayment()
    {
    }

    private OrderPayment(Guid orderId, Guid customerId, decimal amount, string currency, PaymentStatus status, string? failureReason, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(orderId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        if (currency.Length != 3)
        {
            throw new ArgumentException("Currency must be a 3-letter ISO 4217 code.", nameof(currency));
        }

        PaymentId = Guid.NewGuid();
        OrderId = orderId;
        CustomerId = customerId;
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Currency = currency.ToUpperInvariant();
        Status = status;
        FailureReason = Truncate(failureReason);
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Primary key.</summary>
    public Guid PaymentId { get; private set; }

    /// <summary>Order the payment belongs to (unique).</summary>
    public Guid OrderId { get; private set; }

    /// <summary>Identity user id of the customer.</summary>
    public Guid CustomerId { get; private set; }

    /// <summary>Order total charged.</summary>
    public decimal Amount { get; private set; }

    /// <summary>ISO 4217 currency code.</summary>
    public string Currency { get; private set; } = string.Empty;

    /// <summary>Current state.</summary>
    public PaymentStatus Status { get; private set; }

    /// <summary>Why the charge was declined.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>Why the charge was refunded.</summary>
    public string? RefundReason { get; private set; }

    /// <summary>When the charge was attempted (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Last change (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Optimistic concurrency token (PostgreSQL <c>xmin</c>).</summary>
    public uint Version { get; private set; }

    /// <summary>Records a successful charge.</summary>
    public static OrderPayment Charge(Guid orderId, Guid customerId, decimal amount, string currency, DateTimeOffset now) =>
        new(orderId, customerId, amount, currency, PaymentStatus.Charged, failureReason: null, now);

    /// <summary>Records a declined charge.</summary>
    public static OrderPayment Decline(Guid orderId, Guid customerId, decimal amount, string currency, string reason, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new(orderId, customerId, amount, currency, PaymentStatus.Declined, reason, now);
    }

    /// <summary>Refunds a charged payment.</summary>
    /// <exception cref="InvalidOperationException">The payment is not in <see cref="PaymentStatus.Charged"/>.</exception>
    public void Refund(string reason, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (Status != PaymentStatus.Charged)
        {
            throw new InvalidOperationException($"Payment {PaymentId} is {Status}; only a charged payment can be refunded.");
        }

        Status = PaymentStatus.Refunded;
        RefundReason = Truncate(reason);
        UpdatedAt = now;
    }

    private static string? Truncate(string? reason) =>
        reason is null || reason.Length <= ReasonMaxLength ? reason : reason[..ReasonMaxLength];
}
