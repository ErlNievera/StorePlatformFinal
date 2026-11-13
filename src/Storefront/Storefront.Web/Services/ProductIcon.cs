namespace Storefront.Web.Services;

/// <summary>Picks a Bootstrap Icon for a product card from words in its name.</summary>
public static class ProductIcon
{
    private static readonly (string Word, string Icon)[] Map =
    [
        ("hoodie", "bi-person-standing"),
        ("shirt", "bi-person-standing"),
        ("polo", "bi-person-standing"),
        ("cap", "bi-emoji-sunglasses"),
        ("lanyard", "bi-person-badge"),
        ("mug", "bi-cup-hot"),
        ("bottle", "bi-droplet"),
        ("tote", "bi-handbag"),
        ("bag", "bi-handbag"),
        ("notebook", "bi-journal-bookmark"),
        ("umbrella", "bi-umbrella"),
        ("medal", "bi-award"),
        ("pin", "bi-pin-angle"),
    ];

    /// <summary>The icon class, or a generic gift icon.</summary>
    public static string For(string? name)
    {
        var lower = name?.ToLowerInvariant() ?? string.Empty;
        foreach (var (word, icon) in Map)
        {
            if (lower.Contains(word, StringComparison.Ordinal))
            {
                return icon;
            }
        }

        return "bi-gift";
    }
}
