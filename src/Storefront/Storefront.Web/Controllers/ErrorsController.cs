using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Storefront.Web.Models;

namespace Storefront.Web.Controllers;

/// <summary>Friendly pages for 401, 403, 404, 429 and unexpected errors.</summary>
[AllowAnonymous]
[Route("error")]
public sealed class ErrorsController : Controller
{
    /// <summary>Renders the page for <paramref name="code"/>.</summary>
    [HttpGet("{code:int}")]
    [HttpPost("{code:int}")]
    [IgnoreAntiforgeryToken]
    public IActionResult Show(int code)
    {
        var (title, message) = code switch
        {
            401 => ("Please sign in", "You need to sign in to see this page."),
            403 => ("This area is for staff", "Your account is not allowed to open this page. Sign in with an administrator account to continue."),
            404 => ("Page not found", "We could not find that page or product. It may have been moved or is no longer sold."),
            429 => ("Slow down a little", "Too many requests in a short time. Wait a minute and try again."),
            503 => ("Back in a moment", "Part of the store is temporarily unavailable. Please try again shortly."),
            _ => ("Something went wrong", "An unexpected error occurred. Please try again; if it keeps happening, share the reference below with the store team."),
        };

        Response.StatusCode = code is >= 400 and <= 599 ? code : 500;
        return View("Error", new ErrorViewModel(code, title, message, HttpContext.TraceIdentifier));
    }
}
