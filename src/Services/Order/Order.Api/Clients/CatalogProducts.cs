using Polly.CircuitBreaker;
using Polly.Timeout;
using Order.Api.Clients.Catalog;
using Store.Observability;

namespace Order.Api.Clients;

/// <summary>What Order.Api needs to know about a Catalog product.</summary>
public sealed record CatalogProductInfo(Guid ProductId, string Name, decimal Price, string Currency, bool IsActive);

/// <summary>Result of a Catalog lookup: the product, or <see cref="NotFound"/>.</summary>
public sealed record CatalogProductLookup(CatalogProductInfo? Product)
{
    /// <summary>The product does not exist in Catalog.</summary>
    public static CatalogProductLookup NotFound { get; } = new((CatalogProductInfo?)null);

    /// <summary>True when Catalog knows the product.</summary>
    public bool Found => Product is not null;
}

/// <summary>Reads products from Catalog through the generated client.</summary>
public interface ICatalogProducts
{
    /// <summary>
    /// Looks up a product. Returns <see cref="CatalogProductLookup.NotFound"/> for 404 and throws
    /// <see cref="DownstreamServiceException"/> when Catalog fails or cannot be reached after the
    /// resilience pipeline (retries, timeouts, circuit breaker) gives up.
    /// </summary>
    Task<CatalogProductLookup> FindProductAsync(Guid productId, CancellationToken cancellationToken);
}

/// <summary>
/// Adapter around the NSwag-generated <see cref="ICatalogClient"/>: translates 404 into a
/// not-found result and every other failure into <see cref="DownstreamServiceException"/> (503).
/// </summary>
public sealed class CatalogProductsAdapter(ICatalogClient client, ILogger<CatalogProductsAdapter> logger) : ICatalogProducts
{
    private const string ServiceName = "Catalog";

    /// <inheritdoc />
    public async Task<CatalogProductLookup> FindProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        try
        {
            var product = await client.GetProductAsync(productId, cancellationToken);
            return new CatalogProductLookup(new CatalogProductInfo(
                product.ProductId, product.Name, (decimal)product.Price, product.Currency, product.IsActive));
        }
        catch (CatalogApiException ex) when (ex.StatusCode == StatusCodes.Status404NotFound)
        {
            return CatalogProductLookup.NotFound;
        }
        catch (CatalogApiException ex)
        {
            logger.LogWarning(ex, "Catalog answered {StatusCode} for product {ProductId}", ex.StatusCode, productId);
            throw new DownstreamServiceException(ServiceName, $"Catalog answered {ex.StatusCode} while looking up product {productId}.", ex);
        }
        catch (BrokenCircuitException ex)
        {
            logger.LogWarning(ex, "Catalog circuit is open; product {ProductId} not looked up", productId);
            throw new DownstreamServiceException(ServiceName, "Catalog is failing repeatedly; calls are paused for a few seconds.", ex);
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutRejectedException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning(ex, "Catalog could not be reached for product {ProductId}", productId);
            throw new DownstreamServiceException(ServiceName, "Catalog could not be reached.", ex);
        }
    }
}
