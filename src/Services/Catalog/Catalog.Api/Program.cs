using Catalog.Api.Data;
using Microsoft.EntityFrameworkCore;
using Store.Observability;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddStoreConsoleLogging();

var connectionString = builder.Configuration.GetConnectionString("CatalogDb");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:CatalogDb is not configured. Set the ConnectionStrings__CatalogDb environment variable.");
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddStoreObservability();
builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(maxRetryCount: 5)));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<CatalogDbContext>("database");

var app = builder.Build();

app.UseStoreObservability();

await app.InitializeCatalogDatabaseAsync(app.Lifetime.ApplicationStopping);

app.MapHealthChecks("/health");

app.Run();
