using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;

namespace Storefront.Web.Clients;

/// <summary>
/// Sends the JWT kept in the signed-in user's HttpOnly authentication cookie as
/// <c>Authorization: Bearer</c> on every call to the gateway.
/// </summary>
public sealed class GatewayBearerTokenHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    /// <summary>Name under which the access token is stored in the authentication properties.</summary>
    public const string TokenName = "access_token";

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null && request.Headers.Authorization is null)
        {
            var token = await httpContext.GetTokenAsync(TokenName);
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
