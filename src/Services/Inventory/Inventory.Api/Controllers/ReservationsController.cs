using Asp.Versioning;
using Inventory.Api.Contracts;
using Inventory.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Observability;
using Store.Observability.Security;

namespace Inventory.Api.Controllers;

/// <summary>Stock held for orders by the saga (inventory.v1.yaml, tag Reservations).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/reservations")]
[Produces("application/json")]
[Authorize(Roles = StoreClaims.Roles.Admin)]
public sealed class ReservationsController(InventoryDbContext db) : ControllerBase
{
    /// <summary>operationId <c>getReservation</c> (Admin).</summary>
    [HttpGet("{orderId:guid}", Name = "getReservation")]
    [ProducesResponseType<ReservationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReservationResponse>> GetReservation(Guid orderId, CancellationToken cancellationToken)
    {
        var reservation = await db.Reservations.AsNoTracking()
            .Include(r => r.Lines)
            .SingleOrDefaultAsync(r => r.OrderId == orderId, cancellationToken)
            ?? throw new NotFoundException($"No reservation exists for order {orderId}.");
        return Ok(ReservationResponse.From(reservation));
    }
}
