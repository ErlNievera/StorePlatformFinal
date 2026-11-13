using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Inventory.Api.Clients.Catalog;
using Store.Observability;

namespace Inventory.Api.Clients;

/// <summary>Registers the typed Catalog client with correlation propagation and the standard resilience pipeline.</summary>
public static class CatalogClientRegistration
{
    /// <summary>Configuration key of Catalog's versioned base URL, e.g. <c>http://catalog-api:8080/api/v1/</c>.</summary>
    public const string BaseUrlKey = "Services:Catalog:BaseUrl";

    /// <summary>
    /// Adds <see cref="ICatalogClient"/> through IHttpClientFactory with the correlation id
    /// DelegatingHandler and <c>AddStandardResilienceHandler</c>: 3 retries with exponential
    /// backoff and jitter, 2 s per attempt, 10 s in total, and a circuit breaker that opens for
    /// 15 s when 80% of at least 10 calls in 30 s fail. Also adds <see cref="ICatalogProducts"/>.
    /// </summary>
    public static IServiceCollection AddCatalogClient(this IServiceCollection services, IConfiguration configuration)
    {
        var baseUrl = configuration[BaseUrlKey];
        if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(EnsureTrailingSlash(baseUrl), UriKind.Absolute, out var baseAddress))
        {
            throw new InvalidOperationException(
                $"{BaseUrlKey} is not configured. Set Services__Catalog__BaseUrl to Catalog's versioned base URL, e.g. http://catalog-api:8080/api/v1/.");
        }

        services.TryAddSingleton<ICorrelationIdAccessor, CorrelationIdAccessor>();
        services.TryAddTransient<CorrelationIdDelegatingHandler>();

        services.AddHttpClient<ICatalogClient, CatalogClient>(client => client.BaseAddress = baseAddress)
            .AddHttpMessageHandler<CorrelationIdDelegatingHandler>()
            .AddStandardResilienceHandler()
            .Configure((options, serviceProvider) =>
            {
                var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Inventory.Api.Clients.CatalogResilience");

                options.Retry.MaxRetryAttempts = 3;
                options.Retry.BackoffType = DelayBackoffType.Exponential;
                options.Retry.UseJitter = true;
                options.Retry.Delay = TimeSpan.FromMilliseconds(300);
                options.Retry.OnRetry = args =>
                {
                    logger.LogWarning(
                        "Retrying Catalog call (attempt {Attempt} of {MaxAttempts}) in {DelayMs} ms after {Outcome}",
                        args.AttemptNumber + 2,
                        options.Retry.MaxRetryAttempts + 1,
                        (int)args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.GetType().Name ?? $"HTTP {(int?)args.Outcome.Result?.StatusCode}");
                    return ValueTask.CompletedTask;
                };

                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);

                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
                options.CircuitBreaker.FailureRatio = 0.8;
                options.CircuitBreaker.MinimumThroughput = 10;
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
                options.CircuitBreaker.OnOpened = args =>
                {
                    logger.LogError("Catalog circuit opened for {BreakSeconds} s", args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                };
                options.CircuitBreaker.OnClosed = _ =>
                {
                    logger.LogInformation("Catalog circuit closed; calls flow normally again");
                    return ValueTask.CompletedTask;
                };
            });

        services.AddScoped<ICatalogProducts, CatalogProductsAdapter>();
        return services;
    }

    private static string EnsureTrailingSlash(string url) => url.EndsWith('/') ? url : url + "/";
}
