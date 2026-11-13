using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Inventory.Api.Data;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> build the model without running the app or reaching a
/// database. Uses <c>ConnectionStrings__InventoryDb</c> when set (needed only for
/// <c>dotnet ef database update</c>).
/// </summary>
public sealed class InventoryDesignTimeDbContextFactory : IDesignTimeDbContextFactory<InventoryDbContext>
{
    /// <inheritdoc />
    public InventoryDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__InventoryDb")
            ?? "Host=localhost;Database=inventory_db;Username=store";

        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new InventoryDbContext(options);
    }
}
