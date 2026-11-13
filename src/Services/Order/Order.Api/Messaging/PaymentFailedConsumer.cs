using Microsoft.Extensions.Options;
using Order.Api.Saga;
using Store.Contracts.Events;
using Store.Messaging;

namespace Order.Api.Messaging;

/// <summary>Payment declined: release the reserved stock. Queue <c>order.payment-failed</c>.</summary>
public sealed class PaymentFailedConsumer(
    IRabbitMqConnection connection,
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<PaymentFailedConsumer> logger)
    : EventConsumer<PaymentFailed>(connection, scopeFactory, options, logger)
{
    /// <summary>Queue name.</summary>
    public const string Queue = "order.payment-failed";

    /// <inheritdoc />
    protected override string QueueName => Queue;

    /// <inheritdoc />
    protected override Task HandleAsync(PaymentFailed message, IServiceProvider services, CancellationToken cancellationToken) =>
        services.GetRequiredService<OrderSagaOrchestrator>().HandleAsync(message, ConsumerName, cancellationToken);
}
