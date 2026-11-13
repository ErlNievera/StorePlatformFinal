using Microsoft.EntityFrameworkCore;
using Payment.Api.Domain;

namespace Payment.Api.Data;

/// <summary>Applies migrations and seeds the settings row at startup, retrying while PostgreSQL is starting.</summary>
public static class PaymentDatabaseInitializer
{
    /// <summary>
    /// Migrates the database and creates the failure-mode settings row (mode <c>None</c>) if it is
    /// missing. Retries up to <c>Database:MaxStartupAttempts</c> times (default 10) with backoff.
    /// </summary>
    public static async Task InitializePaymentDatabaseAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(PaymentDatabaseInitializer));
        var maxAttempts = Math.Max(1, app.Configuration.GetValue("Database:MaxStartupAttempts", 10));

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var scope = app.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
                var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

                await db.Database.MigrateAsync(cancellationToken);

                var seeded = false;
                if (!await db.Settings.AnyAsync(s => s.Id == PaymentSettings.SingletonId, cancellationToken))
                {
                    db.Settings.Add(new PaymentSettings(PaymentFailureMode.None, clock.GetUtcNow()));
                    await db.SaveChangesAsync(cancellationToken);
                    seeded = true;
                }

                logger.LogInformation(
                    "Payment database is migrated; settings row {SettingsState} on attempt {Attempt}",
                    seeded ? "created with failure mode None" : "already present", attempt);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException && attempt < maxAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempt)));
                logger.LogWarning(
                    ex,
                    "Payment database is not ready (attempt {Attempt} of {MaxAttempts}); retrying in {DelaySeconds}s",
                    attempt, maxAttempts, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}
