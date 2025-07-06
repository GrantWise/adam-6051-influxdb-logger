// Industrial.Adam.Logger - Null InfluxDB Writer
// Null object pattern implementation for when InfluxDB is not configured

using Industrial.Adam.Logger.Interfaces;
using Industrial.Adam.Logger.Models;
using Microsoft.Extensions.Logging;

namespace Industrial.Adam.Logger.Infrastructure;

/// <summary>
/// Null object implementation of IInfluxDbWriter for when InfluxDB is not configured
/// </summary>
public class NullInfluxDbWriter : IInfluxDbWriter
{
    private readonly ILogger<NullInfluxDbWriter> _logger;

    public NullInfluxDbWriter(ILogger<NullInfluxDbWriter> logger)
    {
        _logger = logger;
        _logger.LogInformation("InfluxDB not configured, using null writer (data will not be persisted)");
    }

    /// <inheritdoc/>
    public Task WriteAsync(AdamDataReading reading, CancellationToken cancellationToken = default)
    {
        // No-op: InfluxDB not configured
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task WriteBatchAsync(IEnumerable<AdamDataReading> readings, CancellationToken cancellationToken = default)
    {
        // No-op: InfluxDB not configured
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        // No-op: InfluxDB not configured
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        // Always healthy when not configured
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Nothing to dispose
    }
}