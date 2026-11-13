using Microsoft.EntityFrameworkCore;
using Payment.Api.Data;

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

builder.Services.AddHealthChecks()
    .AddDbContextCheck<PaymentDbContext>("database");

var app = builder.Build();

await app.InitializePaymentDatabaseAsync(app.Lifetime.ApplicationStopping);

app.MapHealthChecks("/health");

app.Run();
