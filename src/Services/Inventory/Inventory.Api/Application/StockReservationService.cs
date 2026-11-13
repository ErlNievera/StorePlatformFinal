using Inventory.Api.Data;
using Inventory.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Store.Contracts.Events;
using Store.Messaging;

namespace Inventory.Api.Application;

/// <summary>
/// Saga steps owned by Inventory: reserve stock for a placed order (all-or-nothing) and release
/// it again as compensation. Methods that change state only modify the change tracker; the
/// caller saves them in the same transaction as the <c>ProcessedMessages</c> row.
/// </summary>
public sealed class StockReservationService(InventoryDbContext db, TimeProvider clock)
{
    /// <summary>Reason used when one or more lines cannot be reserved.</summary>
    public const string InsufficientStockReason = "Insufficient stock";

    /// <summary>
    /// Reserves every line of <paramref name="order"/>, or nothing at all when any product is short.
    /// Returns the outcome to publish: <see cref="StockReserved"/> or <see cref="StockReservationFailed"/>.
    /// </summary>
    public async Task<IIntegrationEvent> ReserveAsync(OrderPlaced order, CancellationToken cancellationToken)
    {
        var existing = await FindReservationAsync(order.OrderId, cancellationToken);
        if (existing is not null)
        {
            // A different message for the same order already reserved it: report the same result.
            return Reserved(order, existing);
        }

        var requested = RequestedQuantities(order);
        var productIds = requested.Keys.ToList();
        var items = await db.StockItems
            .Where(s => productIds.Contains(s.ProductId))
            .ToDictionaryAsync(s => s.ProductId, cancellationToken);

        var shortages = FindShortages(requested, items);
        if (shortages.Count > 0)
        {
            return Failed(order, shortages);
        }

        var now = clock.GetUtcNow();
        foreach (var (productId, quantity) in requested)
        {
            items[productId].Reserve(quantity, now);
        }

        var reservation = new Reservation(
            order.OrderId,
            requested.Select(pair => new ReservationLine(pair.Key, pair.Value)),
            now);
        db.Reservations.Add(reservation);

        return Reserved(order, reservation);
    }

    /// <summary>
    /// Rebuilds the outcome of an already processed <see cref="OrderPlaced"/> without changing
    /// anything, so it can be published again after a redelivery.
    /// </summary>
    public async Task<IIntegrationEvent> DescribeReservationAsync(OrderPlaced order, CancellationToken cancellationToken)
    {
        var existing = await FindReservationAsync(order.OrderId, cancellationToken);
        if (existing is not null)
        {
            return Reserved(order, existing);
        }

        var requested = RequestedQuantities(order);
        var productIds = requested.Keys.ToList();
        var items = await db.StockItems
            .AsNoTracking()
            .Where(s => productIds.Contains(s.ProductId))
            .ToDictionaryAsync(s => s.ProductId, cancellationToken);
        return Failed(order, FindShortages(requested, items));
    }

    /// <summary>
    /// Returns an order's reserved units to available stock. Releasing an order that has no
    /// active reservation changes nothing and still reports <see cref="StockReleased"/>, so the
    /// saga can always continue.
    /// </summary>
    public async Task<StockReleased> ReleaseAsync(StockReleaseRequested request, CancellationToken cancellationToken)
    {
        var reservation = await db.Reservations
            .Include(r => r.Lines)
            .SingleOrDefaultAsync(r => r.OrderId == request.OrderId, cancellationToken);

        if (reservation is null || reservation.Status == ReservationStatus.Released)
        {
            return Released(request, reservation);
        }

        var productIds = reservation.Lines.Select(l => l.ProductId).ToList();
        var items = await db.StockItems
            .Where(s => productIds.Contains(s.ProductId))
            .ToDictionaryAsync(s => s.ProductId, cancellationToken);

        var now = clock.GetUtcNow();
        foreach (var line in reservation.Lines)
        {
            items[line.ProductId].Release(line.Quantity, now);
        }

        reservation.MarkReleased(request.Reason, now);
        return Released(request, reservation);
    }

    /// <summary>Rebuilds the outcome of an already processed <see cref="StockReleaseRequested"/>.</summary>
    public async Task<StockReleased> DescribeReleaseAsync(StockReleaseRequested request, CancellationToken cancellationToken)
    {
        var reservation = await FindReservationAsync(request.OrderId, cancellationToken);
        return Released(request, reservation);
    }

    private Task<Reservation?> FindReservationAsync(Guid orderId, CancellationToken cancellationToken) =>
        db.Reservations
            .AsNoTracking()
            .Include(r => r.Lines)
            .SingleOrDefaultAsync(r => r.OrderId == orderId, cancellationToken);

    private static Dictionary<Guid, int> RequestedQuantities(OrderPlaced order) =>
        order.Lines
            .GroupBy(l => l.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

    private static List<StockShortage> FindShortages(
        IReadOnlyDictionary<Guid, int> requested,
        IReadOnlyDictionary<Guid, StockItem> items)
    {
        var shortages = new List<StockShortage>();
        foreach (var (productId, quantity) in requested)
        {
            var available = items.TryGetValue(productId, out var item) ? item.QuantityAvailable : 0;
            if (quantity <= 0 || available < quantity)
            {
                shortages.Add(new StockShortage(productId, quantity, available));
            }
        }

        return shortages;
    }

    private StockReserved Reserved(OrderPlaced order, Reservation reservation) =>
        new(
            MessageIds.DeriveFrom(order.MessageId, nameof(StockReserved)),
            order.CorrelationId,
            order.OrderId,
            clock.GetUtcNow(),
            reservation.Lines.Select(l => new StockLine(l.ProductId, l.Quantity)).ToList());

    private StockReservationFailed Failed(OrderPlaced order, IReadOnlyList<StockShortage> shortages) =>
        new(
            MessageIds.DeriveFrom(order.MessageId, nameof(StockReservationFailed)),
            order.CorrelationId,
            order.OrderId,
            clock.GetUtcNow(),
            shortages.Count switch
            {
                0 => InsufficientStockReason,
                1 => $"{InsufficientStockReason}: requested {shortages[0].Requested}, available {shortages[0].Available}",
                _ => $"{InsufficientStockReason} for {shortages.Count} products",
            },
            shortages);

    private StockReleased Released(StockReleaseRequested request, Reservation? reservation) =>
        new(
            MessageIds.DeriveFrom(request.MessageId, nameof(StockReleased)),
            request.CorrelationId,
            request.OrderId,
            clock.GetUtcNow(),
            request.Reason,
            reservation is null
                ? []
                : reservation.Lines.Select(l => new StockLine(l.ProductId, l.Quantity)).ToList());
}
