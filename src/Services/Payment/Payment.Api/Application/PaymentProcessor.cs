using Microsoft.EntityFrameworkCore;
using Payment.Api.Data;
using Payment.Api.Domain;
using Store.Contracts.Events;
using Store.Messaging;

namespace Payment.Api.Application;

/// <summary>
/// Saga steps owned by Payment: charge an order (honouring the failure mode) and refund it as
/// compensation. Methods that change state only modify the change tracker; the caller saves them
/// in the same transaction as the <c>ProcessedMessages</c> row.
/// </summary>
public sealed class PaymentProcessor(PaymentDbContext db, TimeProvider clock)
{
    /// <summary>Decline reason used while the failure mode is <see cref="PaymentFailureMode.DeclineCharge"/>.</summary>
    public const string DeclinedByFailureModeReason = "Payment declined: the DeclineCharge failure mode is on";

    /// <summary>
    /// Charges the order once. If a payment for the order already exists its result is reported
    /// again instead of charging twice. Returns <see cref="PaymentCharged"/> or <see cref="PaymentFailed"/>.
    /// </summary>
    public async Task<IIntegrationEvent> ChargeAsync(ChargePaymentRequested request, CancellationToken cancellationToken)
    {
        var existing = await db.Payments.AsNoTracking()
            .SingleOrDefaultAsync(p => p.OrderId == request.OrderId, cancellationToken);
        if (existing is not null)
        {
            return Outcome(request, existing);
        }

        var settings = await db.Settings.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == PaymentSettings.SingletonId, cancellationToken);
        var mode = settings?.FailureMode ?? PaymentFailureMode.None;
        var now = clock.GetUtcNow();

        var payment = mode == PaymentFailureMode.DeclineCharge
            ? OrderPayment.Decline(request.OrderId, request.CustomerId, request.Amount, request.Currency, DeclinedByFailureModeReason, now)
            : OrderPayment.Charge(request.OrderId, request.CustomerId, request.Amount, request.Currency, now);

        db.Payments.Add(payment);
        return Outcome(request, payment);
    }

    /// <summary>Rebuilds the outcome of an already processed charge request without changing anything.</summary>
    public async Task<IIntegrationEvent> DescribeChargeAsync(ChargePaymentRequested request, CancellationToken cancellationToken)
    {
        var payment = await db.Payments.AsNoTracking()
            .SingleOrDefaultAsync(p => p.OrderId == request.OrderId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Charge request {request.MessageId} is marked processed but order {request.OrderId} has no payment.");
        return Outcome(request, payment);
    }

    /// <summary>
    /// Refunds a charged payment (refunds ignore the failure mode). Refunding an already refunded
    /// payment reports success again.
    /// </summary>
    /// <exception cref="InvalidOperationException">The payment does not exist, belongs to another order, or was declined.</exception>
    public async Task<PaymentRefunded> RefundAsync(RefundPaymentRequested request, CancellationToken cancellationToken)
    {
        var payment = await FindForRefundAsync(request, tracked: true, cancellationToken);
        if (payment.Status == PaymentStatus.Charged)
        {
            payment.Refund(request.Reason, clock.GetUtcNow());
        }
        else if (payment.Status != PaymentStatus.Refunded)
        {
            throw new InvalidOperationException(
                $"Payment {payment.PaymentId} for order {request.OrderId} is {payment.Status} and cannot be refunded.");
        }

        return Refunded(request, payment);
    }

    /// <summary>Rebuilds the outcome of an already processed refund request.</summary>
    public async Task<PaymentRefunded> DescribeRefundAsync(RefundPaymentRequested request, CancellationToken cancellationToken)
    {
        var payment = await FindForRefundAsync(request, tracked: false, cancellationToken);
        return Refunded(request, payment);
    }

    private async Task<OrderPayment> FindForRefundAsync(RefundPaymentRequested request, bool tracked, CancellationToken cancellationToken)
    {
        var query = tracked ? db.Payments : db.Payments.AsNoTracking();
        var payment = await query.SingleOrDefaultAsync(p => p.PaymentId == request.PaymentId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Refund requested for unknown payment {request.PaymentId} (order {request.OrderId}).");

        if (payment.OrderId != request.OrderId)
        {
            throw new InvalidOperationException(
                $"Payment {request.PaymentId} belongs to order {payment.OrderId}, not {request.OrderId}.");
        }

        return payment;
    }

    private IIntegrationEvent Outcome(ChargePaymentRequested request, OrderPayment payment) =>
        payment.Status == PaymentStatus.Declined
            ? new PaymentFailed(
                MessageIds.DeriveFrom(request.MessageId, nameof(PaymentFailed)),
                request.CorrelationId,
                request.OrderId,
                clock.GetUtcNow(),
                payment.PaymentId,
                payment.FailureReason ?? "Payment declined")
            : new PaymentCharged(
                MessageIds.DeriveFrom(request.MessageId, nameof(PaymentCharged)),
                request.CorrelationId,
                request.OrderId,
                clock.GetUtcNow(),
                payment.PaymentId,
                payment.Amount,
                payment.Currency);

    private PaymentRefunded Refunded(RefundPaymentRequested request, OrderPayment payment) =>
        new(
            MessageIds.DeriveFrom(request.MessageId, nameof(PaymentRefunded)),
            request.CorrelationId,
            request.OrderId,
            clock.GetUtcNow(),
            payment.PaymentId,
            payment.Amount,
            payment.Currency,
            payment.RefundReason ?? request.Reason);
}
