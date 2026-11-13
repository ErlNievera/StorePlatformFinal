using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Store.Observability;
using Storefront.Web.Clients;
using Storefront.Web.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddStoreConsoleLogging();

builder.Services.TryAddSingleton<ICorrelationIdAccessor, CorrelationIdAccessor>();
builder.Services.TryAddTransient<CorrelationIdDelegatingHandler>();
builder.Services.AddGatewayClients(builder.Configuration);
builder.Services.AddScoped<CartService>();

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<GatewayAuthFailureFilter>();
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "store.session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromHours(4);
});

// The JWT from Identity lives inside this HttpOnly, encrypted cookie (saved as the access_token)
// and is sent to the gateway as a Bearer token by GatewayBearerTokenHandler.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "store.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.LoginPath = "/account/login";
        options.LogoutPath = "/account/logout";
        options.AccessDeniedPath = "/error/403";
        options.SlidingExpiration = false;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    });
builder.Services.AddAuthorization();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler("/error/500");
app.UseStatusCodePagesWithReExecute("/error/{0}");
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
