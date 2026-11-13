using Catalog.Api.Chaos;
using Catalog.Api.Data;
using Microsoft.EntityFrameworkCore;
using Store.Observability;
using Store.Observability.Api;
using Store.Observability.Security;

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
builder.Services.AddStoreJwtAuthentication(builder.Configuration);
builder.Services.AddStoreApi("Store Platform - Catalog API", "Products that can be sold: name, description, price and active flag.");
builder.Services.AddSingleton<ChaosState>();
builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(maxRetryCount: 5)));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<CatalogDbContext>("database");

var app = builder.Build();

app.UseStoreObservability();
if (app.Environment.IsDevelopment())
{
    app.UseStoreSwagger();
}

app.UseAuthentication();
app.UseAuthorization();

await app.InitializeCatalogDatabaseAsync(app.Lifetime.ApplicationStopping);

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
