using Catalog.Api.Domain;

namespace Catalog.Api.Data;

/// <summary>
/// Demo catalog. The ids match Inventory's seed stock (InventorySeedData), so every product
/// starts with stock; the Founders' Commemorative Medal has 1 unit for the insufficient-stock demo.
/// </summary>
public static class CatalogSeedData
{
    /// <summary>Seeded products.</summary>
    public static IReadOnlyList<SeedProduct> Products { get; } =
    [
        new(Guid.Parse("ac000000-0000-4000-a000-000000000001"), "UST Angelicum College Hoodie",
            "Heavyweight navy hoodie with the gold college seal embroidered on the chest.", 1299.00m),
        new(Guid.Parse("ac000000-0000-4000-a000-000000000002"), "Angelicum Polo Shirt",
            "Breathable piqué polo in Angelicum blue with a gold-thread crest.", 799.00m),
        new(Guid.Parse("ac000000-0000-4000-a000-000000000003"), "Gold Seal Lanyard",
            "Woven lanyard in blue and gold with a detachable ID clip.", 149.00m),
        new(Guid.Parse("ac000000-0000-4000-a000-000000000004"), "Caritas Coffee Mug",
            "12 oz ceramic mug printed with the Caritas motto. Dishwasher safe.", 349.00m),
        new(Guid.Parse("ac000000-0000-4000-a000-000000000005"), "Campus Tote Bag",
            "Heavy canvas tote with an inside pocket, sized for a laptop and notebooks.", 399.00m),
        new(Guid.Parse("ac000000-0000-4000-a000-000000000006"), "Fortitudo Water Bottle",
            "750 ml insulated steel bottle engraved with the Fortitudo motto.", 499.00m),
        new(Guid.Parse("ac000000-0000-4000-a000-000000000007"), "Justitia Notebook Set",
            "Set of three A5 notebooks, dotted, lined and plain, with gold foil covers.", 279.00m),
        new(Guid.Parse("ac000000-0000-4000-a000-000000000008"), "Blue and Gold Umbrella",
            "Auto-open 27-inch umbrella in college colours with a wind-resistant frame.", 599.00m),
        new(Guid.Parse("ac000000-0000-4000-a000-000000000009"), "Angelicum Baseball Cap",
            "Adjustable cotton cap with an embroidered AC monogram.", 449.00m),
        new(Guid.Parse("ac000000-0000-4000-a000-000000000010"), "Founders' Commemorative Medal",
            "Limited-edition brass medal marking the college founders. One left in stock.", 2500.00m),
    ];

    /// <summary>One seeded product.</summary>
    public sealed record SeedProduct(Guid ProductId, string Name, string Description, decimal Price)
    {
        /// <summary>Creates the entity.</summary>
        public Product ToEntity(DateTimeOffset now) =>
            new(ProductId, Name, Description, Price, Product.DefaultCurrency, isActive: true, now);
    }
}
