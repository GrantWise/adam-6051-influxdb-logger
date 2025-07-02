// Industrial.Adam.ScaleLogger - Main Scale Logging Service
// Following proven ADAM-6051 service patterns for industrial reliability

using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Industrial.Adam.ScaleLogger.Configuration;
using Industrial.Adam.ScaleLogger.Data.Repositories;
using Industrial.Adam.ScaleLogger.Infrastructure;
using Industrial.Adam.ScaleLogger.Interfaces;
using Industrial.Adam.ScaleLogger.Models;
using Industrial.Adam.ScaleLogger.Utilities;
using Microsoft.Extensions.Logging;

namespace Industrial.Adam.ScaleLogger.Services;

/// <summary>
/// Main scale logging service implementation following proven ADAM-6051 patterns
/// </summary>
public sealed class ScaleLoggerService : IScaleLoggerService
{
    private readonly Adam4571Config _config;
    private readonly ScaleDeviceManager _deviceManager;
    private readonly IWeighingRepository _weighingRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly ISystemEventRepository _systemEventRepository;
    private readonly ProtocolDiscoveryService _discoveryService;
    private readonly RetryPolicyService _retryPolicy;
    private readonly ILogger<ScaleLoggerService> _logger;

    // Reactive streams following ADAM-6051 patterns
    private readonly Subject<ScaleDataReading> _dataSubject = new();
    private readonly Subject<ScaleDeviceHealth> _healthSubject = new();

    // Service state
    private volatile bool _isRunning;
    private volatile bool _disposed;
    private Timer? _pollingTimer;
    private Timer? _healthTimer;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    // Runtime device management
    private readonly ConcurrentDictionary<string, ScaleDeviceConfig> _runtimeDevices = new();

    public IObservable<ScaleDataReading> DataStream => _dataSubject.AsObservable();
    public IObservable<ScaleDeviceHealth> HealthStream => _healthSubject.AsObservable();
    public bool IsRunning => _isRunning;
    public IReadOnlyList<string> ConfiguredDevices => _deviceManager.GetConfiguredDevices();

    public ScaleLoggerService(
        Adam4571Config config,
        ScaleDeviceManager deviceManager,
        IWeighingRepository weighingRepository,
        IDeviceRepository deviceRepository,
        ISystemEventRepository systemEventRepository,
        ProtocolDiscoveryService discoveryService,
        RetryPolicyService retryPolicy,
        ILogger<ScaleLoggerService> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
        _weighingRepository = weighingRepository ?? throw new ArgumentNullException(nameof(weighingRepository));
        _deviceRepository = deviceRepository ?? throw new ArgumentNullException(nameof(deviceRepository));
        _systemEventRepository = systemEventRepository ?? throw new ArgumentNullException(nameof(systemEventRepository));
        _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
        _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Store initial configuration devices
        foreach (var device in _config.Devices)
        {
            _runtimeDevices.TryAdd(device.DeviceId, device);
        }
    }

    public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ScaleLoggerService));
        if (_isRunning) return true;

        try
        {
            _logger.LogInformation("Starting Scale Logger Service with {DeviceCount} devices", _config.Devices.Count);

            // Log startup event
            await _systemEventRepository.LogEventAsync(
                "ServiceStartup",
                $"Scale Logger Service starting with {_config.Devices.Count} devices",
                severity: "Information",
                cancellationToken: cancellationToken);

            // Initialize devices with protocol discovery
            await InitializeDevicesAsync(cancellationToken);

            // Start polling and health check timers
            StartTimers();

            _isRunning = true;
            _logger.LogInformation("Scale Logger Service started successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Scale Logger Service");
            await StopAsync(CancellationToken.None);
            return false;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || !_isRunning) return;

        try
        {
            _logger.LogInformation("Stopping Scale Logger Service");

            // Stop timers
            _pollingTimer?.Dispose();
            _healthTimer?.Dispose();
            _pollingTimer = null;
            _healthTimer = null;

            // Cancel ongoing operations
            _cancellationTokenSource.Cancel();

            // Log shutdown event
            await _systemEventRepository.LogEventAsync(
                "ServiceShutdown",
                "Scale Logger Service stopping",
                severity: "Information",
                cancellationToken: cancellationToken);

            _isRunning = false;
            _logger.LogInformation("Scale Logger Service stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping Scale Logger Service");
        }
    }

    public async Task<bool> AddDeviceAsync(ScaleDeviceConfig deviceConfig, CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ScaleLoggerService));

        try
        {
            // Discover protocol if not specified
            ProtocolTemplate? protocol = null;
            if (string.IsNullOrEmpty(deviceConfig.ProtocolTemplate) && _config.Discovery.Enabled)
            {
                protocol = await _discoveryService.DiscoverProtocolAsync(
                    deviceConfig.Host, deviceConfig.Port, cancellationToken);
                
                if (protocol != null)
                {
                    _logger.LogInformation("Discovered protocol {Protocol} for device {DeviceId}", 
                        protocol.Name, deviceConfig.DeviceId);
                }
            }

            // Add to device manager
            var success = await _deviceManager.AddDeviceAsync(deviceConfig, protocol);
            
            if (success)
            {
                // Register device in repository
                await _deviceRepository.UpsertDeviceAsync(deviceConfig, cancellationToken);
                
                _runtimeDevices.TryAdd(deviceConfig.DeviceId, deviceConfig);
                _logger.LogInformation("Added device {DeviceId} at runtime", deviceConfig.DeviceId);
                
                // Log device addition event
                await _systemEventRepository.LogEventAsync(
                    "DeviceAdded",
                    $"Device {deviceConfig.DeviceId} added at runtime",
                    deviceConfig.DeviceId,
                    "Information",
                    cancellationToken: cancellationToken);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add device {DeviceId}", deviceConfig.DeviceId);
            return false;
        }
    }

    public async Task<bool> RemoveDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ScaleLoggerService));

        try
        {
            var success = await _deviceManager.RemoveDeviceAsync(deviceId);
            
            if (success)
            {
                // Deactivate device in repository
                await _deviceRepository.DeactivateDeviceAsync(deviceId, cancellationToken);
                
                _runtimeDevices.TryRemove(deviceId, out _);
                _logger.LogInformation("Removed device {DeviceId} at runtime", deviceId);
                
                // Log device removal event
                await _systemEventRepository.LogEventAsync(
                    "DeviceRemoved",
                    $"Device {deviceId} removed at runtime",
                    deviceId,
                    "Information",
                    cancellationToken: cancellationToken);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove device {DeviceId}", deviceId);
            return false;
        }
    }

    public async Task<ScaleDeviceHealth?> GetDeviceHealthAsync(string deviceId)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ScaleLoggerService));
        return await _deviceManager.GetDeviceHealthAsync(deviceId);
    }

    public async Task<IReadOnlyList<ScaleDeviceHealth>> GetAllDeviceHealthAsync()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ScaleLoggerService));
        return await _deviceManager.GetAllDeviceHealthAsync();
    }

    public async Task<Interfaces.ConnectivityTestResult> TestDeviceConnectivityAsync(ScaleDeviceConfig deviceConfig, 
        CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ScaleLoggerService));
        return await _deviceManager.TestConnectivityAsync(deviceConfig, cancellationToken);
    }

    public async Task<IReadOnlyList<ScaleDataReading>> ReadDeviceNowAsync(string deviceId, 
        CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ScaleLoggerService));
        
        var readings = await _deviceManager.ReadScaleDataAsync(deviceId);
        
        // Emit readings through reactive stream
        foreach (var reading in readings)
        {
            _dataSubject.OnNext(reading);
        }

        return readings;
    }

    public async Task<ProtocolTemplate?> DiscoverProtocolAsync(string host, int port, 
        CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ScaleLoggerService));
        return await _discoveryService.DiscoverProtocolAsync(host, port, cancellationToken);
    }

    private async Task InitializeDevicesAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing {DeviceCount} scale devices", _config.Devices.Count);

        var initTasks = _config.Devices.Select(async deviceConfig =>
        {
            try
            {
                ProtocolTemplate? protocol = null;
                
                // Discover protocol if needed
                if (string.IsNullOrEmpty(deviceConfig.ProtocolTemplate) && _config.Discovery.Enabled)
                {
                    protocol = await _discoveryService.DiscoverProtocolAsync(
                        deviceConfig.Host, deviceConfig.Port, cancellationToken);
                }

                await _deviceManager.AddDeviceAsync(deviceConfig, protocol);
                
                // Register device in repository
                await _deviceRepository.UpsertDeviceAsync(deviceConfig, cancellationToken);
                
                _logger.LogInformation("Initialized device {DeviceId}", deviceConfig.DeviceId);
                
                // Log device initialization event
                await _systemEventRepository.LogEventAsync(
                    "DeviceInitialized",
                    $"Device {deviceConfig.DeviceId} initialized successfully",
                    deviceConfig.DeviceId,
                    "Information",
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize device {DeviceId}", deviceConfig.DeviceId);
                
                // Log device initialization error
                await _systemEventRepository.LogEventAsync(
                    "DeviceInitializationError",
                    $"Failed to initialize device {deviceConfig.DeviceId}: {ex.Message}",
                    deviceConfig.DeviceId,
                    "Error",
                    details: new { Exception = ex.ToString() },
                    cancellationToken: cancellationToken);
            }
        });

        await Task.WhenAll(initTasks);
    }

    private void StartTimers()
    {
        // Data polling timer following ADAM-6051 patterns
        _pollingTimer = new Timer(
            async _ => await PerformDataPollingAsync(),
            null,
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(_config.PollIntervalMs));

        // Health check timer
        _healthTimer = new Timer(
            async _ => await PerformHealthCheckAsync(),
            null,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMilliseconds(_config.HealthCheckIntervalMs));

        _logger.LogDebug("Started polling timer (interval: {PollInterval}ms) and health timer (interval: {HealthInterval}ms)", 
            _config.PollIntervalMs, _config.HealthCheckIntervalMs);
    }

    private async Task PerformDataPollingAsync()
    {
        if (!_isRunning || _cancellationTokenSource.Token.IsCancellationRequested) return;

        try
        {
            var readings = await _retryPolicy.ExecuteAsync(
                async () => await _deviceManager.ReadAllDevicesAsync(),
                _config.MaxRetryAttempts,
                TimeSpan.FromMilliseconds(_config.RetryDelayMs),
                _cancellationTokenSource.Token);

            if (readings.Any())
            {
                // Save to database using repository
                await _weighingRepository.SaveWeighingsAsync(readings, _cancellationTokenSource.Token);

                // Emit through reactive stream
                foreach (var reading in readings)
                {
                    _dataSubject.OnNext(reading);
                }

                _logger.LogDebug("Polled {ReadingCount} scale readings", readings.Count);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during data polling");
            
            // Log polling error event
            try
            {
                await _systemEventRepository.LogEventAsync(
                    "DataPollingError",
                    $"Error during data polling: {ex.Message}",
                    severity: "Error",
                    details: new { Exception = ex.ToString() },
                    cancellationToken: _cancellationTokenSource.Token);
            }
            catch
            {
                // Ignore errors during error logging to prevent infinite loops
            }
        }
    }

    private async Task PerformHealthCheckAsync()
    {
        if (!_isRunning || _cancellationTokenSource.Token.IsCancellationRequested) return;

        try
        {
            var healthResults = await _deviceManager.GetAllDeviceHealthAsync();
            
            foreach (var health in healthResults)
            {
                _healthSubject.OnNext(health);
            }

            _logger.LogDebug("Performed health check for {DeviceCount} devices", healthResults.Count);
        }
        catch (OperationCanceledException)
        {
            // Normal during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during health check");
            
            // Log health check error event
            try
            {
                await _systemEventRepository.LogEventAsync(
                    "HealthCheckError",
                    $"Error during health check: {ex.Message}",
                    severity: "Error",
                    details: new { Exception = ex.ToString() },
                    cancellationToken: _cancellationTokenSource.Token);
            }
            catch
            {
                // Ignore errors during error logging to prevent infinite loops
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _cancellationTokenSource.Cancel();
            
            Task.Run(async () => await StopAsync(CancellationToken.None))
                .Wait(TimeSpan.FromSeconds(30));

            _pollingTimer?.Dispose();
            _healthTimer?.Dispose();
            _dataSubject.Dispose();
            _healthSubject.Dispose();
            _cancellationTokenSource.Dispose();
            _deviceManager.Dispose();

            _logger.LogInformation("Scale Logger Service disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing Scale Logger Service");
        }
    }
}