// Industrial.Adam.ScaleLogger - Scale Device Communication Manager
// Following proven ADAM-6051 communication patterns for TCP scale devices

using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Industrial.Adam.ScaleLogger.Configuration;
using Industrial.Adam.ScaleLogger.Interfaces;
using Industrial.Adam.ScaleLogger.Models;
using Microsoft.Extensions.Logging;

namespace Industrial.Adam.ScaleLogger.Infrastructure;

/// <summary>
/// Manages TCP communication with scale devices
/// Following proven ADAM-6051 device management patterns
/// </summary>
public sealed class ScaleDeviceManager : IDisposable
{
    private readonly ILogger<ScaleDeviceManager> _logger;
    private readonly ConcurrentDictionary<string, ScaleDevice> _devices = new();
    private readonly ConcurrentDictionary<string, ProtocolTemplate> _protocols = new();
    private volatile bool _disposed;

    public ScaleDeviceManager(ILogger<ScaleDeviceManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Add a scale device for monitoring
    /// </summary>
    public async Task<bool> AddDeviceAsync(ScaleDeviceConfig config, ProtocolTemplate? protocol = null)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ScaleDeviceManager));

        try
        {
            var device = new ScaleDevice(config, protocol, _logger);
            if (_devices.TryAdd(config.DeviceId, device))
            {
                await device.ConnectAsync();
                _logger.LogInformation("Added scale device {DeviceId} at {Host}:{Port}", 
                    config.DeviceId, config.Host, config.Port);
                return true;
            }
            else
            {
                device.Dispose();
                _logger.LogWarning("Device {DeviceId} already exists", config.DeviceId);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add device {DeviceId}", config.DeviceId);
            return false;
        }
    }

    /// <summary>
    /// Remove a scale device
    /// </summary>
    public async Task<bool> RemoveDeviceAsync(string deviceId)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ScaleDeviceManager));

        if (_devices.TryRemove(deviceId, out var device))
        {
            await device.DisconnectAsync();
            device.Dispose();
            _logger.LogInformation("Removed scale device {DeviceId}", deviceId);
            return true;
        }

        _logger.LogWarning("Device {DeviceId} not found for removal", deviceId);
        return false;
    }

    /// <summary>
    /// Read weight data from a specific device
    /// </summary>
    public async Task<IReadOnlyList<ScaleDataReading>> ReadScaleDataAsync(string deviceId)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ScaleDeviceManager));

        if (!_devices.TryGetValue(deviceId, out var device))
        {
            _logger.LogWarning("Device {DeviceId} not found", deviceId);
            return Array.Empty<ScaleDataReading>();
        }

        return await device.ReadWeightDataAsync();
    }

    /// <summary>
    /// Read data from all configured devices
    /// </summary>
    public async Task<IReadOnlyList<ScaleDataReading>> ReadAllDevicesAsync()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ScaleDeviceManager));

        var allReadings = new List<ScaleDataReading>();
        var tasks = _devices.Values.Select(device => device.ReadWeightDataAsync());
        var results = await Task.WhenAll(tasks);

        foreach (var readings in results)
        {
            allReadings.AddRange(readings);
        }

        return allReadings;
    }

    /// <summary>
    /// Get health status for a specific device
    /// </summary>
    public async Task<ScaleDeviceHealth?> GetDeviceHealthAsync(string deviceId)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ScaleDeviceManager));

        if (!_devices.TryGetValue(deviceId, out var device))
        {
            return null;
        }

        return await device.GetHealthAsync();
    }

    /// <summary>
    /// Get health status for all devices
    /// </summary>
    public async Task<IReadOnlyList<ScaleDeviceHealth>> GetAllDeviceHealthAsync()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ScaleDeviceManager));

        var healthTasks = _devices.Values.Select(device => device.GetHealthAsync());
        var healthResults = await Task.WhenAll(healthTasks);
        return healthResults.ToList();
    }

    /// <summary>
    /// Test connectivity to a scale device
    /// </summary>
    public async Task<ConnectivityTestResult> TestConnectivityAsync(ScaleDeviceConfig config, 
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;
        var diagnostics = new Dictionary<string, object>();

        try
        {
            using var testDevice = new ScaleDevice(config, null, _logger);
            await testDevice.ConnectAsync();
            
            var readings = await testDevice.ReadWeightDataAsync();
            var duration = DateTimeOffset.UtcNow - startTime;

            diagnostics["connectionTime"] = duration.TotalMilliseconds;
            diagnostics["readingsObtained"] = readings.Count;
            diagnostics["protocol"] = testDevice.Protocol?.Name ?? "unknown";

            return new ConnectivityTestResult
            {
                Success = readings.Any() && readings.All(r => !r.IsError),
                Duration = duration,
                WorkingProtocol = testDevice.Protocol,
                TestReadings = readings,
                Diagnostics = diagnostics
            };
        }
        catch (Exception ex)
        {
            var duration = DateTimeOffset.UtcNow - startTime;
            diagnostics["error"] = ex.Message;
            diagnostics["errorType"] = ex.GetType().Name;

            return new ConnectivityTestResult
            {
                Success = false,
                Duration = duration,
                ErrorMessage = ex.Message,
                Diagnostics = diagnostics
            };
        }
    }

    /// <summary>
    /// Get list of configured device IDs
    /// </summary>
    public IReadOnlyList<string> GetConfiguredDevices()
    {
        return _devices.Keys.ToList();
    }

    /// <summary>
    /// Add a protocol template
    /// </summary>
    public void AddProtocolTemplate(ProtocolTemplate template)
    {
        _protocols.TryAdd(template.Id, template);
        _logger.LogDebug("Added protocol template {TemplateId} for {Manufacturer}", 
            template.Id, template.Manufacturer);
    }

    /// <summary>
    /// Get available protocol templates
    /// </summary>
    public IReadOnlyList<ProtocolTemplate> GetProtocolTemplates()
    {
        return _protocols.Values.ToList();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        var disposeTasks = _devices.Values.Select(async device =>
        {
            try
            {
                await device.DisconnectAsync();
                device.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing device {DeviceId}", device.Config.DeviceId);
            }
        });

        Task.WaitAll(disposeTasks.ToArray(), TimeSpan.FromSeconds(10));
        _devices.Clear();
        _protocols.Clear();
    }
}

/// <summary>
/// Individual scale device communication handler
/// </summary>
internal sealed class ScaleDevice : IDisposable
{
    private readonly ILogger _logger;
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private volatile bool _connected;
    private volatile bool _disposed;
    
    // Health tracking
    private int _totalReads;
    private int _successfulReads;
    private int _consecutiveFailures;
    private DateTimeOffset? _lastSuccessfulRead;
    private string? _lastError;
    private readonly List<double> _latencyHistory = new();

    public ScaleDeviceConfig Config { get; }
    public ProtocolTemplate? Protocol { get; private set; }

    public ScaleDevice(ScaleDeviceConfig config, ProtocolTemplate? protocol, ILogger logger)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
        Protocol = protocol;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ConnectAsync()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ScaleDevice));
        if (_connected) return;

        try
        {
            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(Config.Host, Config.Port);
            _stream = _tcpClient.GetStream();
            _connected = true;

            _logger.LogInformation("Connected to scale {DeviceId} at {Host}:{Port}", 
                Config.DeviceId, Config.Host, Config.Port);
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            _logger.LogError(ex, "Failed to connect to scale {DeviceId}", Config.DeviceId);
            await DisconnectAsync();
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        if (!_connected) return;

        try
        {
            _stream?.Close();
            _tcpClient?.Close();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during disconnect for device {DeviceId}", Config.DeviceId);
        }
        finally
        {
            _connected = false;
            _stream?.Dispose();
            _tcpClient?.Dispose();
            _stream = null;
            _tcpClient = null;
        }
    }

    public async Task<IReadOnlyList<ScaleDataReading>> ReadWeightDataAsync()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ScaleDevice));
        if (!_connected) await ConnectAsync();

        var startTime = DateTimeOffset.UtcNow;
        var readings = new List<ScaleDataReading>();
        
        Interlocked.Increment(ref _totalReads);

        try
        {
            foreach (var channel in Config.Channels)
            {
                var reading = await ReadChannelAsync(channel, startTime);
                readings.Add(reading);
            }

            if (readings.Any(r => !r.IsError))
            {
                Interlocked.Increment(ref _successfulReads);
                Interlocked.Exchange(ref _consecutiveFailures, 0);
                _lastSuccessfulRead = DateTimeOffset.UtcNow;
                
                var latency = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
                lock (_latencyHistory)
                {
                    _latencyHistory.Add(latency);
                    if (_latencyHistory.Count > 100) _latencyHistory.RemoveAt(0);
                }
            }
            else
            {
                Interlocked.Increment(ref _consecutiveFailures);
            }

            return readings;
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _consecutiveFailures);
            _lastError = ex.Message;
            _logger.LogError(ex, "Failed to read from scale {DeviceId}", Config.DeviceId);

            // Return error reading
            return new[]
            {
                new ScaleDataReading
                {
                    DeviceId = Config.DeviceId,
                    DeviceName = Config.Name,
                    Channel = Config.Channels.FirstOrDefault(),
                    Timestamp = startTime,
                    WeightValue = 0,
                    RawValue = string.Empty,
                    Unit = "kg",
                    StandardizedWeightKg = 0,
                    IsError = true,
                    Quality = DataQuality.DeviceFailure,
                    ErrorMessage = ex.Message,
                    AcquisitionTime = DateTimeOffset.UtcNow - startTime
                }
            };
        }
    }

    private async Task<ScaleDataReading> ReadChannelAsync(int channel, DateTimeOffset timestamp)
    {
        // Simple weight reading - will be enhanced with protocol templates
        if (_stream == null) throw new InvalidOperationException("Device not connected");

        try
        {
            // Send basic weight request (this will be protocol-specific later)
            var command = Encoding.ASCII.GetBytes("W\r\n");
            await _stream.WriteAsync(command, 0, command.Length);

            // Read response
            var buffer = new byte[1024];
            var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
            var response = Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim();

            // Parse weight (basic implementation)
            var weight = ParseWeight(response);

            return new ScaleDataReading
            {
                DeviceId = Config.DeviceId,
                DeviceName = Config.Name,
                Channel = channel,
                Timestamp = timestamp,
                WeightValue = weight,
                RawValue = response,
                Unit = "kg",
                StandardizedWeightKg = (decimal)weight,
                IsStable = true,
                Quality = DataQuality.Good,
                AcquisitionTime = DateTimeOffset.UtcNow - timestamp,
                Manufacturer = Config.Manufacturer,
                Model = Config.Model,
                ProtocolTemplate = Protocol?.Name
            };
        }
        catch (Exception ex)
        {
            return new ScaleDataReading
            {
                DeviceId = Config.DeviceId,
                DeviceName = Config.Name,
                Channel = channel,
                Timestamp = timestamp,
                WeightValue = 0,
                RawValue = string.Empty,
                Unit = "kg",
                StandardizedWeightKg = 0,
                IsError = true,
                Quality = DataQuality.DeviceFailure,
                ErrorMessage = ex.Message,
                AcquisitionTime = DateTimeOffset.UtcNow - timestamp
            };
        }
    }

    private static double ParseWeight(string response)
    {
        // Basic weight parsing - will use protocol templates later
        var match = Regex.Match(response, @"[-+]?\d*\.?\d+");
        return match.Success ? double.Parse(match.Value) : 0.0;
    }

    public async Task<ScaleDeviceHealth> GetHealthAsync()
    {
        var latency = 0.0;
        lock (_latencyHistory)
        {
            latency = _latencyHistory.Any() ? _latencyHistory.Average() : 0.0;
        }

        var status = DetermineDeviceStatus();
        var diagnostics = new Dictionary<string, object>
        {
            ["protocol"] = Protocol?.Name ?? "unknown",
            ["manufacturer"] = Config.Manufacturer ?? "unknown",
            ["model"] = Config.Model ?? "unknown",
            ["channels"] = Config.Channels.Count,
            ["averageLatency"] = latency
        };

        return new ScaleDeviceHealth
        {
            DeviceId = Config.DeviceId,
            DeviceName = Config.Name,
            Timestamp = DateTimeOffset.UtcNow,
            Status = status,
            IsConnected = _connected,
            LastSuccessfulRead = _lastSuccessfulRead.HasValue ? 
                DateTimeOffset.UtcNow - _lastSuccessfulRead.Value : null,
            ConsecutiveFailures = _consecutiveFailures,
            CommunicationLatency = latency,
            LastError = _lastError,
            TotalReads = _totalReads,
            SuccessfulReads = _successfulReads,
            ProtocolTemplate = Protocol?.Name,
            Diagnostics = diagnostics
        };
    }

    private DeviceStatus DetermineDeviceStatus()
    {
        if (!_connected) return DeviceStatus.Offline;
        if (_consecutiveFailures > 5) return DeviceStatus.Error;
        if (_consecutiveFailures > 0) return DeviceStatus.Warning;
        return DeviceStatus.Online;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Task.Run(async () => await DisconnectAsync()).Wait(TimeSpan.FromSeconds(5));
    }
}