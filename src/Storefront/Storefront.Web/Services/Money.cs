using System.Globalization;

namespace Storefront.Web.Services;

/// <summary>Formats amounts for display.</summary>
public static class Money
{
    private static readonly CultureInfo Philippines = CultureInfo.GetCultureInfo("en-PH");

    /// <summary>"₱1,299.00" for PHP, "USD 12.00" for anything else.</summary>
    public static string Format(decimal amount, string? currency) =>
        string.Equals(currency, "PHP", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(currency)
            ? amount.ToString("C2", Philippines)
            : $"{currency.ToUpperInvariant()} {amount.ToString("N2", CultureInfo.InvariantCulture)}";
}
