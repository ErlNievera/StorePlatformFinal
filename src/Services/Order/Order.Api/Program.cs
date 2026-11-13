using Microsoft.EntityFrameworkCore;
using Order.Api.Data;
using Order.Api.Application;
using Order.Api.Clients;
using Order.Api.Saga;
using Store.Messaging;
using Store.Observability;
using Store.Observability.Api;
using Store.Observability.Security;

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
builder.Services.AddStoreJwtAuthentication(builder.Configuration);
builder.Services.AddStoreApi("Store Platform - Order API", "Orders, order lines and status history; orchestrates the order saga.");
builder.Services.AddScoped<OrderPlacementService>();
builder.Services.AddScoped<OrderSagaOrchestrator>();
builder.Services.AddStoreMessaging(builder.Configuration);
builder.Services.AddHostedService<StalledSagaMonitor>();
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(maxRetryCount: 5)));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<OrderDbContext>("database");

var app = builder.Build();

app.UseStoreObservability();
if (app.Environment.IsDevelopment())
{
    app.UseStoreSwagger();
}

app.UseAuthentication();
app.UseAuthorization();

await app.InitializeOrderDatabaseAsync(app.Lifetime.ApplicationStopping);

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
