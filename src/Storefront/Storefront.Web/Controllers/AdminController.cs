using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Storefront.Web.Clients;
using Storefront.Web.Clients.Catalog;
using Storefront.Web.Clients.Inventory;
using Storefront.Web.Clients.Orders;
using Storefront.Web.Clients.Payments;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Controllers;

/// <summary>Admin pages: products, stock, and chaos and saga testing (Admin routes on the gateway).</summary>
[Authorize(Roles = "Admin")]
[Route("admin")]
public sealed class AdminController(
    ICatalogClient catalog,
    IInventoryClient inventory,
    IOrderClient orders,
    IPaymentClient payments,
    ILogger<AdminController> logger) : Controller
{
    /// <summary>All products, including inactive ones, plus a create form.</summary>
    [HttpGet("products")]
    public async Task<IActionResult> Products(CancellationToken cancellationToken)
    {
        try
        {
            var products = await catalog.ListProductsAsync(includeInactive: true, cancellationToken);
            return View(new AdminProductsViewModel(products.ToList(), new ProductFormViewModel(), null));
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            return View(new AdminProductsViewModel([], new ProductFormViewModel(), ApiErrors.Describe(ex)));
        }
    }

    /// <summary>Creates a product (POST /catalog/v1/products).</summary>
    [HttpPost("products")]
    public async Task<IActionResult> CreateProduct([Bind(Prefix = "NewProduct")] ProductFormViewModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData.Error(FirstModelError() ?? "Check the product details and try again.");
            return RedirectToAction(nameof(Products));
        }

        try
        {
            var created = await catalog.CreateProductAsync(ToRequest(form), cancellationToken);
            TempData.Success($"Created {created.Name}. Add stock for it on the Stock page.");
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            TempData.Error(ApiErrors.Describe(ex));
        }

        return RedirectToAction(nameof(Products));
    }

    /// <summary>Edit form for one product.</summary>
    [HttpGet("products/{productId:guid}/edit")]
    public async Task<IActionResult> EditProduct(Guid productId, CancellationToken cancellationToken)
    {
        try
        {
            var p = await catalog.GetProductAsync(productId, cancellationToken);
            return View(new ProductFormViewModel
            {
                ProductId = p.ProductId,
                Name = p.Name,
                Description = p.Description,
                Price = p.Price,
                Currency = p.Currency,
                IsActive = p.IsActive,
            });
        }
        catch (Exception ex) when (IsRecoverable(ex) && ApiErrors.StatusOf(ex) == StatusCodes.Status404NotFound)
        {
            return NotFound();
        }
    }

    /// <summary>Saves a product (PUT /catalog/v1/products/{id}).</summary>
    [HttpPost("products/{productId:guid}/edit")]
    public async Task<IActionResult> EditProduct(Guid productId, ProductFormViewModel form, CancellationToken cancellationToken)
    {
        form.ProductId = productId;
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        try
        {
            var updated = await catalog.UpdateProductAsync(ToRequest(form), productId, cancellationToken);
            TempData.Success($"Saved {updated.Name}.");
            return RedirectToAction(nameof(Products));
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            ModelState.AddModelError(string.Empty, ApiErrors.Describe(ex));
            return View(form);
        }
    }

    /// <summary>Stock per product with adjustment forms.</summary>
    [HttpGet("stock")]
    public async Task<IActionResult> Stock(CancellationToken cancellationToken)
    {
        try
        {
            var productsTask = catalog.ListProductsAsync(includeInactive: true, cancellationToken);
            var stockTask = inventory.ListStockItemsAsync(cancellationToken);
            var products = await productsTask;
            var stock = (await stockTask).ToDictionary(s => s.ProductId);

            var rows = products
                .Select(p => stock.TryGetValue(p.ProductId, out var s)
                    ? new StockRow(p.ProductId, p.Name, p.IsActive, s.QuantityOnHand, s.QuantityReserved, s.QuantityAvailable)
                    : new StockRow(p.ProductId, p.Name, p.IsActive, 0, 0, 0))
                .OrderBy(r => r.Name)
                .ToList();
            return View(new AdminStockViewModel(rows, null));
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            return View(new AdminStockViewModel([], ApiErrors.Describe(ex)));
        }
    }

    /// <summary>Adjusts stock (POST /inventory/v1/stock/{id}/adjustments).</summary>
    [HttpPost("stock/{productId:guid}")]
    public async Task<IActionResult> AdjustStock(Guid productId, int delta, string? reason, string? name, CancellationToken cancellationToken)
    {
        if (delta == 0)
        {
            TempData.Warning("Enter a number of units to add (positive) or remove (negative).");
            return RedirectToAction(nameof(Stock));
        }

        try
        {
            var item = await inventory.AdjustStockAsync(
                productId,
                new StockAdjustmentRequest { Delta = delta, Reason = string.IsNullOrWhiteSpace(reason) ? "Manual adjustment" : reason.Trim() },
                cancellationToken);
            TempData.Success($"{name ?? "Stock"}: {(delta > 0 ? "+" : string.Empty)}{delta} unit(s). {item.QuantityAvailable} now available.");
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            TempData.Error(ApiErrors.Describe(ex));
        }

        return RedirectToAction(nameof(Stock));
    }

    /// <summary>Chaos and saga testing: payment and order failure modes, Catalog chaos.</summary>
    [HttpGet("chaos")]
    public async Task<IActionResult> Chaos(CancellationToken cancellationToken)
    {
        var problems = new List<string>();
        PaymentFailureModeResponse? payment = null;
        OrderFailureModeResponse? order = null;
        ChaosSettings? chaos = null;

        try { payment = await payments.GetPaymentFailureModeAsync(cancellationToken); }
        catch (Exception ex) when (IsRecoverable(ex)) { problems.Add($"Payment: {ApiErrors.Describe(ex)}"); }

        try { order = await orders.GetOrderFailureModeAsync(cancellationToken); }
        catch (Exception ex) when (IsRecoverable(ex)) { problems.Add($"Order: {ApiErrors.Describe(ex)}"); }

        try { chaos = await catalog.GetChaosSettingsAsync(cancellationToken); }
        catch (Exception ex) when (IsRecoverable(ex)) { problems.Add($"Catalog chaos: {ApiErrors.Describe(ex)}"); }

        return View(new AdminChaosViewModel(
            payment?.Mode.ToString(), payment?.UpdatedAt,
            order?.Mode.ToString(), order?.UpdatedAt,
            chaos?.FailureRate, chaos?.DelayMs,
            problems));
    }

    /// <summary>PUT /payments/v1/admin/failure-mode.</summary>
    [HttpPost("chaos/payment")]
    public async Task<IActionResult> SetPaymentMode(string mode, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<PaymentFailureMode>(mode, out var parsed))
        {
            TempData.Error("Unknown payment failure mode.");
            return RedirectToAction(nameof(Chaos));
        }

        try
        {
            var result = await payments.SetPaymentFailureModeAsync(new PaymentFailureModeRequest { Mode = parsed }, cancellationToken);
            logger.LogInformation("Admin set payment failure mode to {Mode}", result.Mode);
            TempData.Success(result.Mode == PaymentFailureMode.DeclineCharge
                ? "Payments will now be declined. Place an order to watch the stock being released."
                : "Payments are back to normal.");
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            TempData.Error(ApiErrors.Describe(ex));
        }

        return RedirectToAction(nameof(Chaos));
    }

    /// <summary>PUT /orders/v1/admin/failure-mode.</summary>
    [HttpPost("chaos/order")]
    public async Task<IActionResult> SetOrderMode(string mode, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<OrderFailureMode>(mode, out var parsed))
        {
            TempData.Error("Unknown order failure mode.");
            return RedirectToAction(nameof(Chaos));
        }

        try
        {
            var result = await orders.SetOrderFailureModeAsync(new OrderFailureModeRequest { Mode = parsed }, cancellationToken);
            logger.LogInformation("Admin set order failure mode to {Mode}", result.Mode);
            TempData.Success(result.Mode == OrderFailureMode.FailAfterPayment
                ? "Orders will now fail after payment. Place one to watch the refund and stock release."
                : "Orders confirm normally again.");
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            TempData.Error(ApiErrors.Describe(ex));
        }

        return RedirectToAction(nameof(Chaos));
    }

    /// <summary>PUT /catalog/v1/chaos.</summary>
    [HttpPost("chaos/catalog")]
    public async Task<IActionResult> SetCatalogChaos(double failurePercent, int delayMs, CancellationToken cancellationToken)
    {
        var settings = new ChaosSettings
        {
            FailureRate = Math.Clamp(failurePercent, 0, 100) / 100d,
            DelayMs = Math.Clamp(delayMs, 0, 30000),
        };

        try
        {
            await catalog.SetChaosSettingsAsync(settings, cancellationToken);
            TempData.Success(settings.FailureRate == 0 && settings.DelayMs == 0
                ? "Catalog chaos is off."
                : $"Catalog will fail {settings.FailureRate:P0} of product reads and add {settings.DelayMs} ms. Order and Inventory retry automatically.");
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            TempData.Error(ApiErrors.Describe(ex));
        }

        return RedirectToAction(nameof(Chaos));
    }

    private static ProductRequest ToRequest(ProductFormViewModel form) => new()
    {
        Name = form.Name.Trim(),
        Description = form.Description?.Trim() ?? string.Empty,
        Price = form.Price,
        Currency = form.Currency.Trim().ToUpperInvariant(),
        IsActive = form.IsActive,
    };

    private static bool IsRecoverable(Exception ex) =>
        ex is not OperationCanceledException
        && ApiErrors.StatusOf(ex) is not (StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden);

    private string? FirstModelError() =>
        ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault(m => !string.IsNullOrWhiteSpace(m));
}
