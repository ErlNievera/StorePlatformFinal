namespace Store.Observability;

/// <summary>Names shared by every service for the correlation id.</summary>
public static class Correlation
{
    /// <summary>HTTP header (and RabbitMQ header) that carries the correlation id.</summary>
    public const string HeaderName = "X-Correlation-ID";

    /// <summary>Key used in logging scopes.</summary>
    public const string LogScopeKey = "CorrelationId";

    /// <summary>Problem Details extension member that carries the correlation id.</summary>
    public const string ProblemDetailsKey = "correlationId";

    /// <summary>Longest correlation id accepted from a caller; longer or malformed values are replaced.</summary>
    public const int MaxLength = 128;

    /// <summary>Creates a new correlation id (32 hex characters).</summary>
    public static string NewId() => Guid.NewGuid().ToString("N");

    /// <summary>
    /// Returns <paramref name="candidate"/> when it is a safe correlation id (1-128 characters of
    /// letters, digits, '-', '_', '.', ':'), otherwise <see langword="null"/>. This keeps
    /// caller-supplied values from injecting content into logs or headers.
    /// </summary>
    public static string? Sanitize(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Length > MaxLength)
        {
            return null;
        }

        foreach (var c in candidate)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':'))
            {
                return null;
            }
        }

        return candidate;
    }
}
