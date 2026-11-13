using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Store.Messaging;

/// <summary>DI registration for Store.Messaging.</summary>
public static class MessagingServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="RabbitMqOptions"/> (bound from the <c>RabbitMq</c> section and validated
    /// at startup), the shared <see cref="IRabbitMqConnection"/> and the <see cref="IEventPublisher"/>.
    /// </summary>
    public static IServiceCollection AddStoreMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton<RabbitMqConnection>();
        services.TryAddSingleton<IRabbitMqConnection>(sp => sp.GetRequiredService<RabbitMqConnection>());
        services.TryAddSingleton<RabbitMqEventPublisher>();
        services.TryAddSingleton<IEventPublisher>(sp => sp.GetRequiredService<RabbitMqEventPublisher>());

        return services;
    }

    /// <summary>Registers a consumer derived from <see cref="EventConsumer{TEvent}"/> as a hosted service.</summary>
    public static IServiceCollection AddEventConsumer<TConsumer>(this IServiceCollection services)
        where TConsumer : class, IHostedService
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHostedService<TConsumer>();
        return services;
    }

    /// <summary>Adds a health check named <c>rabbitmq</c> that is unhealthy while the connection is not open.</summary>
    public static IHealthChecksBuilder AddRabbitMqHealthCheck(
        this IHealthChecksBuilder builder,
        HealthStatus failureStatus = HealthStatus.Unhealthy,
        params string[] tags)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddCheck<RabbitMqHealthCheck>("rabbitmq", failureStatus, tags);
    }
}
