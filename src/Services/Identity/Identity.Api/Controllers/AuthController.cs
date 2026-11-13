using Asp.Versioning;
using Identity.Api.Contracts;
using Identity.Api.Domain;
using Identity.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Store.Observability;
using Store.Observability.Security;

namespace Identity.Api.Controllers;

/// <summary>Registration, login and the current user (identity.v1.yaml, tag Auth).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
[Produces("application/json")]
public sealed class AuthController(
    UserManager<AppUser> users,
    TokenService tokens,
    TimeProvider clock,
    ILogger<AuthController> logger) : ControllerBase
{
    private const string InvalidCredentials = "Invalid email or password.";

    /// <summary>operationId <c>register</c>: creates a Customer and signs them in.</summary>
    [HttpPost("register", Name = "register")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var email = request.Email.Trim();
        if (await users.FindByEmailAsync(email) is not null)
        {
            throw new ConflictException($"An account with email {email} already exists.");
        }

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FullName = request.FullName.Trim(),
            CreatedAt = clock.GetUtcNow(),
        };

        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            foreach (var error in created.Errors)
            {
                ModelState.AddModelError(error.Code.StartsWith("Password", StringComparison.Ordinal) ? nameof(request.Password) : nameof(request.Email), error.Description);
            }

            return ValidationProblem(ModelState);
        }

        var addedToRole = await users.AddToRoleAsync(user, Roles.Customer);
        if (!addedToRole.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not add {email} to {Roles.Customer}: {string.Join("; ", addedToRole.Errors.Select(e => e.Description))}");
        }

        logger.LogInformation("Registered customer {UserId} {Email}", user.Id, email);
        var response = CreateAuthResponse(user, [Roles.Customer]);
        return CreatedAtRoute("getCurrentUser", new { version = "1" }, response);
    }

    /// <summary>operationId <c>login</c>: checks the password and issues a JWT.</summary>
    [HttpPost("login", Name = "login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            logger.LogInformation("Login failed: unknown email");
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: InvalidCredentials);
        }

        if (await users.IsLockedOutAsync(user))
        {
            logger.LogWarning("Login refused for locked-out user {UserId}", user.Id);
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: InvalidCredentials,
                detail: "Too many failed attempts. Try again in a few minutes.");
        }

        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            await users.AccessFailedAsync(user);
            logger.LogInformation("Login failed: wrong password for user {UserId}", user.Id);
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: InvalidCredentials);
        }

        await users.ResetAccessFailedCountAsync(user);
        var roles = await users.GetRolesAsync(user);
        logger.LogInformation("User {UserId} signed in with roles {Roles}", user.Id, string.Join(",", roles));
        return Ok(CreateAuthResponse(user, roles));
    }

    /// <summary>operationId <c>getCurrentUser</c>.</summary>
    [HttpGet("me", Name = "getCurrentUser")]
    [Authorize]
    [ProducesResponseType<UserProfile>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfile>> GetCurrentUser()
    {
        var userId = User.GetUserId();
        var user = userId is null ? null : await users.FindByIdAsync(userId.Value.ToString());
        if (user is null)
        {
            throw new NotFoundException("The signed-in user no longer exists.");
        }

        return Ok(ToProfile(user, await users.GetRolesAsync(user)));
    }

    private AuthResponse CreateAuthResponse(AppUser user, IList<string> roles)
    {
        var (token, expiresAt) = tokens.CreateToken(user, roles);
        return new AuthResponse(token, "Bearer", expiresAt, ToProfile(user, roles));
    }

    private static UserProfile ToProfile(AppUser user, IEnumerable<string> roles) =>
        new(user.Id, user.Email ?? string.Empty, user.FullName, roles.OrderBy(r => r, StringComparer.Ordinal).ToList());
}
