namespace Store.Contracts.Events;

/// <summary>
/// Published by Order.Api once stock is reserved: charge the customer for the order total.
/// </summary>
/// <param name="MessageId">Unique id of this message.</param>
/// <param name="CorrelationId">Correlation id of the place-order request.</param>
/// <param name="OrderId">Order to charge for.</param>
/// <param name="OccurredAt">When the charge was requested.</param>
/// <param name="CustomerId">Identity user id of the customer.</param>
/// <param name="Amount">Order total from the price snapshot on the order lines.</param>
/// <param name="Currency">ISO 4217 currency code.</param>
public sealed record ChargePaymentRequested(
    Guid MessageId,
    string CorrelationId,
    Guid OrderId,
    DateTimeOffset OccurredAt,
    Guid CustomerId,
    decimal Amount,
    string Currency) : IIntegrationEvent;

/// <summary>Published by Payment.Api after a successful charge. Order moves to <c>PaymentCharged</c>.</summary>
/// <param name="MessageId">Unique id of this message.</param>
/// <param name="CorrelationId">Correlation id of the place-order request.</param>
/// <param name="OrderId">Order that was charged.</param>
/// <param name="OccurredAt">When the charge succeeded.</param>
/// <param name="PaymentId">Id of the payment record; needed to request a refund.</param>
/// <param name="Amount">Amount charged.</param>
/// <param name="Currency">ISO 4217 currency code.</param>
public sealed record PaymentCharged(
    Guid MessageId,
    string CorrelationId,
    Guid OrderId,
    DateTimeOffset OccurredAt,
    Guid PaymentId,
    decimal Amount,
    string Currency) : IIntegrationEvent;

/// <summary>
/// Published by Payment.Api when a charge is declined (for example while the failure mode is
/// <c>DeclineCharge</c>). No money was taken; Order releases the stock and cancels.
/// </summary>
/// <param name="MessageId">Unique id of this message.</param>
/// <param name="CorrelationId">Correlation id of the place-order request.</param>
/// <param name="OrderId">Order whose charge was declined.</param>
/// <param name="OccurredAt">When the charge was declined.</param>
/// <param name="PaymentId">Id of the declined payment record.</param>
/// <param name="Reason">Human-readable decline reason.</param>
public sealed record PaymentFailed(
    Guid MessageId,
    string CorrelationId,
    Guid OrderId,
    DateTimeOffset OccurredAt,
    Guid PaymentId,
    string Reason) : IIntegrationEvent;

/// <summary>
/// Published by Order.Api as a compensation step when the order fails after payment was charged
/// (for example while the order failure mode is <c>FailAfterPayment</c>).
/// </summary>
/// <param name="MessageId">Unique id of this message.</param>
/// <param name="CorrelationId">Correlation id of the place-order request.</param>
/// <param name="OrderId">Order whose payment must be refunded.</param>
/// <param name="OccurredAt">When the refund was requested.</param>
/// <param name="PaymentId">Payment to refund, from <see cref="PaymentCharged"/>.</param>
/// <param name="Amount">Amount to refund.</param>
/// <param name="Currency">ISO 4217 currency code.</param>
/// <param name="Reason">Why the payment is refunded; stored on the payment.</param>
public sealed record RefundPaymentRequested(
    Guid MessageId,
    string CorrelationId,
    Guid OrderId,
    DateTimeOffset OccurredAt,
    Guid PaymentId,
    decimal Amount,
    string Currency,
    string Reason) : IIntegrationEvent;

/// <summary>
/// Published by Payment.Api after refunding a charge. Refunds ignore the failure mode so
/// compensation always completes.
/// </summary>
/// <param name="MessageId">Unique id of this message.</param>
/// <param name="CorrelationId">Correlation id of the place-order request.</param>
/// <param name="OrderId">Order whose payment was refunded.</param>
/// <param name="OccurredAt">When the refund completed.</param>
/// <param name="PaymentId">Refunded payment.</param>
/// <param name="Amount">Amount refunded.</param>
/// <param name="Currency">ISO 4217 currency code.</param>
/// <param name="Reason">Reason copied from <see cref="RefundPaymentRequested"/>.</param>
public sealed record PaymentRefunded(
    Guid MessageId,
    string CorrelationId,
    Guid OrderId,
    DateTimeOffset OccurredAt,
    Guid PaymentId,
    decimal Amount,
    string Currency,
    string Reason) : IIntegrationEvent;
