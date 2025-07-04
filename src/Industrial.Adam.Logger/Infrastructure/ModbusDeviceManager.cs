// Industrial.Adam.Logger - Modbus Device Manager Implementation
// Concrete implementation for managing Modbus TCP connections to ADAM devices

using System.Diagnostics;
using System.Net.Sockets;
using Industrial.Adam.Logger.Configuration;
using Microsoft.Extensions.Logging;
using NModbus;

namespace Industrial.Adam.Logger.Infrastructure;

/// <summary>
/// Manages Modbus TCP connections to individual ADAM devices with automatic retry and error handling
/// </summary>
internal class ModbusDeviceManager : IModbusDeviceManager
{
    private readonly AdamDeviceConfig _config;
    private readonly ILogger<ModbusDeviceManager> _logger;
    private TcpClient? _tcpClient;
    private IModbusMaster? _modbusMaster;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private volatile bool _isConnected;
    private DateTimeOffset _lastConnectionAttempt = DateTimeOffset.MinValue;

    /// <summary>
    /// Unique identifier for the device being managed
    /// </summary>
    public string DeviceId => _config.DeviceId;

    /// <summary>
    /// Configuration for this device instance
    /// </summary>
    public AdamDeviceConfig Configuration => _config;

    /// <summary>
    /// Current connection status
    /// </summary>
    public bool IsConnected => _isConnected;

    /// <summary>
    /// Initialize a new Modbus device manager
    /// </summary>
    /// <param name="config">Device configuration</param>
    /// <param name="logger">Logger for diagnostic information</param>
    public ModbusDeviceManager(AdamDeviceConfig config, ILogger<ModbusDeviceManager> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Establish connection to the device with retry logic and connection throttling
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to abort the connection attempt</param>
    /// <returns>True if connection was successful</returns>
    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        // Prevent connection spam by enforcing a cooldown period
        if (DateTimeOffset.UtcNow - _lastConnectionAttempt < TimeSpan.FromSeconds(Constants.ConnectionRetryCooldownSeconds))
            return _isConnected;

        _lastConnectionAttempt = DateTimeOffset.UtcNow;

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            // Clean up any existing connection
            Disconnect();

            // Create and configure TCP client
            _tcpClient = new TcpClient
            {
                ReceiveTimeout = _config.TimeoutMs,
                SendTimeout = _config.TimeoutMs,
                NoDelay = !_config.EnableNagle,
                ReceiveBufferSize = _config.ReceiveBufferSize,
                SendBufferSize = _config.SendBufferSize
            };

            // Configure TCP keep-alive if enabled
            if (_config.KeepAlive)
                _tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            // Establish TCP connection
            await _tcpClient.ConnectAsync(_config.IpAddress, _config.Port);

            // Create and configure Modbus master
            var factory = new ModbusFactory();
            _modbusMaster = factory.CreateMaster(_tcpClient);
            _modbusMaster.Transport.ReadTimeout = _config.TimeoutMs;
            _modbusMaster.Transport.WriteTimeout = _config.TimeoutMs;
            _modbusMaster.Transport.Retries = Math.Max(0, _config.MaxRetries);

            _isConnected = true;
            _logger.LogInformation("Connected to ADAM device {DeviceId} at {IpAddress}:{Port}", 
                _config.DeviceId, _config.IpAddress, _config.Port);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to ADAM device {DeviceId} at {IpAddress}:{Port}", 
                _config.DeviceId, _config.IpAddress, _config.Port);
            _isConnected = false;
            Disconnect(); // Clean up partial connection
            return false;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Read Modbus holding registers with automatic retry and error handling
    /// </summary>
    /// <param name="startAddress">Starting register address</param>
    /// <param name="count">Number of registers to read</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation</param>
    /// <returns>Result containing register data or error information</returns>
    public async Task<ModbusReadResult> ReadRegistersAsync(ushort startAddress, ushort count, CancellationToken cancellationToken = default)
    {
        // Ensure we have a connection
        if (!_isConnected && !await ConnectAsync(cancellationToken))
        {
            return ModbusReadResult.CreateFailure(
                new InvalidOperationException("Device not connected"), 
                TimeSpan.Zero);
        }

        var stopwatch = Stopwatch.StartNew();
        
        // Attempt the read operation with retry logic
        for (int attempt = 0; attempt <= _config.MaxRetries; attempt++)
        {
            try
            {
                if (_modbusMaster == null)
                    throw new InvalidOperationException("Modbus master not initialized");

                // Perform the Modbus read operation
                var registers = await Task.Run(() => 
                    _modbusMaster.ReadHoldingRegisters(_config.UnitId, startAddress, count), 
                    cancellationToken);

                return ModbusReadResult.CreateSuccess(registers, stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Modbus read attempt {Attempt} failed for device {DeviceId}", 
                    attempt + 1, _config.DeviceId);

                // If we have more attempts, try to reconnect and retry
                if (attempt < _config.MaxRetries)
                {
                    _isConnected = false;
                    await Task.Delay(_config.RetryDelayMs, cancellationToken);
                    await ConnectAsync(cancellationToken);
                }
                else
                {
                    // All retries exhausted
                    return ModbusReadResult.CreateFailure(ex, stopwatch.Elapsed);
                }
            }
        }

        // Fallback (should not reach here)
        return ModbusReadResult.CreateFailure(
            new TimeoutException("Max retries exceeded"), 
            stopwatch.Elapsed);
    }

    /// <summary>
    /// Test connection to the device with a lightweight read operation
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to abort the test</param>
    /// <returns>True if the device is responsive</returns>
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to read a single register to test connectivity
            // Most ADAM devices support reading register 0
            var result = await ReadRegistersAsync(0, 1, cancellationToken);
            return result.Success;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Disconnect from the device and clean up resources
    /// </summary>
    private void Disconnect()
    {
        try
        {
            _modbusMaster?.Dispose();
            _tcpClient?.Close();
            _tcpClient?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during disconnect for device {DeviceId}", _config.DeviceId);
        }
        finally
        {
            _modbusMaster = null;
            _tcpClient = null;
            _isConnected = false;
        }
    }

    /// <summary>
    /// Dispose of all resources used by this device manager
    /// </summary>
    public void Dispose()
    {
        Disconnect();
        _connectionLock.Dispose();
    }
}