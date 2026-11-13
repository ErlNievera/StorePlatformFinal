using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Order.Api.Contracts;
using Order.Api.Data;
using Order.Api.Domain;
using Store.Observability.Security;

namespace Order.Api.Controllers;

/// <summary>
/// Order failure switch (order.v1.yaml, tag Admin), reached through the gateway as
/// <c>PUT /orders/v1/admin/failure-mode</c>. Applies to the next payment confirmation.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/failure-mode")]
[Produces("application/json")]
[Authorize(Roles = StoreClaims.Roles.Admin)]
public sealed class AdminController(OrderDbContext db, TimeProvider clock, ILogger<AdminController> logger) : ControllerBase
{
    /// <summary>operationId <c>getOrderFailureMode</c>.</summary>
    [HttpGet(Name = "getOrderFailureMode")]
    [ProducesResponseType<OrderFailureModeResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OrderFailureModeResponse>> GetOrderFailureMode(CancellationToken cancellationToken) =>
        Ok(OrderFailureModeResponse.From(await LoadSettingsAsync(cancellationToken)));

    /// <summary>operationId <c>setOrderFailureMode</c>.</summary>
    [HttpPut(Name = "setOrderFailureMode")]
    [ProducesResponseType<OrderFailureModeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrderFailureModeResponse>> SetOrderFailureMode(
        OrderFailureModeRequest request,
        CancellationToken cancellationToken)
    {
        var settings = await LoadSettingsAsync(cancellationToken);
        settings.SetFailureMode(request.Mode!.Value, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        logger.LogWarning("Order failure mode set to {FailureMode}", settings.FailureMode);
        return Ok(OrderFailureModeResponse.From(settings));
    }

    private async Task<OrderSettings> LoadSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await db.Settings.SingleOrDefaultAsync(s => s.Id == OrderSettings.SingletonId, cancellationToken);
        if (settings is null)
        {
            settings = new OrderSettings(OrderFailureMode.None, clock.GetUtcNow());
            db.Settings.Add(settings);
            await db.SaveChangesAsync(cancellationToken);
        }

        return settings;
    }
}
