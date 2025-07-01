// Industrial.IoT.Platform.Devices.Adam - ADAM-4571 Health Monitoring Service
// Focused service for monitoring device health and diagnostics following SRP

using System.Reactive.Linq;
using System.Reactive.Subjects;
using Industrial.IoT.Platform.Core;
using Industrial.IoT.Platform.Core.Interfaces;
using Industrial.IoT.Platform.Devices.Adam.Configuration;
using Industrial.IoT.Platform.Devices.Adam.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Industrial.IoT.Platform.Devices.Adam.Services;

/// <summary>
/// Monitors health and diagnostics for ADAM-4571 devices
/// Single Responsibility: Health monitoring and status reporting
/// </summary>
public class Adam4571HealthMonitor : IHealthCheck, IDisposable
{
    private readonly Adam4571Configuration _config;
    private readonly ILogger<Adam4571HealthMonitor> _logger;
    private readonly Subject<IDeviceHealth> _healthSubject = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private Task? _healthMonitoringTask;
    private volatile bool _isRunning;
    private volatile bool _isConnected;
    private int _consecutiveFailures;
    private DateTime _lastSuccessfulRead = DateTime.MinValue;
    private int _totalReads;
    private int _successfulReads;
    private double? _communicationLatency;
    private string? _lastError;
    private string? _activeProtocol;

    /// <summary>
    /// Observable stream of device health updates
    /// </summary>
    public IObservable<IDeviceHealth> HealthStream => _healthSubject.AsObservable();

    /// <summary>
    /// Current device status
    /// </summary>
    public ScaleDeviceStatus CurrentStatus => DetermineDeviceStatus();

    /// <summary>
    /// Initialize the health monitor
    /// </summary>
    /// <param name="config">Device configuration</param>
    /// <param name="logger">Logger for diagnostic information</param>
    public Adam4571HealthMonitor(
        Adam4571Configuration config,
        ILogger<Adam4571HealthMonitor> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Start health monitoring
    /// </summary>
    /// <returns>Task representing the start operation</returns>
    public Task StartAsync()
    {
        if (_isRunning)
            return Task.CompletedTask;

        _isRunning = true;
        _logger.LogDebug("Starting health monitoring for device {DeviceId}", _config.DeviceId);

        // Start health monitoring task
        _healthMonitoringTask = Task.Run(async () => await HealthMonitoringLoopAsync(_cancellationTokenSource.Token));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stop health monitoring
    /// </summary>
    /// <returns>Task representing the stop operation</returns>
    public async Task StopAsync()
    {
        if (!_isRunning)
            return;

        _logger.LogDebug("Stopping health monitoring for device {DeviceId}", _config.DeviceId);

        _isRunning = false;
        _cancellationTokenSource.Cancel();

        if (_healthMonitoringTask != null)
        {
            try
            {
                await _healthMonitoringTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Timeout waiting for health monitoring task to complete for device {DeviceId}", _config.DeviceId);
            }
        }
    }

    /// <summary>
    /// Update connection status
    /// </summary>
    /// <param name="isConnected">Whether the device is connected</param>
    /// <param name="activeProtocol">Active protocol identifier</param>
    public void UpdateConnectionStatus(bool isConnected, string? activeProtocol = null)
    {
        _isConnected = isConnected;
        _activeProtocol = activeProtocol;
        
        if (!isConnected)
        {
            _consecutiveFailures++;
        }

        // Trigger immediate health status update
        _ = Task.Run(async () => await PublishHealthStatusAsync());
    }

    /// <summary>
    /// Record a successful data read operation
    /// </summary>
    /// <param name="latency">Communication latency in milliseconds</param>
    public void RecordSuccessfulRead(double latency)
    {
        _totalReads++;
        _successfulReads++;
        _lastSuccessfulRead = DateTime.UtcNow;
        _consecutiveFailures = 0;
        _communicationLatency = latency;
        _lastError = null;
    }

    /// <summary>
    /// Record a failed data read operation
    /// </summary>
    /// <param name="error">Error message</param>
    public void RecordFailedRead(string error)
    {
        _totalReads++;
        _consecutiveFailures++;
        _lastError = error;
    }

    /// <summary>
    /// Get current health status for this device
    /// </summary>
    /// <returns>Current health status</returns>
    public async Task<IDeviceHealth> GetHealthAsync()
    {
        var health = CreateHealthStatus();
        return await Task.FromResult(health);
    }

    /// <summary>
    /// Background loop for health monitoring
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the background loop</returns>
    private async Task HealthMonitoringLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _isRunning)
        {
            try
            {
                await PublishHealthStatusAsync();
                await Task.Delay(Constants.DefaultHealthCheckIntervalMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during health monitoring for device {DeviceId}", _config.DeviceId);
                await Task.Delay(5000, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Publish current health status
    /// </summary>
    /// <returns>Task representing the operation</returns>
    private Task PublishHealthStatusAsync()
    {
        try
        {
            var health = CreateHealthStatus();
            _healthSubject.OnNext(health);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error publishing health status for device {DeviceId}", _config.DeviceId);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Create current health status (DRY principle - single source of health creation)
    /// </summary>
    /// <returns>Current health status</returns>
    private ScaleDeviceHealth CreateHealthStatus()
    {
        return new ScaleDeviceHealth
        {
            DeviceId = _config.DeviceId,
            Timestamp = DateTimeOffset.UtcNow,
            Status = DetermineDeviceStatus(),
            IsConnected = _isConnected,
            LastSuccessfulRead = _lastSuccessfulRead != DateTime.MinValue 
                ? DateTimeOffset.UtcNow - _lastSuccessfulRead 
                : null,
            ConsecutiveFailures = _consecutiveFailures,
            CommunicationLatency = _communicationLatency,
            LastError = _lastError,
            TotalReads = _totalReads,
            SuccessfulReads = _successfulReads,
            ActiveProtocol = _activeProtocol,
            CalibrationStatus = CalibrationStatus.Unknown // Will be determined from device responses in Phase 4
        };
    }

    /// <summary>
    /// Determine current device status based on health metrics
    /// </summary>
    /// <returns>Device status</returns>
    private ScaleDeviceStatus DetermineDeviceStatus()
    {
        if (!_isConnected)
            return ScaleDeviceStatus.Offline;
        
        if (_consecutiveFailures >= _config.MaxRetries)
            return ScaleDeviceStatus.Error;
        
        if (_consecutiveFailures > 0)
            return ScaleDeviceStatus.Warning;
        
        return ScaleDeviceStatus.Online;
    }

    /// <summary>
    /// Health check implementation for monitoring
    /// </summary>
    /// <param name="context">Health check context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health check result</returns>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_isConnected)
                return Task.FromResult(HealthCheckResult.Unhealthy($"Device {_config.DeviceId} is not connected"));

            if (_consecutiveFailures >= _config.MaxRetries)
                return Task.FromResult(HealthCheckResult.Degraded($"Device {_config.DeviceId} has {_consecutiveFailures} consecutive failures"));

            return Task.FromResult(HealthCheckResult.Healthy($"Device {_config.DeviceId} is operating normally"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"Health check failed for device {_config.DeviceId}", ex));
        }
    }

    /// <summary>
    /// Dispose of all resources used by this health monitor
    /// </summary>
    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        
        try
        {
            StopAsync().Wait(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during disposal of health monitor for device {DeviceId}", _config.DeviceId);
        }

        _healthSubject.Dispose();
        _cancellationTokenSource.Dispose();
    }
}