using Inventory.Api.Application;
using Inventory.Api.Data;
using Inventory.Api.Messaging;
using Microsoft.EntityFrameworkCore;
using Store.Messaging;

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
builder.Services.AddScoped<StockReservationService>();

builder.Services.AddStoreMessaging(builder.Configuration);
builder.Services.AddEventConsumer<OrderPlacedConsumer>();
builder.Services.AddEventConsumer<StockReleaseRequestedConsumer>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<InventoryDbContext>("database")
    .AddRabbitMqHealthCheck();

var app = builder.Build();

await app.InitializeInventoryDatabaseAsync(app.Lifetime.ApplicationStopping);

app.MapHealthChecks("/health");

app.Run();
