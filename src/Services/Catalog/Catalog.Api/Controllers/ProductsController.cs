using Asp.Versioning;
using Catalog.Api.Chaos;
using Catalog.Api.Contracts;
using Catalog.Api.Data;
using Catalog.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Observability;
using Store.Observability.Security;

namespace Catalog.Api.Controllers;

/// <summary>Products (catalog.v1.yaml, tag Products).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/products")]
[Produces("application/json")]
[TypeFilter(typeof(ChaosFilter))]
public sealed class ProductsController(CatalogDbContext db, TimeProvider clock, ILogger<ProductsController> logger) : ControllerBase
{
    /// <summary>operationId <c>listProducts</c>: active products by name; inactive ones too for Admins.</summary>
    [HttpGet(Name = "listProducts")]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<ProductResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> ListProducts(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        if (includeInactive)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Challenge();
            }

            if (!User.IsInRole(StoreClaims.Roles.Admin))
            {
                return Forbid();
            }
        }

        var query = db.Products.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        var products = await query.OrderBy(p => p.Name).ToListAsync(cancellationToken);
        return Ok(products.Select(ProductResponse.From).ToList());
    }

    /// <summary>operationId <c>getProduct</c>: one product, active or not.</summary>
    [HttpGet("{productId:guid}", Name = "getProduct")]
    [AllowAnonymous]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> GetProduct(Guid productId, CancellationToken cancellationToken)
    {
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.ProductId == productId, cancellationToken)
            ?? throw new NotFoundException($"Product {productId} does not exist.");
        return Ok(ProductResponse.From(product));
    }

    /// <summary>operationId <c>createProduct</c> (Admin).</summary>
    [HttpPost(Name = "createProduct")]
    [Authorize(Roles = StoreClaims.Roles.Admin)]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductResponse>> CreateProduct(ProductRequest request, CancellationToken cancellationToken)
    {
        await EnsureNameIsFreeAsync(request.Name, exceptProductId: null, cancellationToken);

        var product = new Product(
            request.Name,
            request.Description,
            request.Price,
            request.Currency ?? Product.DefaultCurrency,
            request.IsActive!.Value,
            clock.GetUtcNow());
        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created product {ProductId} {Name} at {Price} {Currency}",
            product.ProductId, product.Name, product.Price, product.Currency);
        return CreatedAtRoute("getProduct", new { productId = product.ProductId, version = "1" }, ProductResponse.From(product));
    }

    /// <summary>operationId <c>updateProduct</c> (Admin).</summary>
    [HttpPut("{productId:guid}", Name = "updateProduct")]
    [Authorize(Roles = StoreClaims.Roles.Admin)]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductResponse>> UpdateProduct(Guid productId, ProductRequest request, CancellationToken cancellationToken)
    {
        var product = await db.Products.SingleOrDefaultAsync(p => p.ProductId == productId, cancellationToken)
            ?? throw new NotFoundException($"Product {productId} does not exist.");
        await EnsureNameIsFreeAsync(request.Name, productId, cancellationToken);

        product.Update(
            request.Name,
            request.Description,
            request.Price,
            request.Currency ?? product.Currency,
            request.IsActive!.Value,
            clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Updated product {ProductId}: {Name}, {Price} {Currency}, active {IsActive}",
            product.ProductId, product.Name, product.Price, product.Currency, product.IsActive);
        return Ok(ProductResponse.From(product));
    }

    private async Task EnsureNameIsFreeAsync(string name, Guid? exceptProductId, CancellationToken cancellationToken)
    {
        var trimmed = name.Trim();
        var taken = await db.Products.AnyAsync(
            p => p.Name.ToLower() == trimmed.ToLower() && (exceptProductId == null || p.ProductId != exceptProductId),
            cancellationToken);
        if (taken)
        {
            throw new ConflictException($"A product named '{trimmed}' already exists.");
        }
    }
}
