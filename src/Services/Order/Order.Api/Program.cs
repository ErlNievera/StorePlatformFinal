using Microsoft.EntityFrameworkCore;
using Order.Api.Data;
using Store.Observability;
using Order.Api.Clients;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddStoreConsoleLogging();

var connectionString = builder.Configuration.GetConnectionString("OrderDb");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:OrderDb is not configured. Set the ConnectionStrings__OrderDb environment variable.");
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddCatalogClient(builder.Configuration);
builder.Services.AddStoreObservability();
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(maxRetryCount: 5)));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<OrderDbContext>("database");

var app = builder.Build();

app.UseStoreObservability();

await app.InitializeOrderDatabaseAsync(app.Lifetime.ApplicationStopping);

app.MapHealthChecks("/health");

app.Run();
