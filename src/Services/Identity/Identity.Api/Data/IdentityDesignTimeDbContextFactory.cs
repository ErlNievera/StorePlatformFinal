using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Identity.Api.Data;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> build the model without running the app or reaching a
/// database. Uses <c>ConnectionStrings__IdentityDb</c> when set.
/// </summary>
public sealed class IdentityDesignTimeDbContextFactory : IDesignTimeDbContextFactory<StoreIdentityDbContext>
{
    /// <inheritdoc />
    public StoreIdentityDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__IdentityDb")
            ?? "Host=localhost;Database=identity_db;Username=store";

        return new StoreIdentityDbContext(new DbContextOptionsBuilder<StoreIdentityDbContext>().UseNpgsql(connectionString).Options);
    }
}
