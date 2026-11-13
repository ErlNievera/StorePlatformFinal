using Microsoft.EntityFrameworkCore;
using Payment.Api.Application;
using Payment.Api.Data;
using Payment.Api.Messaging;
using Store.Messaging;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("PaymentDb");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:PaymentDb is not configured. Set the ConnectionStrings__PaymentDb environment variable.");
}

builder.Services.AddSingleton(TimeProvider.System);
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

await app.InitializePaymentDatabaseAsync(app.Lifetime.ApplicationStopping);

app.MapHealthChecks("/health");

app.Run();
