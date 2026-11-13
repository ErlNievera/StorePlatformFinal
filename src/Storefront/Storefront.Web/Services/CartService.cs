using System.Text.Json;

namespace Storefront.Web.Services;

/// <summary>One product in the cart. Prices shown are Catalog's current price; Order snapshots it when the order is placed.</summary>
public sealed record CartLine(Guid ProductId, string Name, decimal UnitPrice, string Currency, int Quantity)
{
    /// <summary>UnitPrice x Quantity.</summary>
    public decimal LineTotal => UnitPrice * Quantity;
}

/// <summary>The shopping cart, kept in the session (server-side, keyed by an HttpOnly session cookie).</summary>
public sealed class CartService(IHttpContextAccessor httpContextAccessor)
{
    /// <summary>Most units of one product per order (matches the Order contract).</summary>
    public const int MaxQuantity = 100;

    private const string SessionKey = "store.cart";

    private ISession Session => httpContextAccessor.HttpContext?.Session
        ?? throw new InvalidOperationException("The cart needs an HTTP request with a session.");

    /// <summary>Lines in the order they were added.</summary>
    public IReadOnlyList<CartLine> Lines
    {
        get
        {
            var json = Session.GetString(SessionKey);
            return string.IsNullOrEmpty(json)
                ? []
                : JsonSerializer.Deserialize<List<CartLine>>(json) ?? [];
        }
    }

    /// <summary>Number of units in the cart.</summary>
    public int ItemCount => Lines.Sum(l => l.Quantity);

    /// <summary>Sum of the line totals.</summary>
    public decimal Total => Lines.Sum(l => l.LineTotal);

    /// <summary>Adds units of a product (capped at <see cref="MaxQuantity"/>).</summary>
    public void Add(Guid productId, string name, decimal unitPrice, string currency, int quantity)
    {
        var lines = Lines.ToList();
        var index = lines.FindIndex(l => l.ProductId == productId);
        if (index >= 0)
        {
            var existing = lines[index];
            lines[index] = existing with { UnitPrice = unitPrice, Quantity = Math.Min(MaxQuantity, existing.Quantity + quantity) };
        }
        else
        {
            lines.Add(new CartLine(productId, name, unitPrice, currency, Math.Clamp(quantity, 1, MaxQuantity)));
        }

        Save(lines);
    }

    /// <summary>Sets the quantity of a line; zero removes it.</summary>
    public void Update(Guid productId, int quantity)
    {
        var lines = Lines.ToList();
        var index = lines.FindIndex(l => l.ProductId == productId);
        if (index < 0)
        {
            return;
        }

        if (quantity <= 0)
        {
            lines.RemoveAt(index);
        }
        else
        {
            lines[index] = lines[index] with { Quantity = Math.Min(MaxQuantity, quantity) };
        }

        Save(lines);
    }

    /// <summary>Removes a line.</summary>
    public void Remove(Guid productId) => Update(productId, 0);

    /// <summary>Empties the cart.</summary>
    public void Clear() => Session.Remove(SessionKey);

    private void Save(List<CartLine> lines) => Session.SetString(SessionKey, JsonSerializer.Serialize(lines));
}
