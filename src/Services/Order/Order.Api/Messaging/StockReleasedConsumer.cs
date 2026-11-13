using Microsoft.Extensions.Options;
using Order.Api.Saga;
using Store.Contracts.Events;
using Store.Messaging;

namespace Order.Api.Messaging;

/// <summary>Compensation step finished: stock returned. Queue <c>order.stock-released</c>.</summary>
public sealed class StockReleasedConsumer(
    IRabbitMqConnection connection,
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<StockReleasedConsumer> logger)
    : EventConsumer<StockReleased>(connection, scopeFactory, options, logger)
{
    /// <summary>Queue name.</summary>
    public const string Queue = "order.stock-released";

    /// <inheritdoc />
    protected override string QueueName => Queue;

    /// <inheritdoc />
    protected override Task HandleAsync(StockReleased message, IServiceProvider services, CancellationToken cancellationToken) =>
        services.GetRequiredService<OrderSagaOrchestrator>().HandleAsync(message, ConsumerName, cancellationToken);
}
