using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Payment.Api.Contracts;
using Payment.Api.Data;
using Store.Observability;
using Store.Observability.Security;

namespace Payment.Api.Controllers;

/// <summary>Payments recorded by the saga (payment.v1.yaml, tag Payments). Admin only.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/payments")]
[Produces("application/json")]
[Authorize(Roles = StoreClaims.Roles.Admin)]
public sealed class PaymentsController(PaymentDbContext db) : ControllerBase
{
    /// <summary>operationId <c>listPayments</c>: newest first.</summary>
    [HttpGet(Name = "listPayments")]
    [ProducesResponseType<IReadOnlyList<PaymentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PaymentResponse>>> ListPayments(
        [FromQuery, Range(1, 200)] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var payments = await db.Payments.AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
        return Ok(payments.Select(PaymentResponse.From).ToList());
    }

    /// <summary>operationId <c>getPaymentByOrder</c>.</summary>
    [HttpGet("orders/{orderId:guid}", Name = "getPaymentByOrder")]
    [ProducesResponseType<PaymentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentResponse>> GetPaymentByOrder(Guid orderId, CancellationToken cancellationToken)
    {
        var payment = await db.Payments.AsNoTracking().SingleOrDefaultAsync(p => p.OrderId == orderId, cancellationToken)
            ?? throw new NotFoundException($"No payment has been attempted for order {orderId}.");
        return Ok(PaymentResponse.From(payment));
    }
}
