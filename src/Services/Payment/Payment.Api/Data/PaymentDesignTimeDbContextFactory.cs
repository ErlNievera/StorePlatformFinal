using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Payment.Api.Data;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> build the model without running the app or reaching a
/// database. Uses <c>ConnectionStrings__PaymentDb</c> when set (needed only for
/// <c>dotnet ef database update</c>).
/// </summary>
public sealed class PaymentDesignTimeDbContextFactory : IDesignTimeDbContextFactory<PaymentDbContext>
{
    /// <inheritdoc />
    public PaymentDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__PaymentDb")
            ?? "Host=localhost;Database=payment_db;Username=store";

        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new PaymentDbContext(options);
    }
}
