using Identity.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Identity.Api.Data;

/// <summary>
/// Applies migrations, creates the Customer and Admin roles and the seeded demo accounts at
/// startup, retrying while PostgreSQL is starting.
/// </summary>
public static class IdentityDatabaseInitializer
{
    /// <summary>Migrates and seeds; retries up to <c>Database:MaxStartupAttempts</c> times (default 10).</summary>
    public static async Task InitializeIdentityDatabaseAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(IdentityDatabaseInitializer));
        var maxAttempts = Math.Max(1, app.Configuration.GetValue("Database:MaxStartupAttempts", 10));

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var scope = app.Services.CreateAsyncScope();
                var services = scope.ServiceProvider;
                await services.GetRequiredService<StoreIdentityDbContext>().Database.MigrateAsync(cancellationToken);

                var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
                foreach (var role in Roles.All)
                {
                    if (!await roleManager.RoleExistsAsync(role))
                    {
                        EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole<Guid>(role)), $"create role {role}");
                    }
                }

                var seed = services.GetRequiredService<IOptions<SeedOptions>>().Value;
                var userManager = services.GetRequiredService<UserManager<AppUser>>();
                var clock = services.GetRequiredService<TimeProvider>();
                await EnsureUserAsync(userManager, clock, logger, seed.AdminEmail, seed.AdminPassword, seed.AdminFullName, Roles.Admin);
                await EnsureUserAsync(userManager, clock, logger, seed.CustomerEmail, seed.CustomerPassword, seed.CustomerFullName, Roles.Customer);

                logger.LogInformation("Identity database is migrated and seeded on attempt {Attempt}", attempt);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException && ex is not SeedConfigurationException && attempt < maxAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempt)));
                logger.LogWarning(
                    ex,
                    "Identity database is not ready (attempt {Attempt} of {MaxAttempts}); retrying in {DelaySeconds}s",
                    attempt, maxAttempts, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private static async Task EnsureUserAsync(
        UserManager<AppUser> userManager,
        TimeProvider clock,
        ILogger logger,
        string email,
        string password,
        string fullName,
        string role)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("No seed {Role} account configured (Seed__{Role}Email / Seed__{Role}Password are empty)", role, role, role);
            return;
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new AppUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                CreatedAt = clock.GetUtcNow(),
            };
            var created = await userManager.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                throw new SeedConfigurationException(
                    $"Seed {role} account {email} could not be created: {string.Join("; ", created.Errors.Select(e => e.Description))}");
            }

            logger.LogInformation("Seeded {Role} account {Email}", role, email);
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            EnsureSucceeded(await userManager.AddToRoleAsync(user, role), $"add {email} to {role}");
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not {action}: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }
    }

    /// <summary>The seed settings are invalid (e.g. a password that fails the policy); retrying cannot help.</summary>
    private sealed class SeedConfigurationException(string message) : InvalidOperationException(message);
}
