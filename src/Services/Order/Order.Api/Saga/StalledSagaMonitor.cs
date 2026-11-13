using Microsoft.EntityFrameworkCore;
using Order.Api.Data;
using Order.Api.Domain;
using Store.Messaging;

namespace Order.Api.Saga;

/// <summary>
/// Safety net for "committed but not published": every <see cref="Interval"/> it finds sagas that
/// have waited longer than <see cref="StallAfter"/> and publishes their current command again.
/// The command keeps its derived MessageId, so a service that already handled it recognises the
/// duplicate and simply republishes its outcome.
/// </summary>
public sealed class StalledSagaMonitor(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<StalledSagaMonitor> logger) : BackgroundService
{
    /// <summary>How often the monitor looks for stalled sagas.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    /// <summary>How long a saga may wait for a reply before its command is published again.</summary>
    public static readonly TimeSpan StallAfter = TimeSpan.FromSeconds(30);

    private const int BatchSize = 50;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RepublishStalledAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Stalled-saga check failed; trying again in {IntervalSeconds}s", Interval.TotalSeconds);
            }
        }
    }

    private async Task RepublishStalledAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

        var cutoff = clock.GetUtcNow() - StallAfter;
        var stalled = await db.SagaStates
            .Where(s => (s.Step == SagaStep.AwaitingStockReservation || s.Step == SagaStep.AwaitingPayment || s.Step == SagaStep.Compensating)
                && s.UpdatedAt < cutoff)
            .OrderBy(s => s.UpdatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var saga in stalled)
        {
            var order = await db.Orders.AsNoTracking()
                .Include(o => o.Lines)
                .SingleAsync(o => o.OrderId == saga.OrderId, cancellationToken);
            var now = clock.GetUtcNow();
            var commands = SagaMessages.PendingCommands(order, saga, now);

            saga.MarkRepublished(now);
            await db.SaveChangesAsync(cancellationToken);

            foreach (var command in commands)
            {
                logger.LogWarning(
                    "Saga for order {OrderId} waited over {StallSeconds}s at {Step}; republishing {Command} (attempt {Count})",
                    saga.OrderId, StallAfter.TotalSeconds, saga.Step, command.GetType().Name, saga.RepublishCount);
                await publisher.PublishAsync(command, cancellationToken);
            }
        }
    }
}
