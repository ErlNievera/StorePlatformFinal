using Store.Observability;
using Storefront.Web.Clients.Catalog;
using Storefront.Web.Clients.Identity;
using Storefront.Web.Clients.Inventory;
using Storefront.Web.Clients.Orders;
using Storefront.Web.Clients.Payments;

namespace Storefront.Web.Clients;

/// <summary>
/// Registers the five generated clients. Every client talks to the Ocelot gateway only
/// (<c>Gateway:BaseUrl</c> + <c>/&lt;service&gt;/v1/</c>), forwards the correlation id and sends the
/// signed-in user's JWT as a Bearer token.
/// </summary>
public static class GatewayClientRegistration
{
    /// <summary>Configuration key of the gateway base URL, e.g. <c>http://gateway:8080</c>.</summary>
    public const string BaseUrlKey = "Gateway:BaseUrl";

    /// <summary>Adds the typed clients and their handlers.</summary>
    public static IServiceCollection AddGatewayClients(this IServiceCollection services, IConfiguration configuration)
    {
        var configured = configuration[BaseUrlKey];
        if (string.IsNullOrWhiteSpace(configured) || !Uri.TryCreate(configured, UriKind.Absolute, out var gateway))
        {
            throw new InvalidOperationException($"{BaseUrlKey} is not configured. Set Gateway__BaseUrl, e.g. http://gateway:8080.");
        }

        services.AddHttpContextAccessor();
        services.AddTransient<GatewayBearerTokenHandler>();

        Add<IIdentityClient, IdentityClient>(services, gateway, "identity/v1/");
        Add<ICatalogClient, CatalogClient>(services, gateway, "catalog/v1/");
        Add<IInventoryClient, InventoryClient>(services, gateway, "inventory/v1/");
        Add<IOrderClient, OrderClient>(services, gateway, "orders/v1/");
        Add<IPaymentClient, PaymentClient>(services, gateway, "payments/v1/");
        return services;
    }

    private static void Add<TClient, TImplementation>(IServiceCollection services, Uri gateway, string path)
        where TClient : class
        where TImplementation : class, TClient
    {
        services.AddHttpClient<TClient, TImplementation>(client =>
            {
                client.BaseAddress = new Uri(gateway, path);
                client.Timeout = TimeSpan.FromSeconds(15);
            })
            .AddHttpMessageHandler<CorrelationIdDelegatingHandler>()
            .AddHttpMessageHandler<GatewayBearerTokenHandler>();
    }
}
