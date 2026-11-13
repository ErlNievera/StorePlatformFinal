using Inventory.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Data;

/// <summary>Applies migrations and seeds stock at startup, retrying while PostgreSQL is starting.</summary>
public static class InventoryDatabaseInitializer
{
    /// <summary>
    /// Migrates the database and inserts any seed stock record that does not exist yet.
    /// Retries up to <c>Database:MaxStartupAttempts</c> times (default 10) with backoff.
    /// </summary>
    public static async Task InitializeInventoryDatabaseAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(InventoryDatabaseInitializer));
        var maxAttempts = Math.Max(1, app.Configuration.GetValue("Database:MaxStartupAttempts", 10));

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var scope = app.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
                var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

                await db.Database.MigrateAsync(cancellationToken);
                var added = await SeedAsync(db, clock, cancellationToken);
                logger.LogInformation(
                    "Inventory database is migrated; {Added} seed stock record(s) added on attempt {Attempt}",
                    added, attempt);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException && attempt < maxAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempt)));
                logger.LogWarning(
                    ex,
                    "Inventory database is not ready (attempt {Attempt} of {MaxAttempts}); retrying in {DelaySeconds}s",
                    attempt, maxAttempts, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private static async Task<int> SeedAsync(InventoryDbContext db, TimeProvider clock, CancellationToken cancellationToken)
    {
        var existing = await db.StockItems
            .Select(s => s.ProductId)
            .ToHashSetAsync(cancellationToken);

        var now = clock.GetUtcNow();
        var missing = InventorySeedData.Items
            .Where(seed => !existing.Contains(seed.ProductId))
            .Select(seed => new StockItem(seed.ProductId, seed.QuantityOnHand, now))
            .ToList();

        if (missing.Count == 0)
        {
            return 0;
        }

        db.StockItems.AddRange(missing);
        await db.SaveChangesAsync(cancellationToken);
        return missing.Count;
    }
}
