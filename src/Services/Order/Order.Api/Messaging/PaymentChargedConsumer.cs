using Microsoft.Extensions.Options;
using Order.Api.Saga;
using Store.Contracts.Events;
using Store.Messaging;

namespace Order.Api.Messaging;

/// <summary>Payment taken: confirm the order, or compensate when FailAfterPayment is on. Queue <c>order.payment-charged</c>.</summary>
public sealed class PaymentChargedConsumer(
    IRabbitMqConnection connection,
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<PaymentChargedConsumer> logger)
    : EventConsumer<PaymentCharged>(connection, scopeFactory, options, logger)
{
    /// <summary>Queue name.</summary>
    public const string Queue = "order.payment-charged";

    /// <inheritdoc />
    protected override string QueueName => Queue;

    /// <inheritdoc />
    protected override Task HandleAsync(PaymentCharged message, IServiceProvider services, CancellationToken cancellationToken) =>
        services.GetRequiredService<OrderSagaOrchestrator>().HandleAsync(message, ConsumerName, cancellationToken);
}
