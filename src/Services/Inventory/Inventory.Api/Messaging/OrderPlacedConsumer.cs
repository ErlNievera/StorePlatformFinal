using Inventory.Api.Application;
using Inventory.Api.Data;
using Microsoft.Extensions.Options;
using Store.Contracts.Events;
using Store.Messaging;

namespace Inventory.Api.Messaging;

/// <summary>
/// Saga step 1: reserves stock for a placed order and publishes <see cref="StockReserved"/> or
/// <see cref="StockReservationFailed"/>. Queue <c>inventory.order-placed</c>.
/// </summary>
public sealed class OrderPlacedConsumer(
    IRabbitMqConnection connection,
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<OrderPlacedConsumer> logger)
    : EventConsumer<OrderPlaced>(connection, scopeFactory, options, logger)
{
    /// <summary>Queue name.</summary>
    public const string Queue = "inventory.order-placed";

    /// <inheritdoc />
    protected override string QueueName => Queue;

    /// <inheritdoc />
    protected override async Task HandleAsync(OrderPlaced message, IServiceProvider services, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<InventoryDbContext>();
        var reservations = services.GetRequiredService<StockReservationService>();
        var publisher = services.GetRequiredService<IEventPublisher>();

        IIntegrationEvent? outcome = null;
        var applied = await db.ExecuteIdempotentAsync(
            message,
            ConsumerName,
            async ct => outcome = await reservations.ReserveAsync(message, ct),
            cancellationToken);

        if (!applied)
        {
            Logger.LogInformation(
                "OrderPlaced {MessageId} for order {OrderId} was already processed; republishing its outcome",
                message.MessageId, message.OrderId);
            outcome = await reservations.DescribeReservationAsync(message, cancellationToken);
        }

        switch (outcome)
        {
            case StockReserved reserved:
                Logger.LogInformation(
                    "Reserved {LineCount} product line(s) for order {OrderId}", reserved.Lines.Count, message.OrderId);
                await publisher.PublishAsync(reserved, cancellationToken);
                break;
            case StockReservationFailed failed:
                Logger.LogWarning(
                    "Could not reserve stock for order {OrderId}: {Reason}", message.OrderId, failed.Reason);
                await publisher.PublishAsync(failed, cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"Unexpected reservation outcome for order {message.OrderId}.");
        }
    }
}
