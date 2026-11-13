using Microsoft.EntityFrameworkCore;
using Payment.Api.Application;
using Payment.Api.Data;
using Payment.Api.Messaging;
using Store.Messaging;
using Store.Observability;
using Store.Observability.Api;
using Store.Observability.Security;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddStoreConsoleLogging();

var connectionString = builder.Configuration.GetConnectionString("PaymentDb");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:PaymentDb is not configured. Set the ConnectionStrings__PaymentDb environment variable.");
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddStoreObservability();
builder.Services.AddStoreJwtAuthentication(builder.Configuration);
builder.Services.AddStoreApi("Store Platform - Payment API", "Charges and refunds for orders, plus an on-demand failure switch for saga demos.");
builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(maxRetryCount: 5)));
builder.Services.AddScoped<PaymentProcessor>();

builder.Services.AddStoreMessaging(builder.Configuration);
builder.Services.AddEventConsumer<ChargePaymentRequestedConsumer>();
builder.Services.AddEventConsumer<RefundPaymentRequestedConsumer>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<PaymentDbContext>("database")
    .AddRabbitMqHealthCheck();

var app = builder.Build();

app.UseStoreObservability();
if (app.Environment.IsDevelopment())
{
    app.UseStoreSwagger();
}

app.UseAuthentication();
app.UseAuthorization();

await app.InitializePaymentDatabaseAsync(app.Lifetime.ApplicationStopping);

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
