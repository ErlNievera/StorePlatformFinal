using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Payment.Api.Contracts;
using Payment.Api.Data;
using Payment.Api.Domain;
using Store.Observability.Security;

namespace Payment.Api.Controllers;

/// <summary>
/// Payment failure switch (payment.v1.yaml, tag Admin). Reached through the gateway as
/// <c>PUT /payments/v1/admin/failure-mode</c>; takes effect for the next charge, with no restart.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/failure-mode")]
[Produces("application/json")]
[Authorize(Roles = StoreClaims.Roles.Admin)]
public sealed class AdminController(PaymentDbContext db, TimeProvider clock, ILogger<AdminController> logger) : ControllerBase
{
    /// <summary>operationId <c>getPaymentFailureMode</c>.</summary>
    [HttpGet(Name = "getPaymentFailureMode")]
    [ProducesResponseType<PaymentFailureModeResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaymentFailureModeResponse>> GetPaymentFailureMode(CancellationToken cancellationToken) =>
        Ok(PaymentFailureModeResponse.From(await LoadSettingsAsync(cancellationToken)));

    /// <summary>operationId <c>setPaymentFailureMode</c>.</summary>
    [HttpPut(Name = "setPaymentFailureMode")]
    [ProducesResponseType<PaymentFailureModeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaymentFailureModeResponse>> SetPaymentFailureMode(
        PaymentFailureModeRequest request,
        CancellationToken cancellationToken)
    {
        var settings = await LoadSettingsAsync(cancellationToken);
        settings.SetFailureMode(request.Mode!.Value, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        logger.LogWarning("Payment failure mode set to {FailureMode}", settings.FailureMode);
        return Ok(PaymentFailureModeResponse.From(settings));
    }

    private async Task<PaymentSettings> LoadSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await db.Settings.SingleOrDefaultAsync(s => s.Id == PaymentSettings.SingletonId, cancellationToken);
        if (settings is null)
        {
            settings = new PaymentSettings(PaymentFailureMode.None, clock.GetUtcNow());
            db.Settings.Add(settings);
            await db.SaveChangesAsync(cancellationToken);
        }

        return settings;
    }
}
