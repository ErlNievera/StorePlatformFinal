using System.Text.Json;
using System.Text.Json.Serialization;

// Partial hooks for every generated client:
// - NSwag puts JsonStringEnumConverter on single enum properties but not on enum arrays
//   (for example AuthResponse.user.roles: ["Customer"]); it is added for the whole client.
// - ReadResponseAsString keeps the response text on ApiException, so ApiErrors can show the
//   service's own Problem Details detail (e.g. "Cannot remove 5 unit(s): only 1 on hand.").

namespace Storefront.Web.Clients.Identity
{
    public partial class IdentityClient
    {
        partial void Initialize() => ReadResponseAsString = true;

        static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings) =>
            settings.Converters.Add(new JsonStringEnumConverter());
    }
}

namespace Storefront.Web.Clients.Catalog
{
    public partial class CatalogClient
    {
        partial void Initialize() => ReadResponseAsString = true;

        static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings) =>
            settings.Converters.Add(new JsonStringEnumConverter());
    }
}

namespace Storefront.Web.Clients.Inventory
{
    public partial class InventoryClient
    {
        partial void Initialize() => ReadResponseAsString = true;

        static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings) =>
            settings.Converters.Add(new JsonStringEnumConverter());
    }
}

namespace Storefront.Web.Clients.Orders
{
    public partial class OrderClient
    {
        partial void Initialize() => ReadResponseAsString = true;

        static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings) =>
            settings.Converters.Add(new JsonStringEnumConverter());
    }
}

namespace Storefront.Web.Clients.Payments
{
    public partial class PaymentClient
    {
        partial void Initialize() => ReadResponseAsString = true;

        static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings) =>
            settings.Converters.Add(new JsonStringEnumConverter());
    }
}
