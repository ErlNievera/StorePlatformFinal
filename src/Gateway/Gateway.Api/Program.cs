using Gateway.Api;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ocelot.Provider.Polly;
using Store.Observability;
using Store.Observability.Security;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddStoreConsoleLogging();
builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: false);

// Same issuer, audience and key as Identity and every service (Jwt__* from .env).
// Routes with AuthenticationProviderKeys ["Bearer"] are rejected here with 401 before reaching a service.
builder.Services.AddStoreJwtAuthentication(builder.Configuration);
builder.Services.AddHealthChecks();
builder.Services.AddOcelot(builder.Configuration).AddPolly();

var app = builder.Build();

app.UseMiddleware<EdgeCorrelationMiddleware>();
app.UseHealthChecks("/health");
app.UseAuthentication();
await app.UseOcelot();

await app.RunAsync();
