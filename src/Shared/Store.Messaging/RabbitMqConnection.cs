using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Store.Messaging;

/// <summary>
/// The single, long-lived RabbitMQ connection of a service. Publishers and consumers open
/// their own channels on it.
/// </summary>
public interface IRabbitMqConnection
{
    /// <summary><see langword="true"/> when a connection exists and is currently open.</summary>
    bool IsConnected { get; }

    /// <summary>
    /// Returns the open connection, creating it on first use. While the broker is unreachable
    /// (for example while its container is still starting) this keeps retrying with exponential
    /// backoff until it connects or <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Default <see cref="IRabbitMqConnection"/>. Automatic recovery is enabled, so after the first
/// successful connect the client reconnects and restores channels, queues and consumers by itself.
/// </summary>
internal sealed class RabbitMqConnection(
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqConnection> logger) : IRabbitMqConnection, IAsyncDisposable
{
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;

    public bool IsConnected => _connection is { IsOpen: true };

    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            return _connection;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_connection is not null)
            {
                return _connection;
            }

            var settings = options.Value;
            var factory = new ConnectionFactory
            {
                HostName = settings.HostName,
                Port = settings.Port,
                UserName = settings.UserName,
                Password = settings.Password,
                VirtualHost = settings.VirtualHost,
                ClientProvidedName = settings.ClientProvidedName ?? AppDomain.CurrentDomain.FriendlyName,
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
            };

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    _connection = await factory.CreateConnectionAsync(cancellationToken);
                    logger.LogInformation(
                        "Connected to RabbitMQ at {Host}:{Port} as {ClientName} after {Attempts} attempt(s)",
                        settings.HostName, settings.Port, factory.ClientProvidedName, attempt);
                    return _connection;
                }
                catch (Exception ex) when (ex is not OperationCanceledException && !cancellationToken.IsCancellationRequested)
                {
                    var delay = TimeSpan.FromSeconds(Math.Min(MaxRetryDelay.TotalSeconds, Math.Pow(2, Math.Min(attempt, 5))));
                    logger.LogWarning(
                        ex,
                        "RabbitMQ at {Host}:{Port} is not reachable (attempt {Attempt}); retrying in {DelaySeconds}s",
                        settings.HostName, settings.Port, attempt, delay.TotalSeconds);
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            try
            {
                if (_connection.IsOpen)
                {
                    await _connection.CloseAsync();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error while closing the RabbitMQ connection");
            }

            await _connection.DisposeAsync();
            _connection = null;
        }

        _gate.Dispose();
    }
}
