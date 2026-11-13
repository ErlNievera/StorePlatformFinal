using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Order.Api.Application;
using Order.Api.Contracts;
using Order.Api.Data;
using Store.Observability;
using Store.Observability.Security;

namespace Order.Api.Controllers;

/// <summary>Place orders and follow their progress (order.v1.yaml, tag Orders).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/orders")]
[Produces("application/json")]
[Authorize]
public sealed class OrdersController(OrderDbContext db, OrderPlacementService placement) : ControllerBase
{
    /// <summary>operationId <c>placeOrder</c> (Customer): 202 with the order in Pending.</summary>
    [HttpPost(Name = "placeOrder")]
    [Authorize(Roles = StoreClaims.Roles.Customer)]
    [ProducesResponseType<OrderDetailsResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<OrderDetailsResponse>> PlaceOrder(PlaceOrderRequest request, CancellationToken cancellationToken)
    {
        var customerId = User.GetUserId() ?? throw new BadRequestException("The token has no valid user id.");
        var order = await placement.PlaceAsync(customerId, request, cancellationToken);
        return AcceptedAtRoute("getOrder", new { orderId = order.OrderId, version = "1" }, OrderDetailsResponse.From(order));
    }

    /// <summary>operationId <c>listMyOrders</c> (Customer): newest first.</summary>
    [HttpGet("mine", Name = "listMyOrders")]
    [Authorize(Roles = StoreClaims.Roles.Customer)]
    [ProducesResponseType<IReadOnlyList<OrderSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrderSummaryResponse>>> ListMyOrders(CancellationToken cancellationToken)
    {
        var customerId = User.GetUserId() ?? throw new BadRequestException("The token has no valid user id.");
        var orders = await db.Orders.AsNoTracking()
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new OrderSummaryResponse(
                o.OrderId,
                o.Status,
                o.TotalAmount,
                o.Currency,
                o.Lines.Sum(l => l.Quantity),
                o.CancellationReason,
                o.CreatedAt,
                o.UpdatedAt))
            .ToListAsync(cancellationToken);
        return Ok(orders);
    }

    /// <summary>operationId <c>getOrder</c>: the owner or an Admin; anyone else gets 404.</summary>
    [HttpGet("{orderId:guid}", Name = "getOrder")]
    [ProducesResponseType<OrderDetailsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDetailsResponse>> GetOrder(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Lines)
            .Include(o => o.History)
            .AsSplitQuery()
            .SingleOrDefaultAsync(o => o.OrderId == orderId, cancellationToken);

        var isAdmin = User.IsInRole(StoreClaims.Roles.Admin);
        if (order is null || (!isAdmin && order.CustomerId != User.GetUserId()))
        {
            throw new NotFoundException($"Order {orderId} was not found.");
        }

        return Ok(OrderDetailsResponse.From(order));
    }
}
