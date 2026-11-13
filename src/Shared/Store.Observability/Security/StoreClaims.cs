using System.Security.Claims;

namespace Store.Observability.Security;

/// <summary>Claim types in Store Platform JWTs (kept short; inbound claim mapping is off).</summary>
public static class StoreClaims
{
    /// <summary>User id (uuid).</summary>
    public const string Subject = "sub";

    /// <summary>Email address.</summary>
    public const string Email = "email";

    /// <summary>Display name.</summary>
    public const string Name = "name";

    /// <summary>One claim per role.</summary>
    public const string Role = "role";

    /// <summary>Role names.</summary>
    public static class Roles
    {
        /// <summary>Shoppers.</summary>
        public const string Customer = "Customer";

        /// <summary>Staff.</summary>
        public const string Admin = "Admin";
    }

    /// <summary>The signed-in user's id, or <see langword="null"/> when absent or not a uuid.</summary>
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return Guid.TryParse(principal.FindFirstValue(Subject), out var id) ? id : null;
    }
}
