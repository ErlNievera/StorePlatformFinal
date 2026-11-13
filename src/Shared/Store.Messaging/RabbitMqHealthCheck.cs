using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Store.Messaging;

/// <summary>Reports whether the service's RabbitMQ connection is open.</summary>
internal sealed class RabbitMqHealthCheck(IRabbitMqConnection connection) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(connection.IsConnected
            ? HealthCheckResult.Healthy("RabbitMQ connection is open.")
            : new HealthCheckResult(context.Registration.FailureStatus, "RabbitMQ connection is not open."));
}
