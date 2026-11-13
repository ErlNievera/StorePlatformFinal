namespace Inventory.Api.Data;

/// <summary>
/// Starting stock for the demo catalog. The product ids are shared with Catalog's seed data:
/// Catalog.Api must seed the same ids so every sellable product has stock.
/// </summary>
public static class InventorySeedData
{
    /// <summary>Product with a single unit, used for the insufficient-stock demo (order 2 of it).</summary>
    public static readonly Guid LimitedEditionProductId = Guid.Parse("ac000000-0000-4000-a000-000000000010");

    /// <summary>Seeded stock: product id, display name (for logs only; Catalog owns names) and units on hand.</summary>
    public static IReadOnlyList<SeedStock> Items { get; } =
    [
        new(Guid.Parse("ac000000-0000-4000-a000-000000000001"), "UST Angelicum College Hoodie", 40),
        new(Guid.Parse("ac000000-0000-4000-a000-000000000002"), "Angelicum Polo Shirt", 60),
        new(Guid.Parse("ac000000-0000-4000-a000-000000000003"), "Gold Seal Lanyard", 120),
        new(Guid.Parse("ac000000-0000-4000-a000-000000000004"), "Caritas Coffee Mug", 75),
        new(Guid.Parse("ac000000-0000-4000-a000-000000000005"), "Campus Tote Bag", 50),
        new(Guid.Parse("ac000000-0000-4000-a000-000000000006"), "Fortitudo Water Bottle", 80),
        new(Guid.Parse("ac000000-0000-4000-a000-000000000007"), "Justitia Notebook Set", 100),
        new(Guid.Parse("ac000000-0000-4000-a000-000000000008"), "Blue and Gold Umbrella", 30),
        new(Guid.Parse("ac000000-0000-4000-a000-000000000009"), "Angelicum Baseball Cap", 45),
        new(LimitedEditionProductId, "Founders' Commemorative Medal", 1),
    ];

    /// <summary>One seeded stock record.</summary>
    public sealed record SeedStock(Guid ProductId, string Name, int QuantityOnHand);
}
