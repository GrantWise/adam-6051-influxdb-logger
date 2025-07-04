// Industrial.Adam.Logger - InfluxDB Writer Implementation
// Implementation for writing ADAM data to InfluxDB

using Industrial.Adam.Logger.Configuration;
using Industrial.Adam.Logger.Interfaces;
using Industrial.Adam.Logger.Models;
using Industrial.Adam.Logger.Utilities;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Core.Exceptions;
using InfluxDB.Client.Writes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Industrial.Adam.Logger.Infrastructure;

/// <summary>
/// InfluxDB writer implementation for ADAM data
/// </summary>
public class InfluxDbWriter : IInfluxDbWriter
{
    private readonly ILogger<InfluxDbWriter> _logger;
    private readonly InfluxDbConfig _config;
    private readonly IRetryPolicyService _retryService;
    private readonly InfluxDBClient _client;
    private readonly WriteApiAsync _writeApi;
    private readonly ConcurrentQueue<AdamDataReading> _writeQueue;
    private readonly Timer _flushTimer;
    private readonly SemaphoreSlim _writeSemaphore;
    private bool _disposed;

    public InfluxDbWriter(
        IOptions<AdamLoggerConfig> config,
        IRetryPolicyService retryService,
        ILogger<InfluxDbWriter> logger)
    {
        _logger = logger;
        _config = config.Value.InfluxDb ?? throw new ArgumentException("InfluxDB configuration is required");
        _retryService = retryService;

        // Validate configuration
        var validationResults = _config.Validate();
        if (validationResults.Any())
        {
            var errors = string.Join(", ", validationResults.Select(r => r.ErrorMessage));
            throw new ArgumentException($"Invalid InfluxDB configuration: {errors}");
        }

        // Initialize InfluxDB client
        var options = new InfluxDBClientOptions.Builder()
            .Url(_config.Url)
            .AuthenticateToken(_config.Token)
            .TimeOut(TimeSpan.FromMilliseconds(_config.TimeoutMs))
            .Build();

        _client = new InfluxDBClient(options);
        _writeApi = _client.GetWriteApiAsync();

        // Initialize buffering
        _writeQueue = new ConcurrentQueue<AdamDataReading>();
        _writeSemaphore = new SemaphoreSlim(1, 1);

        // Start flush timer
        _flushTimer = new Timer(FlushPendingWrites, null, 
            TimeSpan.FromMilliseconds(_config.FlushIntervalMs),
            TimeSpan.FromMilliseconds(_config.FlushIntervalMs));

        _logger.LogInformation("InfluxDB writer initialized for {Url} -> {Bucket}", 
            _config.Url, _config.Bucket);
    }

    /// <inheritdoc/>
    public async Task WriteAsync(AdamDataReading reading, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(InfluxDbWriter));

        try
        {
            var point = CreateDataPoint(reading);
            
            if (_config.EnableDebugLogging)
            {
                _logger.LogDebug("Writing data point: {DeviceId} Ch{Channel} = {Value}", 
                    reading.DeviceId, reading.Channel, reading.ProcessedValue);
            }

            var retryPolicy = _retryService.CreateNetworkRetryPolicy(_config.MaxRetryAttempts, 
                TimeSpan.FromMilliseconds(_config.RetryDelayMs));
            await _retryService.ExecuteAsync(async (ct) =>
            {
                await _writeApi.WritePointAsync(point, _config.Bucket, _config.Organization, ct);
            }, retryPolicy, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write data point to InfluxDB: {DeviceId} Ch{Channel}", 
                reading.DeviceId, reading.Channel);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task WriteBatchAsync(IEnumerable<AdamDataReading> readings, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(InfluxDbWriter));

        var readingsList = readings.ToList();
        if (!readingsList.Any())
            return;

        try
        {
            var points = readingsList.Select(CreateDataPoint).ToList();
            
            if (_config.EnableDebugLogging)
            {
                _logger.LogDebug("Writing batch of {Count} data points to InfluxDB", points.Count);
            }

            var retryPolicy = _retryService.CreateNetworkRetryPolicy(_config.MaxRetryAttempts, 
                TimeSpan.FromMilliseconds(_config.RetryDelayMs));
            await _retryService.ExecuteAsync(async (ct) =>
            {
                await _writeApi.WritePointsAsync(points, _config.Bucket, _config.Organization, ct);
            }, retryPolicy, cancellationToken);

            _logger.LogDebug("Successfully wrote {Count} data points to InfluxDB", points.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write batch of {Count} data points to InfluxDB", readingsList.Count);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return;

        await _writeSemaphore.WaitAsync(cancellationToken);
        try
        {
            var pendingWrites = new List<AdamDataReading>();
            while (_writeQueue.TryDequeue(out var reading))
            {
                pendingWrites.Add(reading);
            }

            if (pendingWrites.Any())
            {
                await WriteBatchAsync(pendingWrites, cancellationToken);
            }
        }
        finally
        {
            _writeSemaphore.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return false;

        try
        {
            var result = await _client.PingAsync();
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "InfluxDB health check failed");
            return false;
        }
    }

    private PointData CreateDataPoint(AdamDataReading reading)
    {
        var point = PointData.Measurement(_config.Measurement)
            .Tag("device_id", reading.DeviceId)
            .Tag("channel", reading.Channel.ToString())
            .Tag("channel_name", $"Ch{reading.Channel}")
            .Tag("quality", reading.Quality.ToString())
            .Field("raw_value", reading.RawValue)
            .Field("processed_value", reading.ProcessedValue ?? reading.RawValue)
            .Timestamp(reading.Timestamp, WritePrecision.Ms);

        // Add rate of change if available
        if (reading.Rate.HasValue)
        {
            point = point.Field("rate_of_change", reading.Rate.Value);
        }

        // Add device tags
        foreach (var tag in reading.Tags)
        {
            point = point.Tag(tag.Key, tag.Value.ToString());
        }

        // Add global tags from configuration
        foreach (var tag in _config.GlobalTags)
        {
            point = point.Tag(tag.Key, tag.Value);
        }

        return point;
    }

    private async void FlushPendingWrites(object? state)
    {
        try
        {
            await FlushAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during automatic flush");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            _flushTimer?.Dispose();
            
            // Flush any remaining writes
            FlushAsync().Wait(TimeSpan.FromSeconds(10));
            
            // WriteApiAsync doesn't implement IDisposable directly
            _client?.Dispose();
            _writeSemaphore?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing InfluxDB writer");
        }

        _logger.LogInformation("InfluxDB writer disposed");
    }
}