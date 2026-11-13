using Microsoft.Extensions.Options;
using Order.Api.Saga;
using Store.Contracts.Events;
using Store.Messaging;

namespace Order.Api.Messaging;

/// <summary>Not enough stock: cancel the order (nothing to compensate). Queue <c>order.stock-reservation-failed</c>.</summary>
public sealed class StockReservationFailedConsumer(
    IRabbitMqConnection connection,
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<StockReservationFailedConsumer> logger)
    : EventConsumer<StockReservationFailed>(connection, scopeFactory, options, logger)
{
    /// <summary>Queue name.</summary>
    public const string Queue = "order.stock-reservation-failed";

    /// <inheritdoc />
    protected override string QueueName => Queue;

    /// <inheritdoc />
    protected override Task HandleAsync(StockReservationFailed message, IServiceProvider services, CancellationToken cancellationToken) =>
        services.GetRequiredService<OrderSagaOrchestrator>().HandleAsync(message, ConsumerName, cancellationToken);
}
