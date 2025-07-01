// Industrial.IoT.Platform.Devices.Adam - ADAM-4571 Scale Provider Implementation
// Device provider implementation for ADAM-4571 scale devices following existing ADAM logger patterns

using System.ComponentModel.DataAnnotations;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Industrial.IoT.Platform.Core;
using Industrial.IoT.Platform.Core.Interfaces;
using Industrial.IoT.Platform.Core.Models;
using Industrial.IoT.Platform.Devices.Adam.Configuration;
using Industrial.IoT.Platform.Devices.Adam.Models;
using Industrial.IoT.Platform.Devices.Adam.Transport;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Industrial.IoT.Platform.Devices.Adam.Providers;

/// <summary>
/// Device provider for ADAM-4571 scale devices with automatic protocol discovery and health monitoring
/// Follows the exact same patterns as the existing AdamLoggerService implementation
/// </summary>
public class Adam4571Provider : IDeviceProvider, IHealthCheck
{
    private readonly Adam4571Configuration _config;
    private readonly IProtocolDiscovery _protocolDiscovery;
    private readonly ILogger<Adam4571Provider> _logger;
    private readonly ILoggerFactory _loggerFactory;

    private ITcpRawProvider? _tcpProvider;
    private string? _discoveredProtocol;
    private readonly object _protocolLock = new();

    private readonly Subject<IDataReading> _dataSubject = new();
    private readonly Subject<IDeviceHealth> _healthSubject = new();
    
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly SemaphoreSlim _operationSemaphore = new(1, 1);
    private Task? _dataAcquisitionTask;
    private Task? _healthMonitoringTask;

    private volatile bool _isRunning;
    private volatile bool _isConnected;
    private int _consecutiveFailures;
    private DateTime _lastSuccessfulRead = DateTime.MinValue;
    private readonly List<double> _stabilityBuffer = new();

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
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Reactive stream of all data readings from this device
    /// </summary>
    public IObservable<IDataReading> DataStream => _dataSubject.AsObservable();

    /// <summary>
    /// Reactive stream of device health updates
    /// </summary>
    public IObservable<IDeviceHealth> HealthStream => _healthSubject.AsObservable();

    /// <summary>
    /// Initialize the ADAM-4571 provider with configuration and dependencies
    /// </summary>
    /// <param name="config">Device configuration</param>
    /// <param name="protocolDiscovery">Protocol discovery service</param>
    /// <param name="logger">Logger for diagnostic information</param>
    /// <param name="loggerFactory">Logger factory for creating additional loggers</param>
    public Adam4571Provider(
        Adam4571Configuration config,
        IProtocolDiscovery protocolDiscovery,
        ILogger<Adam4571Provider> logger,
        ILoggerFactory loggerFactory)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _protocolDiscovery = protocolDiscovery ?? throw new ArgumentNullException(nameof(protocolDiscovery));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

        ValidateConfiguration();
    }

    /// <summary>
    /// Validate the configuration and throw exceptions for any invalid settings
    /// Follows the exact same pattern as AdamLoggerService.ValidateConfiguration
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
    /// </summary>
    /// <param name="configuration">Device configuration (must be Adam4571Configuration)</param>
    /// <param name="cancellationToken">Cancellation token to abort the connection attempt</param>
    /// <returns>True if connection and protocol discovery were successful</returns>
    public async Task<bool> ConnectAsync(IDeviceConfiguration configuration, CancellationToken cancellationToken = default)
    {
        if (configuration is not Adam4571Configuration adam4571Config)
            throw new ArgumentException($"Configuration must be of type {nameof(Adam4571Configuration)}", nameof(configuration));

        await _operationSemaphore.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation("Connecting to ADAM-4571 device {DeviceId} at {IpAddress}:{Port}", 
                _config.DeviceId, _config.IpAddress, _config.Port);

            // Create TCP provider
            var endpoint = _config.CreateTcpEndpoint();
            var tcpLogger = _loggerFactory.CreateLogger<TcpRawProvider>();
            _tcpProvider = new TcpRawProvider(endpoint, tcpLogger);

            // Establish TCP connection
            if (!await _tcpProvider.ConnectAsync(cancellationToken))
            {
                _logger.LogWarning("Failed to establish TCP connection to ADAM-4571 device {DeviceId}", _config.DeviceId);
                return false;
            }

            // Perform protocol discovery if enabled
            if (_config.EnableProtocolDiscovery && string.IsNullOrWhiteSpace(_config.ForceProtocolTemplate))
            {
                _logger.LogInformation("Starting protocol discovery for ADAM-4571 device {DeviceId}", _config.DeviceId);
                
                if (!await PerformProtocolDiscoveryAsync(cancellationToken))
                {
                    _logger.LogWarning("Protocol discovery failed for ADAM-4571 device {DeviceId}", _config.DeviceId);
                    await _tcpProvider.DisconnectAsync();
                    return false;
                }
            }
            else if (!string.IsNullOrWhiteSpace(_config.ForceProtocolTemplate))
            {
                lock (_protocolLock)
                {
                    _discoveredProtocol = _config.ForceProtocolTemplate;
                }
                _logger.LogInformation("Using forced protocol template '{Protocol}' for ADAM-4571 device {DeviceId}", 
                    _discoveredProtocol, _config.DeviceId);
            }

            _isConnected = true;
            _consecutiveFailures = 0;

            _logger.LogInformation("Successfully connected to ADAM-4571 device {DeviceId} using protocol '{Protocol}'", 
                _config.DeviceId, _discoveredProtocol ?? "Unknown");

            // Publish initial health status
            await PublishHealthStatusAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to ADAM-4571 device {DeviceId}", _config.DeviceId);
            _isConnected = false;
            await PublishHealthStatusAsync();
            return false;
        }
        finally
        {
            _operationSemaphore.Release();
        }
    }

    /// <summary>
    /// Start data acquisition from the device
    /// Follows the exact same pattern as AdamLoggerService.StartAsync
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to abort the start operation</param>
    /// <returns>Task that completes when data acquisition has started</returns>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
            return Task.CompletedTask;

        if (!_isConnected)
            throw new InvalidOperationException($"Device {_config.DeviceId} is not connected. Call ConnectAsync first.");

        _isRunning = true;

        _logger.LogInformation("Starting data acquisition for ADAM-4571 device {DeviceId}", _config.DeviceId);

        // Start data acquisition task
        _dataAcquisitionTask = Task.Run(async () => await DataAcquisitionLoopAsync(_cancellationTokenSource.Token), cancellationToken);

        // Start health monitoring task
        _healthMonitoringTask = Task.Run(async () => await HealthMonitoringLoopAsync(_cancellationTokenSource.Token), cancellationToken);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stop data acquisition from the device
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to abort the stop operation</param>
    /// <returns>Task that completes when data acquisition has stopped</returns>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
            return;

        _logger.LogInformation("Stopping data acquisition for ADAM-4571 device {DeviceId}", _config.DeviceId);

        _isRunning = false;
        _cancellationTokenSource.Cancel();

        // Wait for background tasks to complete
        var tasks = new List<Task>();
        if (_dataAcquisitionTask != null) tasks.Add(_dataAcquisitionTask);
        if (_healthMonitoringTask != null) tasks.Add(_healthMonitoringTask);

        if (tasks.Any())
        {
            try
            {
                await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Timeout waiting for background tasks to complete for device {DeviceId}", _config.DeviceId);
            }
        }

        // Disconnect from device
        if (_tcpProvider != null)
        {
            await _tcpProvider.DisconnectAsync();
            _tcpProvider.Dispose();
            _tcpProvider = null;
        }

        _isConnected = false;
        await PublishHealthStatusAsync();

        _logger.LogInformation("Stopped data acquisition for ADAM-4571 device {DeviceId}", _config.DeviceId);
    }

    /// <summary>
    /// Get current health status for this device
    /// </summary>
    /// <returns>Current health status</returns>
    public async Task<IDeviceHealth> GetHealthAsync()
    {
        var health = new ScaleDeviceHealth
        {
            DeviceId = _config.DeviceId,
            Timestamp = DateTimeOffset.UtcNow,
            Status = DetermineDeviceStatus(),
            IsConnected = _isConnected,
            LastSuccessfulRead = _lastSuccessfulRead != DateTime.MinValue 
                ? DateTimeOffset.UtcNow - _lastSuccessfulRead 
                : null,
            ConsecutiveFailures = _consecutiveFailures,
            ActiveProtocol = _discoveredProtocol,
            CalibrationStatus = CalibrationStatus.Unknown,
            TotalReads = 0, // Will be tracked in full implementation
            SuccessfulReads = 0 // Will be tracked in full implementation
        };

        return await Task.FromResult(health);
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

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            // Create a temporary TCP provider for testing
            var endpoint = adam4571Config.CreateTcpEndpoint();
            var tcpLogger = _loggerFactory.CreateLogger<TcpRawProvider>();
            using var testProvider = new TcpRawProvider(endpoint, tcpLogger);

            // Test connection
            var connected = await testProvider.ConnectAsync(cancellationToken);
            if (!connected)
            {
                return new DeviceTestResult
                {
                    Success = false,
                    Duration = stopwatch.Elapsed,
                    ErrorMessage = "Failed to establish TCP connection"
                };
            }

            // Test communication
            var communicationTest = await testProvider.TestConnectionAsync(cancellationToken);
            
            return new DeviceTestResult
            {
                Success = communicationTest,
                Duration = stopwatch.Elapsed,
                ErrorMessage = communicationTest ? null : "Device did not respond to test communication",
                Diagnostics = new Dictionary<string, object>
                {
                    ["TcpConnectionSuccessful"] = connected,
                    ["CommunicationTestSuccessful"] = communicationTest,
                    ["Endpoint"] = $"{adam4571Config.IpAddress}:{adam4571Config.Port}"
                }
            };
        }
        catch (Exception ex)
        {
            return new DeviceTestResult
            {
                Success = false,
                Duration = stopwatch.Elapsed,
                ErrorMessage = ex.Message,
                Diagnostics = new Dictionary<string, object>
                {
                    ["ExceptionType"] = ex.GetType().Name,
                    ["StackTrace"] = ex.StackTrace ?? string.Empty
                }
            };
        }
    }

    /// <summary>
    /// Perform protocol discovery to identify the scale protocol
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if protocol was successfully discovered</returns>
    private async Task<bool> PerformProtocolDiscoveryAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_tcpProvider == null)
                return false;

            // Simulate discovery process duration for industrial-grade timing behavior
            await Task.Delay(100, cancellationToken);

            // Protocol discovery will be implemented in Phase 4
            // For now, use a placeholder protocol
            lock (_protocolLock)
            {
                _discoveredProtocol = "Generic"; // Will be replaced with actual discovered protocol in Phase 4
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Protocol discovery failed for device {DeviceId}", _config.DeviceId);
            return false;
        }
    }

    /// <summary>
    /// Background loop for continuous data acquisition
    /// Follows the same pattern as AdamLoggerService data acquisition
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the background loop</returns>
    private async Task DataAcquisitionLoopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting data acquisition loop for device {DeviceId}", _config.DeviceId);

        while (!cancellationToken.IsCancellationRequested && _isRunning)
        {
            try
            {
                // Read scale data (implementation will be expanded in Phase 4)
                var reading = await ReadScaleDataAsync(cancellationToken);
                if (reading != null)
                {
                    _dataSubject.OnNext(reading);
                    _lastSuccessfulRead = DateTime.UtcNow;
                    _consecutiveFailures = 0;

                    // Update stability buffer for stability detection
                    UpdateStabilityBuffer(reading.ProcessedWeight ?? reading.RawWeight);
                }
                else
                {
                    _consecutiveFailures++;
                }

                // Wait for next poll interval
                await Task.Delay(_config.PollingIntervalMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during data acquisition for device {DeviceId}", _config.DeviceId);
                _consecutiveFailures++;
                
                // Attempt to reconnect if too many failures
                if (_consecutiveFailures >= _config.MaxRetries)
                {
                    _logger.LogWarning("Too many consecutive failures for device {DeviceId}, attempting reconnection", _config.DeviceId);
                    _isConnected = false;
                    
                    // Try to reconnect
                    if (_tcpProvider != null && await _tcpProvider.ConnectAsync(cancellationToken))
                    {
                        _isConnected = true;
                        _consecutiveFailures = 0;
                        _logger.LogInformation("Reconnected to device {DeviceId}", _config.DeviceId);
                    }
                }

                await Task.Delay(_config.RetryDelayMs, cancellationToken);
            }
        }

        _logger.LogDebug("Data acquisition loop stopped for device {DeviceId}", _config.DeviceId);
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
    /// Read scale data from the device (placeholder implementation)
    /// Will be fully implemented in Phase 4 with protocol-specific parsing
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Scale data reading or null if failed</returns>
    private async Task<ScaleDataReading?> ReadScaleDataAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_tcpProvider == null || !_isConnected)
                return null;

            // Placeholder implementation - will be replaced with protocol-specific commands
            var testCommand = System.Text.Encoding.ASCII.GetBytes("?\r");
            var result = await _tcpProvider.SendAndReceiveAsync(testCommand, _config.TimeoutMs, cancellationToken);

            if (result.Success && result.Data?.Length > 0)
            {
                // Placeholder parsing - will be replaced with protocol-specific parsing
                var response = System.Text.Encoding.ASCII.GetString(result.Data);
                var weight = ExtractWeightFromResponse(response);

                return new ScaleDataReading
                {
                    DeviceId = _config.DeviceId,
                    Channel = 0,
                    RawWeight = weight,
                    ProcessedWeight = weight,
                    Timestamp = DateTimeOffset.UtcNow,
                    Stability = DetermineStability(weight),
                    Quality = ScaleQuality.Good,
                    Unit = _config.DefaultUnit,
                    AcquisitionTime = result.Duration,
                    Tags = new Dictionary<string, object>(_config.Tags)
                    {
                        ["Protocol"] = _discoveredProtocol ?? "Unknown"
                    }
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read scale data from device {DeviceId}", _config.DeviceId);
            return null;
        }
    }

    /// <summary>
    /// Extract weight value from device response (placeholder implementation)
    /// </summary>
    /// <param name="response">Raw response from device</param>
    /// <returns>Extracted weight value</returns>
    private double ExtractWeightFromResponse(string response)
    {
        // Placeholder implementation - will be replaced with protocol-specific parsing
        if (double.TryParse(response.Trim(), out var weight))
            return weight;
        
        return 0.0;
    }

    /// <summary>
    /// Update stability buffer and determine current stability
    /// </summary>
    /// <param name="weight">Current weight value</param>
    private void UpdateStabilityBuffer(double weight)
    {
        _stabilityBuffer.Add(weight);
        
        // Keep only recent readings for stability analysis
        while (_stabilityBuffer.Count > Constants.DefaultStabilityWindow)
            _stabilityBuffer.RemoveAt(0);
    }

    /// <summary>
    /// Determine stability based on recent weight readings
    /// </summary>
    /// <param name="currentWeight">Current weight reading</param>
    /// <returns>Stability assessment</returns>
    private ScaleStability DetermineStability(double currentWeight)
    {
        if (_stabilityBuffer.Count < 3)
            return ScaleStability.Unknown;

        var variance = CalculateVariance(_stabilityBuffer);
        
        return variance <= _config.StabilityTolerance 
            ? ScaleStability.Stable 
            : ScaleStability.Unstable;
    }

    /// <summary>
    /// Calculate variance of weight readings
    /// </summary>
    /// <param name="values">Weight values</param>
    /// <returns>Variance</returns>
    private static double CalculateVariance(IReadOnlyList<double> values)
    {
        if (values.Count < 2) return 0;

        var mean = values.Average();
        var variance = values.Sum(x => Math.Pow(x - mean, 2)) / (values.Count - 1);
        return Math.Sqrt(variance);
    }

    /// <summary>
    /// Publish current health status
    /// </summary>
    /// <returns>Task representing the operation</returns>
    private async Task PublishHealthStatusAsync()
    {
        try
        {
            var health = new ScaleDeviceHealth
            {
                DeviceId = _config.DeviceId,
                Timestamp = DateTimeOffset.UtcNow,
                Status = DetermineDeviceStatus(),
                IsConnected = _isConnected,
                LastSuccessfulRead = _lastSuccessfulRead != DateTime.MinValue 
                    ? DateTimeOffset.UtcNow - _lastSuccessfulRead 
                    : null,
                ConsecutiveFailures = _consecutiveFailures,
                ActiveProtocol = _discoveredProtocol,
                CalibrationStatus = CalibrationStatus.Unknown // Will be determined from device responses
            };

            _healthSubject.OnNext(health);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error publishing health status for device {DeviceId}", _config.DeviceId);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Determine current device status
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
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_isConnected)
                return HealthCheckResult.Unhealthy($"Device {_config.DeviceId} is not connected");

            if (_consecutiveFailures >= _config.MaxRetries)
                return HealthCheckResult.Degraded($"Device {_config.DeviceId} has {_consecutiveFailures} consecutive failures");

            // Test connection if provider is available
            if (_tcpProvider != null && !await _tcpProvider.TestConnectionAsync(cancellationToken))
                return HealthCheckResult.Degraded($"Device {_config.DeviceId} connection test failed");

            return HealthCheckResult.Healthy($"Device {_config.DeviceId} is operating normally");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Health check failed for device {_config.DeviceId}", ex);
        }
    }

    /// <summary>
    /// Dispose of all resources used by this provider
    /// </summary>
    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        
        try
        {
            StopAsync().Wait(TimeSpan.FromSeconds(10));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during disposal of device {DeviceId}", _config.DeviceId);
        }

        _dataSubject.Dispose();
        _healthSubject.Dispose();
        _operationSemaphore.Dispose();
        _cancellationTokenSource.Dispose();
        _tcpProvider?.Dispose();
    }
}