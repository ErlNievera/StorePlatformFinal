using Microsoft.Extensions.Options;
using Order.Api.Saga;
using Store.Contracts.Events;
using Store.Messaging;

namespace Order.Api.Messaging;

/// <summary>Compensation step finished: payment refunded. Queue <c>order.payment-refunded</c>.</summary>
public sealed class PaymentRefundedConsumer(
    IRabbitMqConnection connection,
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<PaymentRefundedConsumer> logger)
    : EventConsumer<PaymentRefunded>(connection, scopeFactory, options, logger)
{
    /// <summary>Queue name.</summary>
    public const string Queue = "order.payment-refunded";

    /// <inheritdoc />
    protected override string QueueName => Queue;

    /// <inheritdoc />
    protected override Task HandleAsync(PaymentRefunded message, IServiceProvider services, CancellationToken cancellationToken) =>
        services.GetRequiredService<OrderSagaOrchestrator>().HandleAsync(message, ConsumerName, cancellationToken);
}
