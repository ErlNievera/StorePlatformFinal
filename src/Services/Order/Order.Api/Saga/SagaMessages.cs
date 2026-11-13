using Order.Api.Domain;
using Store.Contracts.Events;
using Store.Messaging;

namespace Order.Api.Saga;

/// <summary>
/// Builds the commands and outcomes the saga publishes. Message ids are derived from the order
/// id and the message type, so a command republished by the stalled-saga monitor carries the
/// same <c>MessageId</c> and is recognised as a duplicate by the receiving service.
/// </summary>
public static class SagaMessages
{
    /// <summary>Starts the saga: Inventory reserves stock.</summary>
    public static OrderPlaced OrderPlaced(CustomerOrder order, OrderSagaState saga, DateTimeOffset now) => new(
        Id(order.OrderId, nameof(Store.Contracts.Events.OrderPlaced)),
        saga.CorrelationId,
        order.OrderId,
        now,
        order.CustomerId,
        order.Lines.Select(l => new OrderLineItem(l.ProductId, l.ProductName, l.Quantity, l.UnitPrice)).ToList(),
        order.TotalAmount,
        order.Currency);

    /// <summary>Stock is reserved: Payment charges the order total.</summary>
    public static ChargePaymentRequested ChargePayment(CustomerOrder order, OrderSagaState saga, DateTimeOffset now) => new(
        Id(order.OrderId, nameof(ChargePaymentRequested)),
        saga.CorrelationId,
        order.OrderId,
        now,
        order.CustomerId,
        order.TotalAmount,
        order.Currency);

    /// <summary>Compensation: Inventory returns the reserved stock.</summary>
    public static StockReleaseRequested ReleaseStock(CustomerOrder order, OrderSagaState saga, DateTimeOffset now) => new(
        Id(order.OrderId, nameof(StockReleaseRequested)),
        saga.CorrelationId,
        order.OrderId,
        now,
        saga.FailureReason ?? "Order cancelled");

    /// <summary>Compensation: Payment refunds the charge.</summary>
    public static RefundPaymentRequested RefundPayment(CustomerOrder order, OrderSagaState saga, DateTimeOffset now) => new(
        Id(order.OrderId, nameof(RefundPaymentRequested)),
        saga.CorrelationId,
        order.OrderId,
        now,
        saga.PaymentId ?? throw new InvalidOperationException($"Order {order.OrderId} has no payment to refund."),
        order.TotalAmount,
        order.Currency,
        saga.FailureReason ?? "Order cancelled");

    /// <summary>The saga completed successfully.</summary>
    public static OrderConfirmed Confirmed(CustomerOrder order, OrderSagaState saga, DateTimeOffset now) => new(
        Id(order.OrderId, nameof(OrderConfirmed)),
        saga.CorrelationId,
        order.OrderId,
        now,
        order.CustomerId,
        saga.PaymentId ?? Guid.Empty,
        order.TotalAmount,
        order.Currency);

    /// <summary>The saga ended with the order cancelled.</summary>
    public static OrderCancelled Cancelled(CustomerOrder order, OrderSagaState saga, DateTimeOffset now) => new(
        Id(order.OrderId, nameof(OrderCancelled)),
        saga.CorrelationId,
        order.OrderId,
        now,
        order.CustomerId,
        order.CancellationReason ?? saga.FailureReason ?? "Order cancelled");

    /// <summary>
    /// Commands to publish again for a saga that is still waiting: the one that matches its step,
    /// or the outstanding compensations.
    /// </summary>
    public static IReadOnlyList<IIntegrationEvent> PendingCommands(CustomerOrder order, OrderSagaState saga, DateTimeOffset now)
    {
        var commands = new List<IIntegrationEvent>();
        switch (saga.Step)
        {
            case SagaStep.AwaitingStockReservation:
                commands.Add(OrderPlaced(order, saga, now));
                break;
            case SagaStep.AwaitingPayment:
                commands.Add(ChargePayment(order, saga, now));
                break;
            case SagaStep.Compensating:
                if (saga.RefundPending)
                {
                    commands.Add(RefundPayment(order, saga, now));
                }

                if (saga.StockReleasePending)
                {
                    commands.Add(ReleaseStock(order, saga, now));
                }

                break;
        }

        return commands;
    }

    private static Guid Id(Guid orderId, string messageType) => MessageIds.DeriveFrom(orderId, messageType);
}
