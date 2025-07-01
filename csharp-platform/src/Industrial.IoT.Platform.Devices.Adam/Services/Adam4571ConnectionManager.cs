// Industrial.IoT.Platform.Devices.Adam - ADAM-4571 Connection Management Service
// Focused service for managing TCP connections and protocol discovery following SRP

using System.Reactive.Linq;
using Industrial.IoT.Platform.Core;
using Industrial.IoT.Platform.Core.Interfaces;
using Industrial.IoT.Platform.Devices.Adam.Configuration;
using Industrial.IoT.Platform.Devices.Adam.Transport;
using Microsoft.Extensions.Logging;

namespace Industrial.IoT.Platform.Devices.Adam.Services;

/// <summary>
/// Manages TCP connections and protocol discovery for ADAM-4571 devices
/// Single Responsibility: Connection lifecycle and protocol negotiation
/// </summary>
public class Adam4571ConnectionManager : IDisposable
{
    private readonly Adam4571Configuration _config;
    private readonly IProtocolDiscovery _protocolDiscovery;
    private readonly ILogger<Adam4571ConnectionManager> _logger;
    private readonly ILoggerFactory _loggerFactory;

    private ITcpRawProvider? _tcpProvider;
    private string? _discoveredProtocol;
    private readonly object _protocolLock = new();
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private volatile bool _isConnected;
    private DateTimeOffset _lastConnectionAttempt = DateTimeOffset.MinValue;

    /// <summary>
    /// Current TCP transport provider
    /// </summary>
    public ITcpRawProvider? TcpProvider => _tcpProvider;

    /// <summary>
    /// Whether the device is currently connected and responsive
    /// </summary>
    public bool IsConnected => _isConnected;

    /// <summary>
    /// Discovered protocol identifier
    /// </summary>
    public string? DiscoveredProtocol => _discoveredProtocol;

    /// <summary>
    /// Observable stream of connection status changes
    /// </summary>
    public IObservable<bool> ConnectionStatus => _tcpProvider?.ConnectionStatus ?? 
        System.Reactive.Linq.Observable.Empty<bool>();

    /// <summary>
    /// Observable stream of raw data received from the TCP connection
    /// </summary>
    public IObservable<byte[]> DataReceived => _tcpProvider?.DataReceived ?? 
        System.Reactive.Linq.Observable.Empty<byte[]>();

    /// <summary>
    /// Initialize the connection manager
    /// </summary>
    /// <param name="config">Device configuration</param>
    /// <param name="protocolDiscovery">Protocol discovery service</param>
    /// <param name="logger">Logger for diagnostic information</param>
    /// <param name="loggerFactory">Logger factory for creating additional loggers</param>
    public Adam4571ConnectionManager(
        Adam4571Configuration config,
        IProtocolDiscovery protocolDiscovery,
        ILogger<Adam4571ConnectionManager> logger,
        ILoggerFactory loggerFactory)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _protocolDiscovery = protocolDiscovery ?? throw new ArgumentNullException(nameof(protocolDiscovery));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    /// <summary>
    /// Establish connection to the device with retry logic and protocol discovery
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to abort the connection attempt</param>
    /// <returns>True if connection and protocol discovery were successful</returns>
    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        // Prevent connection spam by enforcing a cooldown period
        if (DateTimeOffset.UtcNow - _lastConnectionAttempt < TimeSpan.FromSeconds(Constants.ConnectionRetryCooldownSeconds))
            return _isConnected;

        _lastConnectionAttempt = DateTimeOffset.UtcNow;

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation("Connecting to ADAM-4571 device {DeviceId} at {IpAddress}:{Port}", 
                _config.DeviceId, _config.IpAddress, _config.Port);

            // Clean up any existing connection
            await DisconnectInternalAsync();

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
                    await DisconnectInternalAsync();
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

            _logger.LogInformation("Successfully connected to ADAM-4571 device {DeviceId} using protocol '{Protocol}'", 
                _config.DeviceId, _discoveredProtocol ?? "Unknown");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to ADAM-4571 device {DeviceId}", _config.DeviceId);
            _isConnected = false;
            await DisconnectInternalAsync();
            return false;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Disconnect from the device and clean up resources
    /// </summary>
    /// <returns>Task representing the disconnect operation</returns>
    public async Task DisconnectAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            await DisconnectInternalAsync();
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Test connectivity to the device without establishing a persistent connection
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to abort the test</param>
    /// <returns>Test result with success status and diagnostics</returns>
    public async Task<DeviceTestResult> TestConnectivityAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            // Create a temporary TCP provider for testing
            var endpoint = _config.CreateTcpEndpoint();
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
                    ["Endpoint"] = $"{_config.IpAddress}:{_config.Port}"
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
    /// Internal disconnect method without locking
    /// </summary>
    /// <returns>Task representing the disconnect operation</returns>
    private async Task DisconnectInternalAsync()
    {
        try
        {
            if (_tcpProvider != null)
            {
                await _tcpProvider.DisconnectAsync();
                _tcpProvider.Dispose();
                _tcpProvider = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during disconnect for device {DeviceId}", _config.DeviceId);
        }
        finally
        {
            _isConnected = false;
            lock (_protocolLock)
            {
                _discoveredProtocol = null;
            }
        }
    }

    /// <summary>
    /// Dispose of all resources used by this connection manager
    /// </summary>
    public void Dispose()
    {
        try
        {
            DisconnectAsync().Wait(TimeSpan.FromSeconds(Constants.DefaultDeviceTimeoutMs / 1000));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during disposal of connection manager for device {DeviceId}", _config.DeviceId);
        }

        _connectionLock.Dispose();
    }
}