using Inventory.Api.Application;
using Inventory.Api.Data;
using Microsoft.Extensions.Options;
using Store.Contracts.Events;
using Store.Messaging;

namespace Inventory.Api.Messaging;

/// <summary>
/// Compensation: returns an order's reserved stock and publishes <see cref="StockReleased"/>.
/// Queue <c>inventory.stock-release-requested</c>.
/// </summary>
public sealed class StockReleaseRequestedConsumer(
    IRabbitMqConnection connection,
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<StockReleaseRequestedConsumer> logger)
    : EventConsumer<StockReleaseRequested>(connection, scopeFactory, options, logger)
{
    /// <summary>Queue name.</summary>
    public const string Queue = "inventory.stock-release-requested";

    /// <inheritdoc />
    protected override string QueueName => Queue;

    /// <inheritdoc />
    protected override async Task HandleAsync(StockReleaseRequested message, IServiceProvider services, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<InventoryDbContext>();
        var reservations = services.GetRequiredService<StockReservationService>();
        var publisher = services.GetRequiredService<IEventPublisher>();

        StockReleased? released = null;
        var applied = await db.ExecuteIdempotentAsync(
            message,
            ConsumerName,
            async ct => released = await reservations.ReleaseAsync(message, ct),
            cancellationToken);

        if (!applied || released is null)
        {
            Logger.LogInformation(
                "StockReleaseRequested {MessageId} for order {OrderId} was already processed; republishing its outcome",
                message.MessageId, message.OrderId);
            released = await reservations.DescribeReleaseAsync(message, cancellationToken);
        }

        Logger.LogInformation(
            "Released {LineCount} product line(s) for order {OrderId}: {Reason}",
            released.Lines.Count, message.OrderId, message.Reason);
        await publisher.PublishAsync(released, cancellationToken);
    }
}
