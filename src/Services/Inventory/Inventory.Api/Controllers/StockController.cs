using Asp.Versioning;
using Inventory.Api.Clients;
using Inventory.Api.Contracts;
using Inventory.Api.Data;
using Inventory.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Observability;
using Store.Observability.Security;

namespace Inventory.Api.Controllers;

/// <summary>Stock levels and admin adjustments (inventory.v1.yaml, tag Stock).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/stock")]
[Produces("application/json")]
public sealed class StockController(
    InventoryDbContext db,
    ICatalogProducts catalog,
    TimeProvider clock,
    ILogger<StockController> logger) : ControllerBase
{
    /// <summary>operationId <c>listStockItems</c>.</summary>
    [HttpGet(Name = "listStockItems")]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<StockItemResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StockItemResponse>>> ListStockItems(CancellationToken cancellationToken)
    {
        var items = await db.StockItems.AsNoTracking().OrderBy(s => s.ProductId).ToListAsync(cancellationToken);
        return Ok(items.Select(StockItemResponse.From).ToList());
    }

    /// <summary>operationId <c>getStockItem</c>.</summary>
    [HttpGet("{productId:guid}", Name = "getStockItem")]
    [AllowAnonymous]
    [ProducesResponseType<StockItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockItemResponse>> GetStockItem(Guid productId, CancellationToken cancellationToken)
    {
        var item = await db.StockItems.AsNoTracking().SingleOrDefaultAsync(s => s.ProductId == productId, cancellationToken)
            ?? throw new NotFoundException($"No stock record exists for product {productId}.");
        return Ok(StockItemResponse.From(item));
    }

    /// <summary>
    /// operationId <c>adjustStock</c> (Admin). A product without a stock record is first checked
    /// against Catalog (404 if unknown, 503 if Catalog is down) and then created with zero units.
    /// </summary>
    [HttpPost("{productId:guid}/adjustments", Name = "adjustStock")]
    [Authorize(Roles = StoreClaims.Roles.Admin)]
    [ProducesResponseType<StockItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<StockItemResponse>> AdjustStock(
        Guid productId,
        StockAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var item = await db.StockItems.SingleOrDefaultAsync(s => s.ProductId == productId, cancellationToken);
        if (item is null)
        {
            var lookup = await catalog.FindProductAsync(productId, cancellationToken);
            if (!lookup.Found)
            {
                throw new NotFoundException($"Product {productId} does not exist in Catalog.");
            }

            item = new StockItem(productId, quantityOnHand: 0, now);
            db.StockItems.Add(item);
            logger.LogInformation("Created stock record for Catalog product {ProductId} ({Name})", productId, lookup.Product!.Name);
        }

        try
        {
            item.Adjust(request.Delta, now);
        }
        catch (StockConflictException ex)
        {
            throw new ConflictException(ex.Message);
        }

        var adjustedBy = User.FindFirst(StoreClaims.Email)?.Value ?? User.GetUserId()?.ToString();
        db.StockAdjustments.Add(new StockAdjustment(item, request.Delta, request.Reason, adjustedBy, now));
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Adjusted stock of {ProductId} by {Delta} ({Reason}); on hand {OnHand}, reserved {Reserved}",
            productId, request.Delta, request.Reason, item.QuantityOnHand, item.QuantityReserved);
        return Ok(StockItemResponse.From(item));
    }
}
