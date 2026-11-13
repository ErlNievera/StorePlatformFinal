namespace Gateway.Api;

/// <summary>
/// Identifies the caller for Ocelot's rate limiter. Ocelot counts requests per value of the
/// <see cref="HeaderName"/> header and rejects requests that lack it, so the gateway sets it from
/// the caller's IP address, always overwriting whatever a client sent so the limit on
/// <c>POST /identity/v1/auth/login</c> (5 per minute) cannot be bypassed by changing the header.
/// </summary>
public sealed class RateLimitClientMiddleware(RequestDelegate next)
{
    /// <summary>Header Ocelot reads the client id from (its default, set explicitly in ocelot.json).</summary>
    public const string HeaderName = "Oc-Client";

    /// <summary>Handles one request.</summary>
    public Task InvokeAsync(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        var clientId = address is null
            ? "unknown"
            : (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();

        context.Request.Headers[HeaderName] = $"ip:{clientId}";
        return next(context);
    }
}
