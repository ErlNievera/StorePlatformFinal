namespace Inventory.Api.Domain;

/// <summary>
/// A stock change that would break an invariant (negative stock, reserving more than is available,
/// releasing more than is reserved). Mapped to HTTP 409 by the API.
/// </summary>
public sealed class StockConflictException(string message) : InvalidOperationException(message);
