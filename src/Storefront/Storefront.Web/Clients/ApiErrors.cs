using System.Text.Json;
using Storefront.Web.Clients.Identity;

namespace Storefront.Web.Clients;

/// <summary>Turns a failed gateway call into a message a shopper can act on.</summary>
public static class ApiErrors
{
    /// <summary>HTTP status of a failed call, or 503 when the gateway could not be reached.</summary>
    public static int StatusOf(Exception exception) => exception switch
    {
        ApiException api => api.StatusCode,
        HttpRequestException or TaskCanceledException => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status500InternalServerError,
    };

    /// <summary>
    /// A friendly message: the service's own Problem Details <c>detail</c> when it explains the
    /// problem (for example "Cannot remove 1000 unit(s): only 45 on hand."), otherwise a sensible
    /// default for the status code.
    /// </summary>
    public static string Describe(Exception exception)
    {
        var status = StatusOf(exception);
        var fromService = exception is ApiException api ? ReadProblem(api) : null;

        return status switch
        {
            StatusCodes.Status400BadRequest => fromService ?? "Some of the details you entered are not valid. Check the form and try again.",
            StatusCodes.Status401Unauthorized => fromService ?? "Please sign in to continue.",
            StatusCodes.Status403Forbidden => "Your account is not allowed to do that.",
            StatusCodes.Status404NotFound => fromService ?? "We could not find what you were looking for.",
            StatusCodes.Status409Conflict => fromService ?? "That change conflicts with the current state. Refresh and try again.",
            StatusCodes.Status429TooManyRequests => fromService ?? "Too many attempts. Wait a minute and try again.",
            StatusCodes.Status502BadGateway or StatusCodes.Status503ServiceUnavailable or StatusCodes.Status504GatewayTimeout =>
                fromService is null ? "Part of the store is temporarily unavailable. Please try again in a moment." : $"{fromService} Please try again in a moment.",
            _ => "Something went wrong on our side. Please try again.",
        };
    }

    /// <summary>Validation errors from a 400 Problem Details response, keyed by field.</summary>
    public static IReadOnlyDictionary<string, string[]> ValidationErrors(Exception exception)
    {
        if (exception is not ApiException { StatusCode: StatusCodes.Status400BadRequest } api || string.IsNullOrWhiteSpace(api.Response))
        {
            return new Dictionary<string, string[]>();
        }

        try
        {
            using var document = JsonDocument.Parse(api.Response);
            if (document.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                return errors.EnumerateObject().ToDictionary(
                    p => p.Name,
                    p => p.Value.ValueKind == JsonValueKind.Array
                        ? p.Value.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray()
                        : [p.Value.ToString()],
                    StringComparer.OrdinalIgnoreCase);
            }
        }
        catch (JsonException)
        {
        }

        return new Dictionary<string, string[]>();
    }

    private static string? ReadProblem(ApiException api)
    {
        if (string.IsNullOrWhiteSpace(api.Response))
        {
            return null;
        }

        var body = api.Response.Trim();
        if (!body.StartsWith('{'))
        {
            // Plain-text bodies, such as the gateway's rate-limit message.
            return body.Length <= 200 ? body : null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                var first = errors.EnumerateObject()
                    .SelectMany(p => p.Value.ValueKind == JsonValueKind.Array ? p.Value.EnumerateArray().Select(e => e.GetString()) : [p.Value.ToString()])
                    .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m));
                if (first is not null)
                {
                    return first;
                }
            }

            if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(detail.GetString()))
            {
                return detail.GetString();
            }

            if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
            {
                return title.GetString();
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }
}
