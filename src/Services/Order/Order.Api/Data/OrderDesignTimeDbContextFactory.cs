using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Order.Api.Data;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> build the model without running the app or reaching a
/// database. Uses <c>ConnectionStrings__OrderDb</c> when set.
/// </summary>
public sealed class OrderDesignTimeDbContextFactory : IDesignTimeDbContextFactory<OrderDbContext>
{
    /// <inheritdoc />
    public OrderDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__OrderDb")
            ?? "Host=localhost;Database=order_db;Username=store";

        return new OrderDbContext(new DbContextOptionsBuilder<OrderDbContext>().UseNpgsql(connectionString).Options);
    }
}
