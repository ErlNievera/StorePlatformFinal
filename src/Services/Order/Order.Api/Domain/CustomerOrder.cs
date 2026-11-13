namespace Order.Api.Domain;

/// <summary>
/// An order and its status history. Transition methods enforce the saga's state machine and
/// append a history entry for every step; an invalid transition throws
/// <see cref="InvalidOrderTransitionException"/>.
/// </summary>
public sealed class CustomerOrder
{
    /// <summary>Maximum length of <see cref="CancellationReason"/>.</summary>
    public const int CancellationReasonMaxLength = 500;

    private readonly List<OrderLine> _lines = [];
    private readonly List<OrderStatusHistory> _history = [];

    private CustomerOrder()
    {
    }

    /// <summary>Primary key.</summary>
    public Guid OrderId { get; private set; }

    /// <summary>Identity user id of the customer (JWT <c>sub</c>).</summary>
    public Guid CustomerId { get; private set; }

    /// <summary>Current status.</summary>
    public OrderStatus Status { get; private set; }

    /// <summary>Sum of the line totals at placement.</summary>
    public decimal TotalAmount { get; private set; }

    /// <summary>ISO 4217 currency code.</summary>
    public string Currency { get; private set; } = string.Empty;

    /// <summary>Why the order was cancelled, when it was.</summary>
    public string? CancellationReason { get; private set; }

    /// <summary>When the order was placed (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Last change (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Optimistic concurrency token (PostgreSQL <c>xmin</c>).</summary>
    public uint Version { get; private set; }

    /// <summary>Ordered products.</summary>
    public IReadOnlyList<OrderLine> Lines => _lines;

    /// <summary>Timeline, oldest first.</summary>
    public IReadOnlyList<OrderStatusHistory> History => _history;

    /// <summary>Places an order in <see cref="OrderStatus.Pending"/>.</summary>
    public static CustomerOrder Place(Guid customerId, IReadOnlyCollection<OrderLine> lines, string currency, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(customerId, Guid.Empty);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        if (lines.Count == 0)
        {
            throw new ArgumentException("An order needs at least one line.", nameof(lines));
        }

        var order = new CustomerOrder
        {
            OrderId = Guid.NewGuid(),
            CustomerId = customerId,
            Status = OrderStatus.Pending,
            Currency = currency.ToUpperInvariant(),
            CreatedAt = now,
            UpdatedAt = now,
        };
        order._lines.AddRange(lines);
        order.TotalAmount = lines.Sum(l => l.LineTotal);
        order._history.Add(new OrderStatusHistory(OrderStatus.Pending, "Order placed", isCompensation: false, now));
        return order;
    }

    /// <summary>Pending → StockReserved.</summary>
    public void MarkStockReserved(DateTimeOffset now) =>
        Move(OrderStatus.StockReserved, "Stock reserved", now, OrderStatus.Pending);

    /// <summary>StockReserved → PaymentCharged.</summary>
    public void MarkPaymentCharged(DateTimeOffset now) =>
        Move(OrderStatus.PaymentCharged, "Payment charged", now, OrderStatus.StockReserved);

    /// <summary>PaymentCharged → Confirmed.</summary>
    public void Confirm(DateTimeOffset now) =>
        Move(OrderStatus.Confirmed, "Order confirmed", now, OrderStatus.PaymentCharged);

    /// <summary>StockReserved or PaymentCharged → Cancelling, recording the failure.</summary>
    public void StartCancellation(string reason, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Move(OrderStatus.Cancelling, reason, now, OrderStatus.StockReserved, OrderStatus.PaymentCharged);
        CancellationReason = Truncate(reason);
    }

    /// <summary>Records a finished compensation step (stays in Cancelling).</summary>
    public void RecordCompensation(string step, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(step);
        EnsureIn(OrderStatus.Cancelling);
        _history.Add(new OrderStatusHistory(OrderStatus.Cancelling, step, isCompensation: true, now));
        UpdatedAt = now;
    }

    /// <summary>Pending or Cancelling → Cancelled.</summary>
    public void Cancel(string reason, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Move(OrderStatus.Cancelled, reason, now, OrderStatus.Pending, OrderStatus.Cancelling);
        CancellationReason ??= Truncate(reason);
    }

    private void Move(OrderStatus target, string reason, DateTimeOffset now, params OrderStatus[] allowedFrom)
    {
        EnsureIn(allowedFrom);
        Status = target;
        UpdatedAt = now;
        _history.Add(new OrderStatusHistory(target, reason, isCompensation: false, now));
    }

    private void EnsureIn(params OrderStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new InvalidOrderTransitionException(OrderId, Status, allowed);
        }
    }

    private static string Truncate(string reason) =>
        reason.Length <= CancellationReasonMaxLength ? reason : reason[..CancellationReasonMaxLength];
}

/// <summary>A saga event arrived for an order in a status that does not accept it.</summary>
public sealed class InvalidOrderTransitionException(Guid orderId, OrderStatus current, IReadOnlyCollection<OrderStatus> expected)
    : InvalidOperationException($"Order {orderId} is {current}; expected one of {string.Join(", ", expected)}.")
{
    /// <summary>Status the order was in.</summary>
    public OrderStatus Current { get; } = current;
}
