namespace Store.Observability;

/// <summary>The requested resource does not exist (HTTP 404).</summary>
public class NotFoundException(string message) : Exception(message);

/// <summary>The request conflicts with the current state of a resource (HTTP 409).</summary>
public class ConflictException(string message) : Exception(message);

/// <summary>The request is invalid in a way model validation cannot express (HTTP 400).</summary>
public class BadRequestException(string message) : Exception(message);

/// <summary>
/// A service this service depends on failed or could not be reached after the resilience
/// pipeline gave up (HTTP 503). Adapters around generated clients throw this.
/// </summary>
public class DownstreamServiceException(string serviceName, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    /// <summary>Name of the failing dependency, e.g. "Catalog".</summary>
    public string ServiceName { get; } = serviceName;
}
