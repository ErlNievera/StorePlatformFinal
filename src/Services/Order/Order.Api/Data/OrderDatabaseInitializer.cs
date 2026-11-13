using Microsoft.EntityFrameworkCore;
using Order.Api.Domain;

namespace Order.Api.Data;

/// <summary>Applies migrations and seeds the settings row at startup, retrying while PostgreSQL is starting.</summary>
public static class OrderDatabaseInitializer
{
    /// <summary>Migrates and creates the failure-mode row (None) when missing; retries up to <c>Database:MaxStartupAttempts</c> times.</summary>
    public static async Task InitializeOrderDatabaseAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(OrderDatabaseInitializer));
        var maxAttempts = Math.Max(1, app.Configuration.GetValue("Database:MaxStartupAttempts", 10));

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var scope = app.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
                var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

                await db.Database.MigrateAsync(cancellationToken);
                var seeded = false;
                if (!await db.Settings.AnyAsync(s => s.Id == OrderSettings.SingletonId, cancellationToken))
                {
                    db.Settings.Add(new OrderSettings(OrderFailureMode.None, clock.GetUtcNow()));
                    await db.SaveChangesAsync(cancellationToken);
                    seeded = true;
                }

                logger.LogInformation(
                    "Order database is migrated; settings row {SettingsState} on attempt {Attempt}",
                    seeded ? "created with failure mode None" : "already present", attempt);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException && attempt < maxAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempt)));
                logger.LogWarning(
                    ex,
                    "Order database is not ready (attempt {Attempt} of {MaxAttempts}); retrying in {DelaySeconds}s",
                    attempt, maxAttempts, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}
