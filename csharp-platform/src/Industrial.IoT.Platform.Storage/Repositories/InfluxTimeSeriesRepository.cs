// Industrial.IoT.Platform.Storage - Simple InfluxDB Repository
// EXACT pattern copied from working ADAM-6051 logger - no over-engineering
// Uses same InfluxDB 2.x patterns as Python implementation

using System.Reactive.Subjects;
using System.Reactive.Linq;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Core;
using InfluxDB.Client.Writes;
using Industrial.IoT.Platform.Core.Interfaces;
using Industrial.IoT.Platform.Core.Models;
using Industrial.IoT.Platform.Storage.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Industrial.IoT.Platform.Storage.Repositories;

/// <summary>
/// Simple InfluxDB time-series repository - EXACT pattern from working ADAM-6051 logger
/// No over-engineering - uses same patterns as existing Python implementation
/// </summary>
public sealed class InfluxTimeSeriesRepository : ITimeSeriesRepository, IHealthCheck, IDisposable
{
    private readonly ILogger<InfluxTimeSeriesRepository> _logger;
    private readonly InfluxTimeSeriesConfiguration _configuration;
    private readonly Subject<StorageMetricsEventArgs> _metricsSubject = new();
    private readonly Subject<StorageFailureEventArgs> _failureSubject = new();
    
    private InfluxDBClient? _client;
    private WriteApi? _writeApi;
    private QueryApi? _queryApi;
    private volatile bool _isConnected;
    private volatile bool _isDisposed;

    /// <summary>
    /// Storage type identifier
    /// </summary>
    public string StorageType => "InfluxDB";

    /// <summary>
    /// Whether the repository is connected and ready for operations
    /// </summary>
    public bool IsConnected => _isConnected && _client != null;

    /// <summary>
    /// Observable stream of storage metrics updates
    /// </summary>
    public IObservable<StorageMetricsEventArgs> MetricsStream => _metricsSubject.AsObservable();

    /// <summary>
    /// Observable stream of storage failure events
    /// </summary>
    public IObservable<StorageFailureEventArgs> FailureStream => _failureSubject.AsObservable();

    /// <summary>
    /// Initialize InfluxDB repository - SAME pattern as ADAM-6051 logger
    /// </summary>
    public InfluxTimeSeriesRepository(
        IOptions<InfluxTimeSeriesConfiguration> configuration,
        ILogger<InfluxTimeSeriesRepository> logger)
    {
        _configuration = configuration?.Value ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _logger.LogInformation("InfluxDB repository initialized for bucket: {Bucket}", _configuration.Bucket);
    }

    /// <summary>
    /// Connect to InfluxDB - EXACT pattern from ADAM-6051 logger
    /// </summary>
    public async Task<bool> ConnectAsync(string? connectionString = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (_isConnected && _client != null)
            return true;

        try
        {
            _logger.LogInformation("Connecting to InfluxDB at {Url} with bucket {Bucket}", 
                _configuration.Url, _configuration.Bucket);
            
            // EXACT same pattern as Python implementation
            var options = InfluxDBClientOptions.Builder.CreateNew()
                .Url(_configuration.Url)
                .AuthenticateToken(_configuration.Token)
                .Org(_configuration.Organization)
                .Bucket(_configuration.Bucket)
                .Build();
            
            _client = InfluxDBClientFactory.Create(options);
            _writeApi = _client.GetWriteApi(_configuration.CreateWriteOptions());
            _queryApi = _client.GetQueryApi();
            
            // Configure write error handling - same pattern as Python
            _writeApi.EventHandler += OnWriteEvent;
            
            // Test connection by checking bucket exists - same as Python
            var bucketsApi = _client.GetBucketsApi();
            var bucket = await bucketsApi.FindBucketByNameAsync(_configuration.Bucket);
            
            if (bucket != null)
            {
                _isConnected = true;
                _logger.LogInformation("Successfully connected to InfluxDB 2.x bucket: {Bucket}", _configuration.Bucket);
                return true;
            }
            else
            {
                _logger.LogError("Bucket '{Bucket}' not found in InfluxDB", _configuration.Bucket);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to InfluxDB");
            return false;
        }
    }

    /// <summary>
    /// Disconnect from InfluxDB
    /// </summary>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_writeApi != null)
        {
            _writeApi.EventHandler -= OnWriteEvent;
            _writeApi?.Dispose();
            _writeApi = null;
        }

        _client?.Dispose();
        _client = null;
        _queryApi = null;
        _isConnected = false;

        _logger.LogInformation("Disconnected from InfluxDB");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Test connectivity to InfluxDB
    /// </summary>
    public async Task<StorageTestResult> TestConnectivityAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;
        
        try
        {
            if (_client == null)
            {
                return new StorageTestResult
                {
                    Success = false,
                    StorageBackend = StorageType,
                    Duration = DateTimeOffset.UtcNow - startTime,
                    ErrorMessage = "Not connected to InfluxDB"
                };
            }

            // Simple ping test - check if we can get bucket info
            var bucketsApi = _client.GetBucketsApi();
            var bucket = await bucketsApi.FindBucketByNameAsync(_configuration.Bucket);
            var duration = DateTimeOffset.UtcNow - startTime;

            return new StorageTestResult
            {
                Success = bucket != null,
                StorageBackend = StorageType,
                Duration = duration,
                Latency = duration,
                ErrorMessage = bucket == null ? "Bucket not found" : null
            };
        }
        catch (Exception ex)
        {
            return new StorageTestResult
            {
                Success = false,
                StorageBackend = StorageType,
                Duration = DateTimeOffset.UtcNow - startTime,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Get health status
    /// </summary>
    public async Task<StorageHealth> GetHealthAsync()
    {
        var testResult = await TestConnectivityAsync(TimeSpan.FromSeconds(5));
        
        return new StorageHealth
        {
            StorageBackend = StorageType,
            IsHealthy = testResult.Success && IsConnected,
            LastChecked = DateTimeOffset.UtcNow,
            IsConnected = IsConnected,
            AverageResponseTime = testResult.Latency?.TotalMilliseconds ?? 0,
            LastError = testResult.ErrorMessage
        };
    }

    /// <summary>
    /// Write time-series data - EXACT pattern from ADAM-6051 logger
    /// </summary>
    public async Task<StorageWriteResult> WriteTimeSeriesAsync(IReadOnlyList<IDataReading> dataPoints, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (!IsConnected || _writeApi == null)
        {
            return new StorageWriteResult
            {
                Success = false,
                StorageBackend = StorageType,
                ErrorMessage = "Not connected to InfluxDB"
            };
        }

        var startTime = DateTime.UtcNow;
        
        try
        {
            // Convert to InfluxDB points - same pattern as Python
            var points = dataPoints.Select(ConvertToInfluxPoint).ToList();
            
            // Write with retry logic - same as Python
            for (int attempt = 0; attempt <= _configuration.MaxRetries; attempt++)
            {
                try
                {
                    _writeApi.WritePoints(points, _configuration.Bucket, _configuration.Organization);
                    
                    _logger.LogDebug("Successfully wrote {Count} data points to InfluxDB 2.x", points.Count);
                    
                    return new StorageWriteResult
                    {
                        Success = true,
                        RecordsWritten = points.Count,
                        StorageBackend = StorageType,
                        Duration = DateTime.UtcNow - startTime
                    };
                }
                catch (Exception ex) when (attempt < _configuration.MaxRetries)
                {
                    _logger.LogWarning("InfluxDB write attempt {Attempt} failed: {Error}", attempt + 1, ex.Message);
                    await Task.Delay(_configuration.RetryIntervalMs, cancellationToken);
                    
                    // Reconnect like Python implementation
                    await ConnectAsync(cancellationToken: cancellationToken);
                }
            }

            // All retries failed
            var error = $"Failed to write to InfluxDB after {_configuration.MaxRetries} retries";
            _logger.LogError(error);
            
            return new StorageWriteResult
            {
                Success = false,
                StorageBackend = StorageType,
                Duration = DateTime.UtcNow - startTime,
                ErrorMessage = error
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing to InfluxDB");
            
            return new StorageWriteResult
            {
                Success = false,
                StorageBackend = StorageType,
                Duration = DateTime.UtcNow - startTime,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Query time-series data - simplified implementation
    /// </summary>
    public async Task<TimeSeriesQueryResult> QueryTimeSeriesAsync(TimeSeriesQuery query, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (!IsConnected || _queryApi == null)
        {
            return new TimeSeriesQueryResult
            {
                Success = false,
                ErrorMessage = "Not connected to InfluxDB"
            };
        }

        var startTime = DateTime.UtcNow;
        
        try
        {
            // Simple Flux query - same pattern as Python
            var flux = $@"
                from(bucket: ""{_configuration.Bucket}"")
                |> range(start: {query.StartTime:yyyy-MM-ddTHH:mm:ssZ}, stop: {query.EndTime:yyyy-MM-ddTHH:mm:ssZ})
                |> filter(fn: (r) => r._measurement == ""{_configuration.DefaultMeasurement}"")";

            if (query.DeviceIds.Any())
            {
                var deviceFilter = string.Join(" or ", query.DeviceIds.Select(id => $@"r.device_id == ""{id}"""));
                flux += $" |> filter(fn: (r) => {deviceFilter})";
            }

            var tables = await _queryApi.QueryAsync(flux, _configuration.Organization, cancellationToken);
            var dataPoints = new List<TimeSeriesDataPoint>();

            foreach (var table in tables)
            {
                foreach (var record in table.Records)
                {
                    dataPoints.Add(new TimeSeriesDataPoint
                    {
                        DeviceId = record.Values.GetValueOrDefault("device_id")?.ToString() ?? "",
                        Timestamp = record.GetTime()?.ToDateTimeOffset() ?? DateTimeOffset.UtcNow,
                        Value = record.GetValue() ?? 0,
                        Quality = DataQuality.Good,
                        Tags = record.Values.Where(kvp => kvp.Key.StartsWith("_") == false)
                                   .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                    });
                }
            }

            return new TimeSeriesQueryResult
            {
                Success = true,
                DataPoints = dataPoints,
                ExecutionTime = DateTime.UtcNow - startTime,
                TotalDataPoints = dataPoints.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying InfluxDB");
            
            return new TimeSeriesQueryResult
            {
                Success = false,
                ExecutionTime = DateTime.UtcNow - startTime,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Get latest values for devices
    /// </summary>
    public async Task<IReadOnlyList<IDataReading>> GetLatestValuesAsync(IReadOnlyList<string> deviceIds, CancellationToken cancellationToken = default)
    {
        // Simple implementation - could be expanded later
        return Array.Empty<IDataReading>();
    }

    /// <summary>
    /// Cleanup old data
    /// </summary>
    public async Task<long> CleanupOldDataAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default)
    {
        // Simple implementation - could be expanded later
        return 0;
    }

    /// <summary>
    /// Health check implementation
    /// </summary>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var health = await GetHealthAsync();
            
            if (health.IsHealthy)
                return HealthCheckResult.Healthy($"InfluxDB connection healthy, response time: {health.AverageResponseTime:F1}ms");
            else
                return HealthCheckResult.Unhealthy($"InfluxDB connection unhealthy: {health.LastError}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("InfluxDB health check failed", ex);
        }
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        try
        {
            DisconnectAsync().Wait(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during InfluxDB dispose");
        }

        _metricsSubject?.Dispose();
        _failureSubject?.Dispose();

        _logger.LogInformation("InfluxDB repository disposed");
    }

    #region Private Methods - EXACT patterns from ADAM-6051 logger

    /// <summary>
    /// Convert data reading to InfluxDB point - SAME pattern as Python
    /// </summary>
    private PointData ConvertToInfluxPoint(IDataReading dataReading)
    {
        // EXACT same pattern as Python Point creation
        var point = PointData
            .Measurement(_configuration.DefaultMeasurement)
            .Tag("device_id", dataReading.DeviceId)
            .Tag("quality", dataReading.Quality.ToString())
            .Timestamp(dataReading.Timestamp, WritePrecision.Ms);

        // Add tags from the reading
        foreach (var tag in dataReading.Tags)
        {
            if (tag.Value != null)
                point = point.Tag(tag.Key, tag.Value.ToString()!);
        }

        // Add fields based on data reading type
        if (dataReading is IScaleDataReading scaleReading)
        {
            point = point.Field("weight_value", scaleReading.WeightValue);
            if (!string.IsNullOrEmpty(scaleReading.Unit))
                point = point.Tag("unit", scaleReading.Unit);
        }
        else
        {
            // Generic field for other reading types
            point = point.Field("value", 1.0); // Default value
        }

        return point;
    }

    /// <summary>
    /// Handle write events - SAME pattern as Python error handling
    /// </summary>
    private void OnWriteEvent(object? sender, EventArgs eventArgs)
    {
        switch (eventArgs)
        {
            case WriteSuccessEvent successEvent:
                _logger.LogDebug("InfluxDB write successful: {LineProtocol}", successEvent.LineProtocol);
                break;
                
            case WriteErrorEvent errorEvent:
                _logger.LogWarning("InfluxDB write error: {Exception}", errorEvent.Exception?.Message);
                _ = PublishFailureAsync("WriteEvent", errorEvent.Exception?.Message ?? "Unknown write error", errorEvent.Exception);
                break;
        }
    }

    /// <summary>
    /// Publish failure event
    /// </summary>
    private async Task PublishFailureAsync(string operationType, string errorMessage, Exception? exception = null, object? failedData = null)
    {
        var args = new StorageFailureEventArgs
        {
            StorageBackend = StorageType,
            OperationType = operationType,
            ErrorMessage = errorMessage,
            Exception = exception,
            FailedData = failedData
        };

        _failureSubject.OnNext(args);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Throw if disposed
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(InfluxTimeSeriesRepository));
    }

    #endregion
}