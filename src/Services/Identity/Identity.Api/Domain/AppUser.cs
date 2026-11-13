using Microsoft.AspNetCore.Identity;

namespace Identity.Api.Domain;

/// <summary>A store user (customer or admin). The id is the JWT <c>sub</c> claim used by every service.</summary>
public sealed class AppUser : IdentityUser<Guid>
{
    /// <summary>Maximum length of <see cref="FullName"/>.</summary>
    public const int FullNameMaxLength = 120;

    /// <summary>Name shown in the Storefront and put in the JWT <c>name</c> claim.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>When the account was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
