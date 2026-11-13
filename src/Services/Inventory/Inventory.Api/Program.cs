using Inventory.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("InventoryDb");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:InventoryDb is not configured. Set the ConnectionStrings__InventoryDb environment variable.");
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(maxRetryCount: 5)));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<InventoryDbContext>("database");

var app = builder.Build();

await app.InitializeInventoryDatabaseAsync(app.Lifetime.ApplicationStopping);

app.MapHealthChecks("/health");

app.Run();
