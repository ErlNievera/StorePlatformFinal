namespace Catalog.Api.Chaos;

/// <summary>Current fault-injection settings, shared by every request (singleton).</summary>
public sealed class ChaosState
{
    private readonly Lock _gate = new();
    private double _failureRate;
    private int _delayMs;

    /// <summary>Share of product requests answered with 503.</summary>
    public double FailureRate
    {
        get { lock (_gate) { return _failureRate; } }
    }

    /// <summary>Latency added to every product request.</summary>
    public int DelayMs
    {
        get { lock (_gate) { return _delayMs; } }
    }

    /// <summary>Replaces both settings atomically.</summary>
    public void Set(double failureRate, int delayMs)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(failureRate, 0d);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(failureRate, 1d);
        ArgumentOutOfRangeException.ThrowIfNegative(delayMs);

        lock (_gate)
        {
            _failureRate = failureRate;
            _delayMs = delayMs;
        }
    }

    /// <summary>Reads both settings atomically.</summary>
    public (double FailureRate, int DelayMs) Snapshot()
    {
        lock (_gate)
        {
            return (_failureRate, _delayMs);
        }
    }
}
