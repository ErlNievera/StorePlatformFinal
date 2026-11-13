using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Storefront.Web.Clients;
using Storefront.Web.Clients.Catalog;
using Storefront.Web.Clients.Orders;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Controllers;

/// <summary>Cart and checkout (POST /orders/v1/orders through the gateway).</summary>
public sealed class CartController(CartService cart, ICatalogClient catalog, IOrderClient orders, ILogger<CartController> logger) : Controller
{
    /// <summary>The cart.</summary>
    [HttpGet("/cart")]
    public IActionResult Index() => View(CreateModel());

    /// <summary>Adds a product using Catalog's current name and price.</summary>
    [HttpPost("/cart/add")]
    public async Task<IActionResult> Add(Guid productId, int quantity, string? returnUrl, CancellationToken cancellationToken)
    {
        try
        {
            var product = await catalog.GetProductAsync(productId, cancellationToken);
            if (!product.IsActive)
            {
                TempData.Warning($"{product.Name} is not available right now.");
                return RedirectBack(returnUrl);
            }

            cart.Add(product.ProductId, product.Name, product.Price, product.Currency, Math.Clamp(quantity, 1, CartService.MaxQuantity));
            TempData.Success($"Added {Math.Clamp(quantity, 1, CartService.MaxQuantity)} × {product.Name} to your cart.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            TempData.Error(ApiErrors.Describe(ex));
        }

        return RedirectBack(returnUrl);
    }

    /// <summary>Changes a quantity (0 removes the line).</summary>
    [HttpPost("/cart/update")]
    public IActionResult Update(Guid productId, int quantity)
    {
        cart.Update(productId, quantity);
        TempData.Success(quantity <= 0 ? "Removed from your cart." : "Cart updated.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Removes a line.</summary>
    [HttpPost("/cart/remove")]
    public IActionResult Remove(Guid productId)
    {
        cart.Remove(productId);
        TempData.Success("Removed from your cart.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Order summary before placing the order.</summary>
    [HttpGet("/checkout")]
    [Authorize(Roles = "Customer")]
    public IActionResult Checkout()
    {
        if (cart.ItemCount == 0)
        {
            TempData.Info("Your cart is empty. Add something first.");
            return RedirectToAction(nameof(HomeController.Index), "Home");
        }

        return View(CreateModel());
    }

    /// <summary>Places the order; the saga then runs in the background.</summary>
    [HttpPost("/checkout")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> PlaceOrder(CancellationToken cancellationToken)
    {
        var lines = cart.Lines;
        if (lines.Count == 0)
        {
            TempData.Info("Your cart is empty.");
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var request = new PlaceOrderRequest();
            foreach (var line in lines)
            {
                request.Lines.Add(new PlaceOrderLine { ProductId = line.ProductId, Quantity = line.Quantity });
            }

            var order = await orders.PlaceOrderAsync(request, cancellationToken);
            cart.Clear();
            logger.LogInformation("Placed order {OrderId}", order.OrderId);
            TempData.Success("Order placed! Watch it move through the steps below.");
            return RedirectToAction(nameof(OrdersController.Details), "Orders", new { orderId = order.OrderId });
        }
        catch (Exception ex) when (ex is not OperationCanceledException && ApiErrors.StatusOf(ex) is not (StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden))
        {
            TempData.Error(ApiErrors.Describe(ex));
            return RedirectToAction(nameof(Checkout));
        }
    }

    private CartViewModel CreateModel() =>
        new(cart.Lines, cart.Total, cart.Lines.FirstOrDefault()?.Currency ?? "PHP");

    private IActionResult RedirectBack(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction(nameof(Index));
}
