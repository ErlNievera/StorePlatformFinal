using Identity.Api.Data;
using Identity.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Identity.Api.Security;
using Microsoft.EntityFrameworkCore;
using Store.Observability;
using Store.Observability.Api;
using Store.Observability.Security;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddStoreConsoleLogging();

var connectionString = builder.Configuration.GetConnectionString("IdentityDb");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:IdentityDb is not configured. Set the ConnectionStrings__IdentityDb environment variable.");
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddStoreObservability();
builder.Services.AddStoreJwtAuthentication(builder.Configuration);
builder.Services.AddStoreApi("Store Platform - Identity API", "Customer registration, login and the JWTs every other service trusts.");
builder.Services.AddSingleton<TokenService>();
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection(SeedOptions.SectionName));
builder.Services.AddDbContext<StoreIdentityDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(maxRetryCount: 5)));

builder.Services
    .AddIdentityCore<AppUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 10;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<StoreIdentityDbContext>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<StoreIdentityDbContext>("database");

var app = builder.Build();

app.UseStoreObservability();
if (app.Environment.IsDevelopment())
{
    app.UseStoreSwagger();
}

app.UseAuthentication();
app.UseAuthorization();

await app.InitializeIdentityDatabaseAsync(app.Lifetime.ApplicationStopping);

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
