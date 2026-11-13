using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Data;

/// <summary>Applies migrations and seeds products at startup, retrying while PostgreSQL is starting.</summary>
public static class CatalogDatabaseInitializer
{
    /// <summary>
    /// Migrates the database and inserts any seed product that does not exist yet.
    /// Retries up to <c>Database:MaxStartupAttempts</c> times (default 10) with backoff.
    /// </summary>
    public static async Task InitializeCatalogDatabaseAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(CatalogDatabaseInitializer));
        var maxAttempts = Math.Max(1, app.Configuration.GetValue("Database:MaxStartupAttempts", 10));

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var scope = app.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
                var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

                await db.Database.MigrateAsync(cancellationToken);

                var existing = await db.Products.Select(p => p.ProductId).ToHashSetAsync(cancellationToken);
                var now = clock.GetUtcNow();
                var missing = CatalogSeedData.Products
                    .Where(seed => !existing.Contains(seed.ProductId))
                    .Select(seed => seed.ToEntity(now))
                    .ToList();
                if (missing.Count > 0)
                {
                    db.Products.AddRange(missing);
                    await db.SaveChangesAsync(cancellationToken);
                }

                logger.LogInformation(
                    "Catalog database is migrated; {Added} seed product(s) added on attempt {Attempt}",
                    missing.Count, attempt);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException && attempt < maxAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempt)));
                logger.LogWarning(
                    ex,
                    "Catalog database is not ready (attempt {Attempt} of {MaxAttempts}); retrying in {DelaySeconds}s",
                    attempt, maxAttempts, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}
