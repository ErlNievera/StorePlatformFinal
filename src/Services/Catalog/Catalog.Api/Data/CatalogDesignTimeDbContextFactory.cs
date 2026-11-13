using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Catalog.Api.Data;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> build the model without running the app or reaching a
/// database. Uses <c>ConnectionStrings__CatalogDb</c> when set.
/// </summary>
public sealed class CatalogDesignTimeDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    /// <inheritdoc />
    public CatalogDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__CatalogDb")
            ?? "Host=localhost;Database=catalog_db;Username=store";

        return new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(connectionString).Options);
    }
}
