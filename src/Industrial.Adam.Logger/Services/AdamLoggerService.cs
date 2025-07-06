// Industrial.Adam.Logger - Main Service Implementation
// Core service that orchestrates data acquisition from multiple ADAM devices

using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Industrial.Adam.Logger.Configuration;
using Industrial.Adam.Logger.Infrastructure;
using Industrial.Adam.Logger.Interfaces;
using Industrial.Adam.Logger.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Industrial.Adam.Logger.Services;

/// <summary>
/// Main service implementation that manages ADAM devices and provides reactive data streams
/// Implements IHostedService for automatic lifecycle management and IHealthCheck for monitoring
/// </summary>
public class AdamLoggerService : IAdamLoggerService, IHostedService, IHealthCheck
{
    private readonly AdamLoggerConfig _config;
    private readonly IDataProcessor _dataProcessor;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AdamLoggerService> _logger;

    private readonly ConcurrentDictionary<string, IModbusDeviceManager> _deviceManagers = new();
    private readonly ConcurrentDictionary<string, AdamDeviceHealth> _deviceHealth = new();
    
    private readonly Subject<AdamDataReading> _dataSubject = new();
    private readonly Subject<AdamDeviceHealth> _healthSubject = new();
    
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly SemaphoreSlim _deviceManagementSemaphore = new(1, 1);
    private Task? _acquisitionTask;
    private Task? _healthCheckTask;

    /// <summary>
    /// Reactive stream of all data readings from all configured devices
    /// </summary>
    public IObservable<AdamDataReading> DataStream => _dataSubject.AsObservable();

    /// <summary>
    /// Reactive stream of device health updates
    /// </summary>
    public IObservable<AdamDeviceHealth> HealthStream => _healthSubject.AsObservable();

    /// <summary>
    /// Whether the service is currently running and acquiring data
    /// </summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// Initialize the ADAM logger service with configuration and dependencies
    /// </summary>
    /// <param name="config">Configuration options for the service</param>
    /// <param name="dataProcessor">Service for processing raw device data</param>
    /// <param name="serviceProvider">Service provider for dependency resolution</param>
    /// <param name="logger">Logger for diagnostic information</param>
    public AdamLoggerService(
        IOptions<AdamLoggerConfig> config,
        IDataProcessor dataProcessor,
        IServiceProvider serviceProvider,
        ILogger<AdamLoggerService> logger)
    {
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _dataProcessor = dataProcessor ?? throw new ArgumentNullException(nameof(dataProcessor));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ValidateConfiguration();
    }

    /// <summary>
    /// Validate the configuration and throw exceptions for any invalid settings
    /// </summary>
    private void ValidateConfiguration()
    {
        var validationResults = _config.ValidateConfiguration();
        
        if (validationResults.Any())
        {
            var errors = string.Join(Environment.NewLine, validationResults.Select(r => $"- {r.ErrorMessage}"));
            throw new ArgumentException($"Invalid ADAM Logger configuration:{Environment.NewLine}{errors}");
        }
    }

    /// <summary>
    /// Start data acquisition from all configured devices
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to abort the start operation</param>
    /// <returns>Task that completes when the service has started</returns>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
            return Task.CompletedTask;

        _logger.LogInformation("Starting ADAM Logger Service with {DeviceCount} devices", _config.Devices.Count);

        // Initialize device managers for each configured device
        foreach (var deviceConfig in _config.Devices)
        {
            IModbusDeviceManager manager;
            
            if (_config.DemoMode)
            {
                var mockLogger = _serviceProvider.GetRequiredService<ILogger<MockModbusDeviceManager>>();
                manager = new MockModbusDeviceManager(deviceConfig, mockLogger);
                _logger.LogInformation("Created mock device manager for {DeviceId} (Demo Mode)", deviceConfig.DeviceId);
            }
            else
            {
                var deviceLogger = _serviceProvider.GetRequiredService<ILogger<ModbusDeviceManager>>();
                manager = new ModbusDeviceManager(deviceConfig, deviceLogger);
                _logger.LogInformation("Created Modbus device manager for {DeviceId}", deviceConfig.DeviceId);
            }
            
            _deviceManagers[deviceConfig.DeviceId] = manager;

            // Initialize health status
            _deviceHealth[deviceConfig.DeviceId] = new AdamDeviceHealth
            {
                DeviceId = deviceConfig.DeviceId,
                Timestamp = DateTimeOffset.UtcNow,
                Status = DeviceStatus.Unknown,
                IsConnected = false
            };
        }

        // Start background tasks for data acquisition and health monitoring
        _acquisitionTask = RunDataAcquisitionAsync(_cancellationTokenSource.Token);
        _healthCheckTask = RunHealthCheckAsync(_cancellationTokenSource.Token);

        IsRunning = true;
        _logger.LogInformation("ADAM Logger Service started successfully");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stop data acquisition and disconnect from all devices
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to abort the stop operation</param>
    /// <returns>Task that completes when the service has stopped</returns>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRunning)
            return;

        _logger.LogInformation("Stopping ADAM Logger Service");

        // Cancel all background operations
        _cancellationTokenSource.Cancel();

        try
        {
            // Wait for background tasks to complete
            if (_acquisitionTask != null)
                await _acquisitionTask;
            if (_healthCheckTask != null)
                await _healthCheckTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelling
        }

        // Dispose all device managers
        foreach (var manager in _deviceManagers.Values)
        {
            manager.Dispose();
        }
        _deviceManagers.Clear();
        _deviceHealth.Clear();

        IsRunning = false;
        _logger.LogInformation("ADAM Logger Service stopped");
    }

    /// <summary>
    /// Background task that continuously acquires data from all devices
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to stop the acquisition loop</param>
    /// <returns>Task that runs until cancellation is requested</returns>
    private async Task RunDataAcquisitionAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting data acquisition loop with {IntervalMs}ms interval", _config.PollIntervalMs);

        while (!cancellationToken.IsCancellationRequested)
        {
            var acquisitionStart = DateTimeOffset.UtcNow;

            try
            {
                // Read data from all devices concurrently
                var tasks = _deviceManagers.Values.Select(async manager =>
                {
                    try
                    {
                        await ReadDeviceDataAsync(manager, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error reading data from device {DeviceId}", manager.DeviceId);
                        UpdateDeviceHealth(manager.DeviceId, false, ex.Message);
                    }
                });

                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in data acquisition loop");
            }

            // Calculate sleep time to maintain the configured polling interval
            var elapsed = DateTimeOffset.UtcNow - acquisitionStart;
            var sleepTime = TimeSpan.FromMilliseconds(_config.PollIntervalMs) - elapsed;

            if (sleepTime > TimeSpan.Zero)
            {
                await Task.Delay(sleepTime, cancellationToken);
            }
            else
            {
                _logger.LogWarning("Data acquisition took {ElapsedMs}ms, longer than interval {IntervalMs}ms", 
                    elapsed.TotalMilliseconds, _config.PollIntervalMs);
            }
        }
    }

    /// <summary>
    /// Read data from all enabled channels on a specific device
    /// </summary>
    /// <param name="manager">Device manager for the target device</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation</param>
    /// <returns>Task that completes when all channels have been read</returns>
    private async Task ReadDeviceDataAsync(IModbusDeviceManager manager, CancellationToken cancellationToken)
    {
        var deviceConfig = manager.Configuration;
        var timestamp = DateTimeOffset.UtcNow;

        foreach (var channel in deviceConfig.Channels.Where(c => c.Enabled))
        {
            try
            {
                var result = await manager.ReadRegistersAsync(
                    channel.StartRegister, 
                    (ushort)channel.RegisterCount, 
                    cancellationToken);

                if (result.Success && result.Data != null)
                {
                    var reading = _dataProcessor.ProcessRawData(
                        deviceConfig.DeviceId, 
                        channel, 
                        result.Data, 
                        timestamp, 
                        result.Duration);

                    _dataSubject.OnNext(reading);
                    UpdateDeviceHealth(deviceConfig.DeviceId, true);
                }
                else
                {
                    _logger.LogWarning("Failed to read channel {Channel} from device {DeviceId}: {Error}", 
                        channel.ChannelNumber, deviceConfig.DeviceId, result.Error?.Message);
                    UpdateDeviceHealth(deviceConfig.DeviceId, false, result.Error?.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading channel {Channel} from device {DeviceId}", 
                    channel.ChannelNumber, deviceConfig.DeviceId);
                UpdateDeviceHealth(deviceConfig.DeviceId, false, ex.Message);
            }
        }
    }

    /// <summary>
    /// Background task that periodically checks device health
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to stop the health check loop</param>
    /// <returns>Task that runs until cancellation is requested</returns>
    private async Task RunHealthCheckAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_config.HealthCheckIntervalMs, cancellationToken);

                foreach (var manager in _deviceManagers.Values)
                {
                    try
                    {
                        var isHealthy = await manager.TestConnectionAsync(cancellationToken);
                        UpdateDeviceHealth(manager.DeviceId, isHealthy);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Health check failed for device {DeviceId}", manager.DeviceId);
                        UpdateDeviceHealth(manager.DeviceId, false, ex.Message);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in health check loop");
            }
        }
    }

    /// <summary>
    /// Update the health status for a specific device
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="success">Whether the operation was successful</param>
    /// <param name="errorMessage">Error message if the operation failed</param>
    private void UpdateDeviceHealth(string deviceId, bool success, string? errorMessage = null)
    {
        if (_deviceHealth.TryGetValue(deviceId, out var currentHealth))
        {
            var newHealth = currentHealth with
            {
                Timestamp = DateTimeOffset.UtcNow,
                IsConnected = success,
                Status = success ? DeviceStatus.Online : DeviceStatus.Error,
                ConsecutiveFailures = success ? 0 : currentHealth.ConsecutiveFailures + 1,
                LastError = errorMessage,
                TotalReads = currentHealth.TotalReads + 1,
                SuccessfulReads = success ? currentHealth.SuccessfulReads + 1 : currentHealth.SuccessfulReads,
                LastSuccessfulRead = success ? DateTimeOffset.UtcNow - currentHealth.Timestamp : currentHealth.LastSuccessfulRead
            };

            _deviceHealth[deviceId] = newHealth;
            _healthSubject.OnNext(newHealth);
        }
    }

    /// <summary>
    /// Get current health status for a specific device
    /// </summary>
    /// <param name="deviceId">Unique identifier of the device</param>
    /// <returns>Current health status or null if device is not found</returns>
    public Task<AdamDeviceHealth?> GetDeviceHealthAsync(string deviceId)
    {
        _deviceHealth.TryGetValue(deviceId, out var health);
        return Task.FromResult(health);
    }

    /// <summary>
    /// Get health status for all configured devices
    /// </summary>
    /// <returns>Collection of device health statuses</returns>
    public Task<IReadOnlyList<AdamDeviceHealth>> GetAllDeviceHealthAsync()
    {
        var healthList = _deviceHealth.Values.ToList();
        return Task.FromResult<IReadOnlyList<AdamDeviceHealth>>(healthList);
    }

    /// <summary>
    /// Add a new device to monitor at runtime
    /// </summary>
    /// <param name="deviceConfig">Configuration for the new device</param>
    /// <returns>Task that completes when the device has been added</returns>
    public async Task AddDeviceAsync(AdamDeviceConfig deviceConfig)
    {
        if (deviceConfig == null)
            throw new ArgumentNullException(nameof(deviceConfig));

        // Validate device configuration
        var validationContext = new ValidationContext(deviceConfig);
        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(deviceConfig, validationContext, validationResults, true))
        {
            var errors = string.Join("; ", validationResults.Select(v => v.ErrorMessage));
            throw new ArgumentException($"Invalid device configuration: {errors}", nameof(deviceConfig));
        }

        await _deviceManagementSemaphore.WaitAsync();
        try
        {
            // Check for duplicate device ID
            if (_deviceManagers.ContainsKey(deviceConfig.DeviceId))
            {
                throw new InvalidOperationException($"Device with ID '{deviceConfig.DeviceId}' already exists");
            }

            _logger.LogInformation("Adding device {DeviceId} at runtime", deviceConfig.DeviceId);

            // Create device manager using the same pattern as startup
            IModbusDeviceManager deviceManager;
            
            if (_config.DemoMode)
            {
                var mockLogger = _serviceProvider.GetRequiredService<ILogger<MockModbusDeviceManager>>();
                deviceManager = new MockModbusDeviceManager(deviceConfig, mockLogger);
            }
            else
            {
                var deviceLogger = _serviceProvider.GetRequiredService<ILogger<ModbusDeviceManager>>();
                deviceManager = new ModbusDeviceManager(deviceConfig, deviceLogger);
            }
            
            // Add to device collections
            if (_deviceManagers.TryAdd(deviceConfig.DeviceId, deviceManager))
            {
                // Initialize device health
                var initialHealth = new AdamDeviceHealth
                {
                    DeviceId = deviceConfig.DeviceId,
                    Status = DeviceStatus.Unknown,
                    IsConnected = false,
                    TotalReads = 0,
                    SuccessfulReads = 0,
                    ConsecutiveFailures = 0,
                    LastSuccessfulRead = null,
                    LastError = null,
                    Timestamp = DateTimeOffset.UtcNow
                };

                _deviceHealth.TryAdd(deviceConfig.DeviceId, initialHealth);

                _logger.LogInformation("Successfully added device {DeviceId}", deviceConfig.DeviceId);

                // Emit health update
                _healthSubject.OnNext(initialHealth);
            }
            else
            {
                // Cleanup if adding failed
                deviceManager.Dispose();
                throw new InvalidOperationException($"Failed to add device '{deviceConfig.DeviceId}' to device collection");
            }
        }
        finally
        {
            _deviceManagementSemaphore.Release();
        }
    }

    /// <summary>
    /// Remove a device from monitoring at runtime
    /// </summary>
    /// <param name="deviceId">Unique identifier of the device to remove</param>
    /// <returns>Task that completes when the device has been removed</returns>
    public async Task RemoveDeviceAsync(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("Device ID cannot be null or empty", nameof(deviceId));

        await _deviceManagementSemaphore.WaitAsync();
        try
        {
            _logger.LogInformation("Removing device {DeviceId} at runtime", deviceId);

            // Remove device manager and dispose it
            if (_deviceManagers.TryRemove(deviceId, out var deviceManager))
            {
                try
                {
                    deviceManager.Dispose();
                    _logger.LogDebug("Disposed device manager for {DeviceId}", deviceId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing device manager for {DeviceId}: {Message}", 
                        deviceId, ex.Message);
                }
            }
            else
            {
                throw new InvalidOperationException($"Device with ID '{deviceId}' not found");
            }

            // Remove device health
            if (_deviceHealth.TryRemove(deviceId, out var finalHealth))
            {
                // Emit final health update indicating removal
                var removalHealth = finalHealth with 
                { 
                    Status = DeviceStatus.Offline,
                    IsConnected = false,
                    Timestamp = DateTimeOffset.UtcNow
                };
                
                _healthSubject.OnNext(removalHealth);
                _logger.LogInformation("Successfully removed device {DeviceId}", deviceId);
            }
            else
            {
                _logger.LogWarning("Device health not found for {DeviceId} during removal", deviceId);
            }
        }
        finally
        {
            _deviceManagementSemaphore.Release();
        }
    }

    /// <summary>
    /// Update device configuration at runtime
    /// </summary>
    /// <param name="deviceConfig">Updated configuration for the device</param>
    /// <returns>Task that completes when the configuration has been updated</returns>
    public async Task UpdateDeviceConfigAsync(AdamDeviceConfig deviceConfig)
    {
        if (deviceConfig == null)
            throw new ArgumentNullException(nameof(deviceConfig));

        // Validate device configuration
        var validationContext = new ValidationContext(deviceConfig);
        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(deviceConfig, validationContext, validationResults, true))
        {
            var errors = string.Join("; ", validationResults.Select(v => v.ErrorMessage));
            throw new ArgumentException($"Invalid device configuration: {errors}", nameof(deviceConfig));
        }

        await _deviceManagementSemaphore.WaitAsync();
        try
        {
            _logger.LogInformation("Updating configuration for device {DeviceId} at runtime", deviceConfig.DeviceId);

            // Check if device exists
            if (!_deviceManagers.ContainsKey(deviceConfig.DeviceId))
            {
                throw new InvalidOperationException($"Device with ID '{deviceConfig.DeviceId}' not found");
            }

            // Preserve current health statistics before removal
            var currentHealth = _deviceHealth.TryGetValue(deviceConfig.DeviceId, out var health) ? health : null;

            // Remove existing device (this will dispose the old manager)
            await RemoveDeviceInternalAsync(deviceConfig.DeviceId, preserveStats: true);

            // Add device with new configuration
            await AddDeviceInternalAsync(deviceConfig, currentHealth);

            _logger.LogInformation("Successfully updated configuration for device {DeviceId}", deviceConfig.DeviceId);
        }
        finally
        {
            _deviceManagementSemaphore.Release();
        }
    }

    /// <summary>
    /// Internal method to remove a device without acquiring the semaphore (for use within other device management operations)
    /// </summary>
    /// <param name="deviceId">Device ID to remove</param>
    /// <param name="preserveStats">Whether to preserve statistics for potential restoration</param>
    private async Task RemoveDeviceInternalAsync(string deviceId, bool preserveStats = false)
    {
        // Remove device manager and dispose it
        if (_deviceManagers.TryRemove(deviceId, out var deviceManager))
        {
            try
            {
                deviceManager.Dispose();
                _logger.LogDebug("Disposed device manager for {DeviceId} (internal)", deviceId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing device manager for {DeviceId}: {Message}", 
                    deviceId, ex.Message);
            }
        }

        // Remove health if not preserving stats, but always emit offline status when removing
        if (_deviceHealth.TryRemove(deviceId, out var finalHealth))
        {
            if (!preserveStats)
            {
                var removalHealth = finalHealth with 
                { 
                    Status = DeviceStatus.Offline,
                    IsConnected = false,
                    Timestamp = DateTimeOffset.UtcNow
                };
                
                _healthSubject.OnNext(removalHealth);
            }
        }

        await Task.CompletedTask; // For consistency with async pattern
    }

    /// <summary>
    /// Internal method to add a device without acquiring the semaphore (for use within other device management operations)
    /// </summary>
    /// <param name="deviceConfig">Device configuration</param>
    /// <param name="previousHealth">Previous health record to preserve statistics</param>
    private async Task AddDeviceInternalAsync(AdamDeviceConfig deviceConfig, AdamDeviceHealth? previousHealth = null)
    {
        // Create device manager using the same pattern as startup
        IModbusDeviceManager deviceManager;
        
        if (_config.DemoMode)
        {
            var mockLogger = _serviceProvider.GetRequiredService<ILogger<MockModbusDeviceManager>>();
            deviceManager = new MockModbusDeviceManager(deviceConfig, mockLogger);
        }
        else
        {
            var deviceLogger = _serviceProvider.GetRequiredService<ILogger<ModbusDeviceManager>>();
            deviceManager = new ModbusDeviceManager(deviceConfig, deviceLogger);
        }
        
        // Add to device collections
        if (_deviceManagers.TryAdd(deviceConfig.DeviceId, deviceManager))
        {
            // Initialize device health, preserving previous statistics if available
            var initialHealth = new AdamDeviceHealth
            {
                DeviceId = deviceConfig.DeviceId,
                Status = DeviceStatus.Unknown,
                IsConnected = false,
                TotalReads = previousHealth?.TotalReads ?? 0,
                SuccessfulReads = previousHealth?.SuccessfulReads ?? 0,
                ConsecutiveFailures = 0, // Reset consecutive failures on config update
                LastSuccessfulRead = previousHealth?.LastSuccessfulRead,
                LastError = null, // Clear previous errors on config update
                Timestamp = DateTimeOffset.UtcNow
            };

            _deviceHealth.TryAdd(deviceConfig.DeviceId, initialHealth);
            _healthSubject.OnNext(initialHealth);
        }
        else
        {
            deviceManager.Dispose();
            throw new InvalidOperationException($"Failed to add device '{deviceConfig.DeviceId}' to device collection");
        }

        await Task.CompletedTask; // For consistency with async pattern
    }

    #region IHostedService Implementation

    /// <summary>
    /// IHostedService implementation for automatic service lifecycle management
    /// </summary>
    Task IHostedService.StartAsync(CancellationToken cancellationToken) => StartAsync(cancellationToken);

    /// <summary>
    /// IHostedService implementation for automatic service lifecycle management
    /// </summary>
    Task IHostedService.StopAsync(CancellationToken cancellationToken) => StopAsync(cancellationToken);

    #endregion

    #region IHealthCheck Implementation

    /// <summary>
    /// Health check implementation for monitoring service status
    /// </summary>
    /// <param name="context">Health check context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health check result indicating service status</returns>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!IsRunning)
            return Task.FromResult(HealthCheckResult.Unhealthy("Service is not running"));

        var healthyDevices = _deviceHealth.Values.Count(h => h.Status == DeviceStatus.Online);
        var totalDevices = _deviceHealth.Count;

        if (healthyDevices == 0)
            return Task.FromResult(HealthCheckResult.Unhealthy($"No devices are online (0/{totalDevices})"));

        if (healthyDevices < totalDevices)
            return Task.FromResult(HealthCheckResult.Degraded($"Some devices are offline ({healthyDevices}/{totalDevices})"));

        return Task.FromResult(HealthCheckResult.Healthy($"All devices are online ({healthyDevices}/{totalDevices})"));
    }

    #endregion

    /// <summary>
    /// Dispose of all resources used by the service
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (!_cancellationTokenSource.IsCancellationRequested)
                _cancellationTokenSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // CancellationTokenSource already disposed
        }
        
        foreach (var manager in _deviceManagers.Values)
        {
            try
            {
                manager.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing device manager: {Message}", ex.Message);
            }
        }

        try
        {
            _dataSubject.Dispose();
            _healthSubject.Dispose();
            _cancellationTokenSource.Dispose();
            _deviceManagementSemaphore.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed
        }
    }
}