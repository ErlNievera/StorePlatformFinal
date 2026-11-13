namespace Order.Api.Domain;

/// <summary>Where the orchestrated saga is for one order.</summary>
public enum SagaStep
{
    /// <summary>OrderPlaced published; waiting for StockReserved or StockReservationFailed.</summary>
    AwaitingStockReservation,

    /// <summary>ChargePaymentRequested published; waiting for PaymentCharged or PaymentFailed.</summary>
    AwaitingPayment,

    /// <summary>Compensation requested; waiting for StockReleased and/or PaymentRefunded.</summary>
    Compensating,

    /// <summary>Order confirmed.</summary>
    Completed,

    /// <summary>Order cancelled after all compensation finished.</summary>
    Cancelled,
}

/// <summary>
/// Saga bookkeeping for one order, kept apart from the order itself: the current step, the
/// payment to refund, and which compensations are still outstanding.
/// </summary>
public sealed class OrderSagaState
{
    /// <summary>Maximum length of <see cref="FailureReason"/>.</summary>
    public const int FailureReasonMaxLength = 500;

    private OrderSagaState()
    {
    }

    /// <summary>Starts the saga for a new order.</summary>
    public OrderSagaState(Guid orderId, string correlationId, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(orderId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        OrderId = orderId;
        CorrelationId = correlationId;
        Step = SagaStep.AwaitingStockReservation;
        StartedAt = now;
        UpdatedAt = now;
    }

    /// <summary>The order (primary key).</summary>
    public Guid OrderId { get; private set; }

    /// <summary>Correlation id of the place-order request, copied onto every saga message.</summary>
    public string CorrelationId { get; private set; } = string.Empty;

    /// <summary>Current step.</summary>
    public SagaStep Step { get; private set; }

    /// <summary>Payment charged for the order, needed for a refund.</summary>
    public Guid? PaymentId { get; private set; }

    /// <summary>Waiting for StockReleased.</summary>
    public bool StockReleasePending { get; private set; }

    /// <summary>Waiting for PaymentRefunded.</summary>
    public bool RefundPending { get; private set; }

    /// <summary>Why compensation started.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>When the saga started (UTC).</summary>
    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>Last change (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Optimistic concurrency token (PostgreSQL <c>xmin</c>).</summary>
    public uint Version { get; private set; }

    /// <summary>True when no compensation is outstanding.</summary>
    public bool CompensationFinished => !StockReleasePending && !RefundPending;

    /// <summary>Stock reserved; payment requested.</summary>
    public void AwaitPayment(DateTimeOffset now) => MoveTo(SagaStep.AwaitingPayment, now, SagaStep.AwaitingStockReservation);

    /// <summary>Payment charged and the order confirmed.</summary>
    public void Complete(Guid paymentId, DateTimeOffset now)
    {
        MoveTo(SagaStep.Completed, now, SagaStep.AwaitingPayment);
        PaymentId = paymentId;
    }

    /// <summary>Records the charged payment before compensating (failure after payment).</summary>
    public void RecordPayment(Guid paymentId, DateTimeOffset now)
    {
        PaymentId = paymentId;
        UpdatedAt = now;
    }

    /// <summary>Starts compensation, marking which steps must be undone.</summary>
    public void BeginCompensation(string reason, bool releaseStock, bool refundPayment, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (refundPayment && PaymentId is null)
        {
            throw new InvalidOperationException($"Order {OrderId} has no payment to refund.");
        }

        MoveTo(SagaStep.Compensating, now, SagaStep.AwaitingPayment);
        FailureReason = reason.Length <= FailureReasonMaxLength ? reason : reason[..FailureReasonMaxLength];
        StockReleasePending = releaseStock;
        RefundPending = refundPayment;
    }

    /// <summary>StockReleased arrived.</summary>
    public void StockReleased(DateTimeOffset now)
    {
        EnsureStep(SagaStep.Compensating);
        StockReleasePending = false;
        UpdatedAt = now;
    }

    /// <summary>PaymentRefunded arrived.</summary>
    public void PaymentRefunded(DateTimeOffset now)
    {
        EnsureStep(SagaStep.Compensating);
        RefundPending = false;
        UpdatedAt = now;
    }

    /// <summary>Saga ended with the order cancelled.</summary>
    public void MarkCancelled(string reason, DateTimeOffset now)
    {
        MoveTo(SagaStep.Cancelled, now, SagaStep.AwaitingStockReservation, SagaStep.Compensating);
        FailureReason ??= reason;
    }

    private void MoveTo(SagaStep target, DateTimeOffset now, params SagaStep[] allowedFrom)
    {
        EnsureStep(allowedFrom);
        Step = target;
        UpdatedAt = now;
    }

    private void EnsureStep(params SagaStep[] allowed)
    {
        if (!allowed.Contains(Step))
        {
            throw new InvalidOperationException(
                $"Saga for order {OrderId} is at {Step}; expected one of {string.Join(", ", allowed)}.");
        }
    }
}
