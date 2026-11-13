using Catalog.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Data;

/// <summary>Catalog's own database (database-per-service).</summary>
public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    /// <summary>Products.</summary>
    public DbSet<Product> Products => Set<Product>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Products", table =>
                table.HasCheckConstraint("CK_Products_Price_Positive", "\"Price\" > 0"));
            entity.HasKey(p => p.ProductId);
            entity.Property(p => p.ProductId).ValueGeneratedNever();
            entity.Property(p => p.Name).HasMaxLength(Product.NameMaxLength).IsRequired();
            entity.HasIndex(p => p.Name).IsUnique();
            entity.Property(p => p.Description).HasMaxLength(Product.DescriptionMaxLength).IsRequired();
            entity.Property(p => p.Price).HasPrecision(18, 2);
            entity.Property(p => p.Currency).HasMaxLength(3).IsFixedLength().IsRequired();
            entity.HasIndex(p => p.IsActive);
            entity.Property(p => p.Version).IsRowVersion();
        });
    }
}
