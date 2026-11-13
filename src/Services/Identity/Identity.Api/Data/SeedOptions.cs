namespace Identity.Api.Data;

/// <summary>
/// Demo accounts created at startup, bound from the <c>Seed</c> section
/// (<c>Seed__AdminEmail</c>, <c>Seed__AdminPassword</c>, ... set from <c>SEED_*</c> in .env).
/// An account whose email or password is empty is skipped.
/// </summary>
public sealed class SeedOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Seed";

    /// <summary>Admin email.</summary>
    public string AdminEmail { get; set; } = string.Empty;

    /// <summary>Admin password.</summary>
    public string AdminPassword { get; set; } = string.Empty;

    /// <summary>Admin display name.</summary>
    public string AdminFullName { get; set; } = "Store Administrator";

    /// <summary>Demo customer email.</summary>
    public string CustomerEmail { get; set; } = string.Empty;

    /// <summary>Demo customer password.</summary>
    public string CustomerPassword { get; set; } = string.Empty;

    /// <summary>Demo customer display name.</summary>
    public string CustomerFullName { get; set; } = "Demo Customer";
}
