using Microsoft.EntityFrameworkCore;
using Order.Api.Data;
using Order.Api.Domain;
using Store.Contracts.Events;
using Store.Messaging;

namespace Order.Api.Saga;

/// <summary>
/// The orchestrated saga: reacts to each reply from Inventory and Payment, moves the order and
/// its saga state forward in one local transaction (together with the ProcessedMessages row), and
/// then publishes the next command.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>StockReserved → StockReserved status, ChargePaymentRequested.</item>
/// <item>StockReservationFailed → Cancelled (nothing to compensate).</item>
/// <item>PaymentCharged → Confirmed; or, with FailAfterPayment on, Cancelling with
/// RefundPaymentRequested and StockReleaseRequested.</item>
/// <item>PaymentFailed → Cancelling with StockReleaseRequested.</item>
/// <item>StockReleased / PaymentRefunded → compensation step recorded; Cancelled when none is left.</item>
/// </list>
/// A reply that does not fit the saga's current step (a late duplicate) is logged and ignored.
/// </remarks>
public sealed class OrderSagaOrchestrator(
    OrderDbContext db,
    IEventPublisher publisher,
    TimeProvider clock,
    ILogger<OrderSagaOrchestrator> logger)
{
    /// <summary>Reason recorded when FailAfterPayment is on.</summary>
    public const string FailAfterPaymentReason = "Order failed after payment (FailAfterPayment mode is on)";

    /// <summary>Handles <see cref="StockReserved"/>.</summary>
    public Task HandleAsync(StockReserved message, string consumer, CancellationToken cancellationToken) =>
        RunStepAsync(message, consumer, SagaStep.AwaitingStockReservation, (order, saga, now, outgoing) =>
        {
            order.MarkStockReserved(now);
            saga.AwaitPayment(now);
            outgoing.Add(SagaMessages.ChargePayment(order, saga, now));
            return Task.CompletedTask;
        }, cancellationToken);

    /// <summary>Handles <see cref="StockReservationFailed"/>.</summary>
    public Task HandleAsync(StockReservationFailed message, string consumer, CancellationToken cancellationToken) =>
        RunStepAsync(message, consumer, SagaStep.AwaitingStockReservation, (order, saga, now, outgoing) =>
        {
            order.Cancel(message.Reason, now);
            saga.MarkCancelled(message.Reason, now);
            outgoing.Add(SagaMessages.Cancelled(order, saga, now));
            return Task.CompletedTask;
        }, cancellationToken);

    /// <summary>Handles <see cref="PaymentCharged"/>, honouring the FailAfterPayment switch.</summary>
    public Task HandleAsync(PaymentCharged message, string consumer, CancellationToken cancellationToken) =>
        RunStepAsync(message, consumer, SagaStep.AwaitingPayment, async (order, saga, now, outgoing) =>
        {
            order.MarkPaymentCharged(now);

            var settings = await db.Settings.AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == OrderSettings.SingletonId, cancellationToken);
            if (settings?.FailureMode == OrderFailureMode.FailAfterPayment)
            {
                order.StartCancellation(FailAfterPaymentReason, now);
                saga.RecordPayment(message.PaymentId, now);
                saga.BeginCompensation(FailAfterPaymentReason, releaseStock: true, refundPayment: true, now);
                outgoing.Add(SagaMessages.RefundPayment(order, saga, now));
                outgoing.Add(SagaMessages.ReleaseStock(order, saga, now));
                return;
            }

            order.Confirm(now);
            saga.Complete(message.PaymentId, now);
            outgoing.Add(SagaMessages.Confirmed(order, saga, now));
        }, cancellationToken);

    /// <summary>Handles <see cref="PaymentFailed"/>.</summary>
    public Task HandleAsync(PaymentFailed message, string consumer, CancellationToken cancellationToken) =>
        RunStepAsync(message, consumer, SagaStep.AwaitingPayment, (order, saga, now, outgoing) =>
        {
            var reason = message.Reason.StartsWith("Payment declined", StringComparison.OrdinalIgnoreCase)
                ? message.Reason
                : $"Payment declined: {message.Reason}";
            order.StartCancellation(reason, now);
            saga.BeginCompensation(reason, releaseStock: true, refundPayment: false, now);
            outgoing.Add(SagaMessages.ReleaseStock(order, saga, now));
            return Task.CompletedTask;
        }, cancellationToken);

    /// <summary>Handles <see cref="StockReleased"/>.</summary>
    public Task HandleAsync(StockReleased message, string consumer, CancellationToken cancellationToken) =>
        RunStepAsync(message, consumer, SagaStep.Compensating, (order, saga, now, outgoing) =>
        {
            if (!saga.StockReleasePending)
            {
                logger.LogInformation("Order {OrderId} was not waiting for a stock release; ignoring", order.OrderId);
                return Task.CompletedTask;
            }

            saga.StockReleased(now);
            var units = message.Lines.Sum(l => l.Quantity);
            order.RecordCompensation($"Stock released: {units} unit(s) returned to inventory", now);
            FinishCompensationIfDone(order, saga, now, outgoing);
            return Task.CompletedTask;
        }, cancellationToken);

    /// <summary>Handles <see cref="PaymentRefunded"/>.</summary>
    public Task HandleAsync(PaymentRefunded message, string consumer, CancellationToken cancellationToken) =>
        RunStepAsync(message, consumer, SagaStep.Compensating, (order, saga, now, outgoing) =>
        {
            if (!saga.RefundPending)
            {
                logger.LogInformation("Order {OrderId} was not waiting for a refund; ignoring", order.OrderId);
                return Task.CompletedTask;
            }

            saga.PaymentRefunded(now);
            order.RecordCompensation($"Payment refunded: {message.Amount:0.00} {message.Currency}", now);
            FinishCompensationIfDone(order, saga, now, outgoing);
            return Task.CompletedTask;
        }, cancellationToken);

    private static void FinishCompensationIfDone(CustomerOrder order, OrderSagaState saga, DateTimeOffset now, List<IIntegrationEvent> outgoing)
    {
        if (!saga.CompensationFinished)
        {
            return;
        }

        var reason = saga.FailureReason ?? "Order cancelled";
        order.Cancel(reason, now);
        saga.MarkCancelled(reason, now);
        outgoing.Add(SagaMessages.Cancelled(order, saga, now));
    }

    private async Task RunStepAsync(
        IIntegrationEvent message,
        string consumer,
        SagaStep expectedStep,
        Func<CustomerOrder, OrderSagaState, DateTimeOffset, List<IIntegrationEvent>, Task> step,
        CancellationToken cancellationToken)
    {
        var outgoing = new List<IIntegrationEvent>();
        var applied = await db.ExecuteIdempotentAsync(message, consumer, async ct =>
        {
            outgoing.Clear();
            var saga = await db.SagaStates.SingleOrDefaultAsync(s => s.OrderId == message.OrderId, ct)
                ?? throw new InvalidOperationException($"No saga exists for order {message.OrderId}.");
            if (saga.Step != expectedStep)
            {
                logger.LogInformation(
                    "Ignoring {EventType} for order {OrderId}: saga is {Step}, expected {ExpectedStep}",
                    message.GetType().Name, message.OrderId, saga.Step, expectedStep);
                return;
            }

            var order = await db.Orders
                .Include(o => o.Lines)
                .Include(o => o.History)
                .AsSplitQuery()
                .SingleAsync(o => o.OrderId == message.OrderId, ct);
            var before = order.Status;
            await step(order, saga, clock.GetUtcNow(), outgoing);

            if (order.Status != before)
            {
                logger.LogInformation(
                    "Order {OrderId}: {From} -> {To} after {EventType}",
                    order.OrderId, before, order.Status, message.GetType().Name);
            }
        }, cancellationToken);

        if (!applied)
        {
            logger.LogInformation(
                "{EventType} {MessageId} for order {OrderId} was already processed",
                message.GetType().Name, message.MessageId, message.OrderId);
            return;
        }

        foreach (var command in outgoing)
        {
            await publisher.PublishAsync(command, cancellationToken);
        }
    }
}
