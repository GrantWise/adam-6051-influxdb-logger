// Industrial.Adam.ScaleLogger - InfluxDB Data Storage Manager
// Following proven ADAM-6051 InfluxDB patterns for reliable data storage

using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using Industrial.Adam.ScaleLogger.Configuration;
using Industrial.Adam.ScaleLogger.Models;
using Microsoft.Extensions.Logging;

namespace Industrial.Adam.ScaleLogger.Infrastructure;

/// <summary>
/// Manages InfluxDB data storage following proven ADAM-6051 patterns
/// </summary>
public sealed class InfluxDbManager : IDisposable
{
    private readonly InfluxDbConfig _config;
    private readonly ILogger<InfluxDbManager> _logger;
    private InfluxDBClient? _client;
    private IWriteApi? _writeApi;
    private volatile bool _connected;
    private volatile bool _disposed;

    public InfluxDbManager(InfluxDbConfig config, ILogger<InfluxDbManager> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Connect to InfluxDB
    /// </summary>
    public async Task<bool> ConnectAsync()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(InfluxDbManager));
        if (_connected) return true;

        try
        {
            _client = new InfluxDBClient(_config.Url, _config.Token);
            
            // Test connection by pinging
            var pingResult = await _client.PingAsync();
            if (!pingResult)
            {
                throw new InvalidOperationException("InfluxDB ping failed");
            }

            // Configure write API with batching
            var writeOptions = new WriteOptions
            {
                BatchSize = _config.BatchSize,
                FlushInterval = _config.FlushIntervalMs
            };

            _writeApi = _client.GetWriteApi(writeOptions);
            _connected = true;

            _logger.LogInformation("Connected to InfluxDB at {Url}, bucket: {Bucket}", 
                _config.Url, _config.Bucket);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to InfluxDB at {Url}", _config.Url);
            await DisconnectAsync();
            return false;
        }
    }

    /// <summary>
    /// Disconnect from InfluxDB
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (!_connected) return;

        try
        {
            if (_writeApi != null)
            {
                _writeApi.Flush();
                _writeApi.Dispose();
                _writeApi = null;
            }

            _client?.Dispose();
            _client = null;
            _connected = false;

            _logger.LogInformation("Disconnected from InfluxDB");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during InfluxDB disconnect");
        }
    }

    /// <summary>
    /// Write scale readings to InfluxDB following ADAM-6051 patterns
    /// </summary>
    public async Task<bool> WriteScaleDataAsync(IEnumerable<ScaleDataReading> readings)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(InfluxDbManager));
        if (!_connected) await ConnectAsync();
        if (_writeApi == null) return false;

        try
        {
            var points = readings
                .Where(r => !r.IsError) // Only write good readings
                .Select(CreateDataPoint)
                .ToList();

            if (!points.Any())
            {
                _logger.LogDebug("No valid readings to write to InfluxDB");
                return true;
            }

            _writeApi.WritePoints(points, _config.Bucket, _config.Organization);
            
            _logger.LogDebug("Wrote {Count} scale readings to InfluxDB", points.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write scale data to InfluxDB");
            return false;
        }
    }

    /// <summary>
    /// Write single scale reading to InfluxDB
    /// </summary>
    public async Task<bool> WriteScaleDataAsync(ScaleDataReading reading)
    {
        return await WriteScaleDataAsync(new[] { reading });
    }

    /// <summary>
    /// Flush any pending writes to InfluxDB
    /// </summary>
    public async Task FlushAsync()
    {
        if (_writeApi != null && _connected)
        {
            _writeApi.Flush();
        }
        await Task.CompletedTask;
    }

    /// <summary>
    /// Create InfluxDB data point from scale reading following ADAM-6051 patterns
    /// </summary>
    private PointData CreateDataPoint(ScaleDataReading reading)
    {
        var point = PointData
            .Measurement("scale_weight")
            .Timestamp(reading.Timestamp, WritePrecision.Ms)
            .Tag("device_id", reading.DeviceId)
            .Tag("device_name", reading.DeviceName)
            .Tag("channel", reading.Channel.ToString())
            .Tag("unit", reading.Unit)
            .Tag("quality", reading.Quality.ToString())
            .Field("weight_value", reading.WeightValue)
            .Field("standardized_weight_kg", (double)reading.StandardizedWeightKg)
            .Field("is_stable", reading.IsStable)
            .Field("acquisition_time_ms", reading.AcquisitionTime.TotalMilliseconds);

        // Add optional tags
        if (!string.IsNullOrEmpty(reading.Manufacturer))
            point = point.Tag("manufacturer", reading.Manufacturer);

        if (!string.IsNullOrEmpty(reading.Model))
            point = point.Tag("model", reading.Model);

        if (!string.IsNullOrEmpty(reading.ProtocolTemplate))
            point = point.Tag("protocol", reading.ProtocolTemplate);

        if (!string.IsNullOrEmpty(reading.Status))
            point = point.Tag("status", reading.Status);

        // Add raw value as field for debugging
        if (!string.IsNullOrEmpty(reading.RawValue))
            point = point.Field("raw_value", reading.RawValue);

        // Add metadata fields
        foreach (var metadata in reading.Metadata)
        {
            if (metadata.Value is string strValue)
                point = point.Tag($"meta_{metadata.Key}", strValue);
            else if (metadata.Value is bool boolValue)
                point = point.Field($"meta_{metadata.Key}", boolValue);
            else if (metadata.Value is double doubleValue)
                point = point.Field($"meta_{metadata.Key}", doubleValue);
            else if (metadata.Value is long longValue)
                point = point.Field($"meta_{metadata.Key}", longValue);
        }

        return point;
    }

    /// <summary>
    /// Test InfluxDB connectivity
    /// </summary>
    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            if (!_connected) await ConnectAsync();
            
            var pingResult = await _client!.PingAsync();
            return pingResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "InfluxDB connection test failed");
            return false;
        }
    }

    /// <summary>
    /// Get connection status
    /// </summary>
    public bool IsConnected => _connected;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Task.Run(async () => await DisconnectAsync()).Wait(TimeSpan.FromSeconds(10));
    }
}