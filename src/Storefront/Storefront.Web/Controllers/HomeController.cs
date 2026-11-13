using Microsoft.AspNetCore.Mvc;
using Storefront.Web.Clients;
using Storefront.Web.Clients.Catalog;
using Storefront.Web.Clients.Inventory;
using Storefront.Web.Models;

namespace Storefront.Web.Controllers;

/// <summary>Public catalog: product grid and product pages (GET /catalog/v1/products, /inventory/v1/stock).</summary>
public sealed class HomeController(ICatalogClient catalog, IInventoryClient inventory, ILogger<HomeController> logger) : Controller
{
    /// <summary>Catalog grid of active products with availability.</summary>
    [HttpGet("/")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            var productsTask = catalog.ListProductsAsync(includeInactive: false, cancellationToken);
            var stockTask = TryLoadStockAsync(cancellationToken);
            var products = await productsTask;
            var stock = await stockTask;

            var cards = products
                .Select(p => new ProductCard(p, stock?.GetValueOrDefault(p.ProductId)))
                .ToList();
            return View(new CatalogViewModel(cards, stock is null ? "Stock levels are unavailable right now; you can still browse and order." : null));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Catalog could not be loaded");
            return View(new CatalogViewModel([], ApiErrors.Describe(ex)));
        }
    }

    /// <summary>One product with its availability.</summary>
    [HttpGet("/products/{productId:guid}")]
    public async Task<IActionResult> Product(Guid productId, CancellationToken cancellationToken)
    {
        try
        {
            var product = await catalog.GetProductAsync(productId, cancellationToken);
            if (!product.IsActive && !User.IsInRole("Admin"))
            {
                return NotFound();
            }

            int? available = null;
            try
            {
                available = (await inventory.GetStockItemAsync(productId, cancellationToken)).QuantityAvailable;
            }
            catch (Exception ex) when (ex is not OperationCanceledException && ApiErrors.StatusOf(ex) == StatusCodes.Status404NotFound)
            {
                available = 0;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Stock for {ProductId} is unavailable", productId);
            }

            return View(new ProductDetailsViewModel(product, available));
        }
        catch (Exception ex) when (ex is not OperationCanceledException && ApiErrors.StatusOf(ex) == StatusCodes.Status404NotFound)
        {
            return NotFound();
        }
    }

    private async Task<Dictionary<Guid, int>?> TryLoadStockAsync(CancellationToken cancellationToken)
    {
        try
        {
            var items = await inventory.ListStockItemsAsync(cancellationToken);
            return items.ToDictionary(s => s.ProductId, s => s.QuantityAvailable);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Stock levels could not be loaded");
            return null;
        }
    }
}
