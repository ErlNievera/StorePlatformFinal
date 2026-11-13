using System.ComponentModel.DataAnnotations;
using Identity.Api.Domain;

namespace Identity.Api.Contracts;

/// <summary>Body of <c>register</c> (schema <c>RegisterRequest</c>).</summary>
public sealed class RegisterRequest
{
    /// <summary>Email, used to sign in.</summary>
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; init; } = string.Empty;

    /// <summary>At least 8 characters with upper, lower and a digit.</summary>
    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string Password { get; init; } = string.Empty;

    /// <summary>Display name.</summary>
    [Required]
    [StringLength(AppUser.FullNameMaxLength, MinimumLength = 2)]
    public string FullName { get; init; } = string.Empty;
}

/// <summary>Body of <c>login</c> (schema <c>LoginRequest</c>).</summary>
public sealed class LoginRequest
{
    /// <summary>Email.</summary>
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; init; } = string.Empty;

    /// <summary>Password.</summary>
    [Required]
    [StringLength(128)]
    public string Password { get; init; } = string.Empty;
}

/// <summary>Schema <c>UserProfile</c>.</summary>
public sealed record UserProfile(Guid UserId, string Email, string FullName, IReadOnlyList<string> Roles);

/// <summary>Schema <c>AuthResponse</c>.</summary>
public sealed record AuthResponse(string AccessToken, string TokenType, DateTimeOffset ExpiresAt, UserProfile User);
