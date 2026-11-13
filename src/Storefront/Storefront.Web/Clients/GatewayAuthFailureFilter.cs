using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Storefront.Web.Clients.Identity;

namespace Storefront.Web.Clients;

/// <summary>
/// When the gateway answers 401 (expired or missing token) the Storefront signs the user out and
/// sends them to the login page; a 403 goes to the friendly "not allowed" page. Other errors are
/// handled by the controllers, which show a toast with <see cref="ApiErrors.Describe"/>.
/// </summary>
public sealed class GatewayAuthFailureFilter(ILogger<GatewayAuthFailureFilter> logger) : IAsyncExceptionFilter
{
    /// <inheritdoc />
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        if (context.Exception is not ApiException api)
        {
            return;
        }

        if (api.StatusCode == StatusCodes.Status401Unauthorized)
        {
            logger.LogInformation("Gateway answered 401; signing the user out");
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
            context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl, expired = true });
            context.ExceptionHandled = true;
        }
        else if (api.StatusCode == StatusCodes.Status403Forbidden)
        {
            context.Result = new RedirectToActionResult("Show", "Errors", new { code = 403 });
            context.ExceptionHandled = true;
        }
    }
}
