using Inventory.Api.Application;
using Inventory.Api.Data;
using Inventory.Api.Messaging;
using Microsoft.EntityFrameworkCore;
using Store.Messaging;
using Store.Observability;
using Store.Observability.Api;
using Store.Observability.Security;
using Inventory.Api.Clients;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddStoreConsoleLogging();

var connectionString = builder.Configuration.GetConnectionString("InventoryDb");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:InventoryDb is not configured. Set the ConnectionStrings__InventoryDb environment variable.");
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddStoreObservability();
builder.Services.AddStoreJwtAuthentication(builder.Configuration);
builder.Services.AddStoreApi("Store Platform - Inventory API", "Stock on hand and stock reservations per product.");
builder.Services.AddCatalogClient(builder.Configuration);
builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(maxRetryCount: 5)));
builder.Services.AddScoped<StockReservationService>();

builder.Services.AddStoreMessaging(builder.Configuration);
builder.Services.AddEventConsumer<OrderPlacedConsumer>();
builder.Services.AddEventConsumer<StockReleaseRequestedConsumer>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<InventoryDbContext>("database")
    .AddRabbitMqHealthCheck();

var app = builder.Build();

app.UseStoreObservability();
if (app.Environment.IsDevelopment())
{
    app.UseStoreSwagger();
}

app.UseAuthentication();
app.UseAuthorization();

await app.InitializeInventoryDatabaseAsync(app.Lifetime.ApplicationStopping);

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
