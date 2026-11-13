using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Store.Observability;

/// <summary>
/// Turns exceptions into RFC 7807 Problem Details: 400 <see cref="BadRequestException"/> and
/// malformed requests, 404 <see cref="NotFoundException"/>, 409 <see cref="ConflictException"/>
/// and <c>DbUpdateConcurrencyException</c>, 503 <see cref="DownstreamServiceException"/>,
/// 500 for anything else. The <c>correlationId</c> extension is added by the shared
/// Problem Details customization.
/// </summary>
public sealed class StoreExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<StoreExceptionHandler> logger) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = Map(exception);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Request {Method} {Path} failed with {StatusCode}: {Title}",
                httpContext.Request.Method, httpContext.Request.Path, status, title);
        }
        else
        {
            logger.LogInformation("Request {Method} {Path} returned {StatusCode}: {Detail}",
                httpContext.Request.Method, httpContext.Request.Path, status, detail);
        }

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
            },
        });
    }

    private static (int Status, string Title, string? Detail) Map(Exception exception) => exception switch
    {
        BadRequestException e => (StatusCodes.Status400BadRequest, "The request is invalid.", e.Message),
        BadHttpRequestException e => (e.StatusCode, "The request is invalid.", e.Message),
        NotFoundException e => (StatusCodes.Status404NotFound, "The resource was not found.", e.Message),
        ConflictException e => (StatusCodes.Status409Conflict, "The request conflicts with the current state.", e.Message),
        DownstreamServiceException e => (StatusCodes.Status503ServiceUnavailable, $"{e.ServiceName} is unavailable.", e.Message),
        _ when exception.GetType().Name == "DbUpdateConcurrencyException" =>
            (StatusCodes.Status409Conflict, "The resource was changed by another request.", "Reload it and try again."),
        _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", "The error has been logged with this request's correlation id."),
    };
}
