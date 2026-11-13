using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Storefront.Web.Clients;
using Storefront.Web.Clients.Orders;
using Storefront.Web.Models;

namespace Storefront.Web.Controllers;

/// <summary>My Orders and order details with the saga timeline (GET /orders/v1/orders/...).</summary>
[Authorize]
[Route("orders")]
public sealed class OrdersController(IOrderClient orders, ILogger<OrdersController> logger) : Controller
{
    /// <summary>The signed-in customer's orders.</summary>
    [HttpGet("")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            var mine = await orders.ListMyOrdersAsync(cancellationToken);
            return View(new OrdersViewModel(mine.ToList(), null));
        }
        catch (Exception ex) when (ex is not OperationCanceledException && ApiErrors.StatusOf(ex) is not (StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden))
        {
            logger.LogWarning(ex, "Orders could not be loaded");
            return View(new OrdersViewModel([], ApiErrors.Describe(ex)));
        }
    }

    /// <summary>Order details with lines and the animated status timeline.</summary>
    [HttpGet("{orderId:guid}")]
    public async Task<IActionResult> Details(Guid orderId, CancellationToken cancellationToken)
    {
        try
        {
            return View(await orders.GetOrderAsync(orderId, cancellationToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException && ApiErrors.StatusOf(ex) == StatusCodes.Status404NotFound)
        {
            return NotFound();
        }
    }

    /// <summary>JSON used by the timeline to poll every 2 seconds until the order is Confirmed or Cancelled.</summary>
    [HttpGet("{orderId:guid}/status")]
    public async Task<IActionResult> Status(Guid orderId, CancellationToken cancellationToken)
    {
        try
        {
            var order = await orders.GetOrderAsync(orderId, cancellationToken);
            return Json(new
            {
                status = order.Status.ToString(),
                cancellationReason = order.CancellationReason,
                final = order.Status is OrderStatus.Confirmed or OrderStatus.Cancelled,
                history = order.History.Select(h => new
                {
                    status = h.Status.ToString(),
                    reason = h.Reason,
                    isCompensation = h.IsCompensation,
                    occurredAt = h.OccurredAt,
                }),
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var status = ApiErrors.StatusOf(ex);
            return StatusCode(status, new { message = ApiErrors.Describe(ex) });
        }
    }
}
