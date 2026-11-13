using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Catalog.Api.Chaos;

/// <summary>
/// Applies the chaos settings to product endpoints in Development: waits <c>DelayMs</c>, then
/// answers 503 for a <c>FailureRate</c> share of requests. Callers' resilience handlers retry these.
/// </summary>
public sealed class ChaosFilter(ChaosState chaos, IHostEnvironment environment, ILogger<ChaosFilter> logger) : IAsyncActionFilter
{
    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!environment.IsDevelopment())
        {
            await next();
            return;
        }

        var (failureRate, delayMs) = chaos.Snapshot();
        if (delayMs > 0)
        {
            await Task.Delay(delayMs, context.HttpContext.RequestAborted);
        }

        if (failureRate > 0 && Random.Shared.NextDouble() < failureRate)
        {
            logger.LogWarning(
                "Chaos: injected 503 for {Method} {Path} (failure rate {FailureRate:P0})",
                context.HttpContext.Request.Method, context.HttpContext.Request.Path, failureRate);
            var problem = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>().CreateProblemDetails(
                context.HttpContext,
                StatusCodes.Status503ServiceUnavailable,
                title: "Catalog is unavailable.",
                detail: "Chaos mode injected this failure. Turn it off with PUT /api/v1/chaos { \"failureRate\": 0, \"delayMs\": 0 }.");
            context.Result = new ObjectResult(problem) { StatusCode = StatusCodes.Status503ServiceUnavailable };
            return;
        }

        await next();
    }
}
