// Industrial.IoT.Platform.Devices.Adam - ADAM-4571 Scale Provider Refactored Implementation
// Device provider using composition of focused services following SRP

using System.ComponentModel.DataAnnotations;
using Industrial.IoT.Platform.Core.Interfaces;
using Industrial.IoT.Platform.Core.Models;
using Industrial.IoT.Platform.Devices.Adam.Configuration;
using Industrial.IoT.Platform.Devices.Adam.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Industrial.IoT.Platform.Devices.Adam.Providers;

/// <summary>
/// Device provider for ADAM-4571 scale devices using composition of focused services
/// Follows Single Responsibility Principle by delegating to specialized services
/// </summary>
public class Adam4571ProviderRefactored : IDeviceProvider, IHealthCheck
{
    private readonly Adam4571Configuration _config;
    private readonly Adam4571ConnectionManager _connectionManager;
    private readonly Adam4571HealthMonitor _healthMonitor;
    private readonly Adam4571DataAcquisition _dataAcquisition;
    private readonly ILogger<Adam4571ProviderRefactored> _logger;

    /// <summary>
    /// Device type identifier
    /// </summary>
    public string DeviceType => "ADAM-4571";

    /// <summary>
    /// Manufacturer identifier
    /// </summary>
    public string Manufacturer => "Advantech";

    /// <summary>
    /// Unique identifier for the specific device instance
    /// </summary>
    public string DeviceId => _config.DeviceId;

    /// <summary>
    /// Current device configuration
    /// </summary>
    public IDeviceConfiguration Configuration => _config;

    /// <summary>
    /// Check if the device provider is currently running and acquiring data
    /// </summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// Reactive stream of all data readings from this device
    /// </summary>
    public IObservable<IDataReading> DataStream => _dataAcquisition.DataStream;

    /// <summary>
    /// Reactive stream of device health updates
    /// </summary>
    public IObservable<IDeviceHealth> HealthStream => _healthMonitor.HealthStream;

    /// <summary>
    /// Initialize the ADAM-4571 provider with focused service dependencies
    /// </summary>
    /// <param name="config">Device configuration</param>
    /// <param name="connectionManager">Connection management service</param>
    /// <param name="healthMonitor">Health monitoring service</param>
    /// <param name="dataAcquisition">Data acquisition service</param>
    /// <param name="logger">Logger for diagnostic information</param>
    public Adam4571ProviderRefactored(
        Adam4571Configuration config,
        Adam4571ConnectionManager connectionManager,
        Adam4571HealthMonitor healthMonitor,
        Adam4571DataAcquisition dataAcquisition,
        ILogger<Adam4571ProviderRefactored> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        _healthMonitor = healthMonitor ?? throw new ArgumentNullException(nameof(healthMonitor));
        _dataAcquisition = dataAcquisition ?? throw new ArgumentNullException(nameof(dataAcquisition));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ValidateConfiguration();
    }

    /// <summary>
    /// Validate the configuration and throw exceptions for any invalid settings
    /// Follows the exact same pattern as existing validation
    /// </summary>
    private void ValidateConfiguration()
    {
        var validationResults = _config.Validate(new ValidationContext(_config));
        
        if (validationResults.Any())
        {
            var errors = string.Join(Environment.NewLine, validationResults.Select(r => $"- {r.ErrorMessage}"));
            throw new ArgumentException($"Invalid ADAM-4571 configuration for device '{_config.DeviceId}':{Environment.NewLine}{errors}");
        }
    }

    /// <summary>
    /// Establish connection to the device with retry logic and protocol discovery
    /// Delegates to connection manager service
    /// </summary>
    /// <param name="configuration">Device configuration (must be Adam4571Configuration)</param>
    /// <param name="cancellationToken">Cancellation token to abort the connection attempt</param>
    /// <returns>True if connection and protocol discovery were successful</returns>
    public async Task<bool> ConnectAsync(IDeviceConfiguration configuration, CancellationToken cancellationToken = default)
    {
        if (configuration is not Adam4571Configuration adam4571Config)
            throw new ArgumentException($"Configuration must be of type {nameof(Adam4571Configuration)}", nameof(configuration));

        _logger.LogInformation("Connecting to ADAM-4571 device {DeviceId}", _config.DeviceId);

        // Delegate to connection manager
        var connected = await _connectionManager.ConnectAsync(cancellationToken);
        
        if (connected)
        {
            // Update health monitor with connection status
            _healthMonitor.UpdateConnectionStatus(true, _connectionManager.DiscoveredProtocol);
            
            _logger.LogInformation("Successfully connected to ADAM-4571 device {DeviceId}", _config.DeviceId);
        }
        else
        {
            _healthMonitor.UpdateConnectionStatus(false);
            _logger.LogWarning("Failed to connect to ADAM-4571 device {DeviceId}", _config.DeviceId);
        }

        return connected;
    }

    /// <summary>
    /// Start data acquisition from the device
    /// Orchestrates startup of all service components
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to abort the start operation</param>
    /// <returns>Task that completes when data acquisition has started</returns>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
            return;

        if (!_connectionManager.IsConnected)
            throw new InvalidOperationException($"Device {_config.DeviceId} is not connected. Call ConnectAsync first.");

        _logger.LogInformation("Starting ADAM-4571 device provider {DeviceId}", _config.DeviceId);

        // Start services in order
        await _healthMonitor.StartAsync();
        await _dataAcquisition.StartAsync();

        IsRunning = true;

        _logger.LogInformation("Started ADAM-4571 device provider {DeviceId}", _config.DeviceId);
    }

    /// <summary>
    /// Stop data acquisition from the device
    /// Orchestrates shutdown of all service components
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to abort the stop operation</param>
    /// <returns>Task that completes when data acquisition has stopped</returns>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRunning)
            return;

        _logger.LogInformation("Stopping ADAM-4571 device provider {DeviceId}", _config.DeviceId);

        IsRunning = false;

        // Stop services in reverse order
        await _dataAcquisition.StopAsync();
        await _healthMonitor.StopAsync();
        await _connectionManager.DisconnectAsync();

        _logger.LogInformation("Stopped ADAM-4571 device provider {DeviceId}", _config.DeviceId);
    }

    /// <summary>
    /// Get current health status for this device
    /// Delegates to health monitor service
    /// </summary>
    /// <returns>Current health status</returns>
    public async Task<IDeviceHealth> GetHealthAsync()
    {
        return await _healthMonitor.GetHealthAsync();
    }

    /// <summary>
    /// Update device configuration at runtime
    /// </summary>
    /// <param name="configuration">Updated configuration for the device</param>
    /// <param name="cancellationToken">Cancellation token to abort the update</param>
    /// <returns>Task that completes when the configuration has been updated</returns>
    public Task UpdateConfigurationAsync(IDeviceConfiguration configuration, CancellationToken cancellationToken = default)
    {
        if (configuration is not Adam4571Configuration adam4571Config)
            throw new ArgumentException($"Configuration must be of type {nameof(Adam4571Configuration)}", nameof(configuration));

        // For now, configuration updates require restart
        // In a full implementation, we would support dynamic configuration updates
        throw new NotSupportedException("Dynamic configuration updates not yet supported. Restart the device provider with new configuration.");
    }

    /// <summary>
    /// Test connectivity to the device without starting data acquisition
    /// Delegates to connection manager service
    /// </summary>
    /// <param name="configuration">Configuration to test</param>
    /// <param name="cancellationToken">Cancellation token to abort the test</param>
    /// <returns>Task with test result indicating success/failure and details</returns>
    public async Task<DeviceTestResult> TestConnectivityAsync(IDeviceConfiguration configuration, CancellationToken cancellationToken = default)
    {
        if (configuration is not Adam4571Configuration adam4571Config)
        {
            return new DeviceTestResult
            {
                Success = false,
                Duration = TimeSpan.Zero,
                ErrorMessage = $"Configuration must be of type {nameof(Adam4571Configuration)}"
            };
        }

        // Delegate to connection manager
        return await _connectionManager.TestConnectivityAsync(cancellationToken);
    }

    /// <summary>
    /// Health check implementation for monitoring
    /// Delegates to health monitor service
    /// </summary>
    /// <param name="context">Health check context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health check result</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return await _healthMonitor.CheckHealthAsync(context, cancellationToken);
    }

    /// <summary>
    /// Dispose of all resources used by this provider
    /// Ensures proper cleanup of all service components
    /// </summary>
    public void Dispose()
    {
        try
        {
            StopAsync().Wait(TimeSpan.FromSeconds(10));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during disposal of device provider {DeviceId}", _config.DeviceId);
        }

        // Dispose services in reverse order
        _dataAcquisition?.Dispose();
        _healthMonitor?.Dispose();
        _connectionManager?.Dispose();
    }
}