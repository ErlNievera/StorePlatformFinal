using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Storefront.Web.Clients;
using Storefront.Web.Clients.Identity;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Controllers;

/// <summary>Sign in, register and sign out (POST /identity/v1/auth/login and /register through the gateway).</summary>
[Route("account")]
public sealed class AccountController(IIdentityClient identity, ILogger<AccountController> logger) : Controller
{
    /// <summary>Split-screen login page.</summary>
    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null, bool expired = false)
    {
        if (expired)
        {
            TempData.Info("Your session has ended. Please sign in again.");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    /// <summary>Signs in through Identity and keeps the JWT in the HttpOnly auth cookie.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var auth = await identity.LoginAsync(new LoginRequest { Email = model.Email.Trim(), Password = model.Password }, cancellationToken);
            await SignInAsync(auth);
            TempData.Success($"Welcome back, {auth.User.FullName}!");
            return RedirectAfterSignIn(model.ReturnUrl, auth);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogInformation("Login failed with {Status}", ApiErrors.StatusOf(ex));
            ModelState.AddModelError(string.Empty, ApiErrors.Describe(ex));
            return View(model);
        }
    }

    /// <summary>Registration page.</summary>
    [HttpGet("register")]
    [AllowAnonymous]
    public IActionResult Register() => View(new RegisterViewModel());

    /// <summary>Creates a customer account and signs it in.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var auth = await identity.RegisterAsync(
                new RegisterRequest { Email = model.Email.Trim(), Password = model.Password, FullName = model.FullName.Trim() },
                cancellationToken);
            await SignInAsync(auth);
            TempData.Success($"Your account is ready, {auth.User.FullName}. Happy shopping!");
            return RedirectToAction(nameof(HomeController.Index), "Home");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            foreach (var (field, messages) in ApiErrors.ValidationErrors(ex))
            {
                foreach (var message in messages)
                {
                    ModelState.AddModelError(field, message);
                }
            }

            if (ModelState.ErrorCount == 0)
            {
                ModelState.AddModelError(string.Empty, ApiErrors.Describe(ex));
            }

            return View(model);
        }
    }

    /// <summary>Signs out and clears the auth cookie.</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData.Info("You have signed out.");
        return RedirectToAction(nameof(HomeController.Index), "Home");
    }

    private async Task SignInAsync(AuthResponse auth)
    {
        var claims = new List<Claim>
        {
            new("sub", auth.User.UserId.ToString()),
            new("name", auth.User.FullName),
            new("email", auth.User.Email),
        };
        claims.AddRange(auth.User.Roles.Select(role => new Claim("role", role.ToString())));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme, "name", "role"));
        var properties = new AuthenticationProperties
        {
            IsPersistent = false,
            ExpiresUtc = auth.ExpiresAt,
            AllowRefresh = false,
        };
        properties.StoreTokens([new AuthenticationToken { Name = GatewayBearerTokenHandler.TokenName, Value = auth.AccessToken }]);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);
        logger.LogInformation("Signed in {UserId} as {Roles}; token expires {ExpiresAt:O} ({TokenId})",
            auth.User.UserId, string.Join(",", auth.User.Roles), auth.ExpiresAt, new JwtSecurityTokenHandler().ReadJwtToken(auth.AccessToken).Id);
    }

    private IActionResult RedirectAfterSignIn(string? returnUrl, AuthResponse auth)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return auth.User.Roles.Contains(UserRole.Admin)
            ? RedirectToAction(nameof(AdminController.Chaos), "Admin")
            : RedirectToAction(nameof(HomeController.Index), "Home");
    }
}
