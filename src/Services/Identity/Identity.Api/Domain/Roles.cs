namespace Identity.Api.Domain;

/// <summary>Role names carried in the JWT <c>role</c> claim.</summary>
public static class Roles
{
    /// <summary>Shoppers: place and view their own orders.</summary>
    public const string Customer = "Customer";

    /// <summary>Staff: manage products and stock, switch failure modes, use chaos.</summary>
    public const string Admin = "Admin";

    /// <summary>Every role, in a stable order.</summary>
    public static IReadOnlyList<string> All { get; } = [Customer, Admin];
}
