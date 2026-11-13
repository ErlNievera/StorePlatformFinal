using Asp.Versioning;
using Catalog.Api.Chaos;
using Catalog.Api.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Store.Observability;
using Store.Observability.Security;

namespace Catalog.Api.Controllers;

/// <summary>
/// Development-only fault injection for product endpoints (catalog.v1.yaml, tag Chaos).
/// Outside Development every action answers 404.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/chaos")]
[Produces("application/json")]
[Authorize(Roles = StoreClaims.Roles.Admin)]
public sealed class ChaosController(ChaosState chaos, IHostEnvironment environment, ILogger<ChaosController> logger) : ControllerBase
{
    /// <summary>operationId <c>getChaosSettings</c>.</summary>
    [HttpGet(Name = "getChaosSettings")]
    [ProducesResponseType<ChaosSettingsDto>(StatusCodes.Status200OK)]
    public ActionResult<ChaosSettingsDto> GetChaosSettings()
    {
        EnsureDevelopment();
        var (failureRate, delayMs) = chaos.Snapshot();
        return Ok(new ChaosSettingsDto { FailureRate = failureRate, DelayMs = delayMs });
    }

    /// <summary>operationId <c>setChaosSettings</c>.</summary>
    [HttpPut(Name = "setChaosSettings")]
    [ProducesResponseType<ChaosSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public ActionResult<ChaosSettingsDto> SetChaosSettings(ChaosSettingsDto settings)
    {
        EnsureDevelopment();
        chaos.Set(settings.FailureRate, settings.DelayMs);
        logger.LogWarning("Chaos settings changed: failure rate {FailureRate:P0}, delay {DelayMs} ms",
            settings.FailureRate, settings.DelayMs);
        return Ok(settings);
    }

    private void EnsureDevelopment()
    {
        if (!environment.IsDevelopment())
        {
            throw new NotFoundException("Chaos endpoints exist only in the Development environment.");
        }
    }
}
