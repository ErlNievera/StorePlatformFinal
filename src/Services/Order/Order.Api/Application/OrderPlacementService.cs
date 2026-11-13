using Order.Api.Clients;
using Order.Api.Contracts;
using Order.Api.Data;
using Order.Api.Domain;
using Order.Api.Saga;
using Store.Messaging;
using Store.Observability;

namespace Order.Api.Application;

/// <summary>
/// Validates an order against Catalog, snapshots names and prices onto the lines, stores the
/// order in <see cref="OrderStatus.Pending"/> together with its saga state, and starts the saga by
/// publishing <c>OrderPlaced</c>.
/// </summary>
public sealed class OrderPlacementService(
    OrderDbContext db,
    ICatalogProducts catalog,
    ICorrelationIdAccessor correlation,
    IEventPublisher publisher,
    TimeProvider clock,
    ILogger<OrderPlacementService> logger)
{
    /// <summary>
    /// Places the order. Throws <see cref="NotFoundException"/> for an unknown product,
    /// <see cref="ConflictException"/> for an inactive product or mixed currencies, and lets
    /// <see cref="DownstreamServiceException"/> through when Catalog is unavailable.
    /// </summary>
    public async Task<CustomerOrder> PlaceAsync(Guid customerId, PlaceOrderRequest request, CancellationToken cancellationToken)
    {
        var requested = request.Lines
            .GroupBy(l => l.ProductId!.Value)
            .Select(g => (ProductId: g.Key, Quantity: g.Sum(l => l.Quantity)))
            .ToList();

        var lines = new List<OrderLine>(requested.Count);
        string? currency = null;
        foreach (var (productId, quantity) in requested)
        {
            var lookup = await catalog.FindProductAsync(productId, cancellationToken);
            var product = lookup.Product ?? throw new NotFoundException($"Product {productId} does not exist.");
            if (!product.IsActive)
            {
                throw new ConflictException($"{product.Name} is no longer available.");
            }

            currency ??= product.Currency;
            if (!string.Equals(currency, product.Currency, StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictException("All products in one order must use the same currency.");
            }

            lines.Add(new OrderLine(product.ProductId, product.Name, product.Price, quantity));
        }

        var now = clock.GetUtcNow();
        var order = CustomerOrder.Place(customerId, lines, currency!, now);
        var correlationId = correlation.CorrelationId ?? Correlation.NewId();
        var saga = new OrderSagaState(order.OrderId, correlationId, now);
        db.Orders.Add(order);
        db.SagaStates.Add(saga);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            await publisher.PublishAsync(SagaMessages.OrderPlaced(order, saga, clock.GetUtcNow()), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The order is saved; the stalled-saga monitor publishes OrderPlaced again shortly.
            logger.LogWarning(ex, "OrderPlaced for order {OrderId} could not be published now; it will be retried", order.OrderId);
        }

        logger.LogInformation(
            "Placed order {OrderId} for customer {CustomerId}: {LineCount} line(s), total {Total} {Currency}",
            order.OrderId, customerId, order.Lines.Count, order.TotalAmount, order.Currency);
        return order;
    }
}
