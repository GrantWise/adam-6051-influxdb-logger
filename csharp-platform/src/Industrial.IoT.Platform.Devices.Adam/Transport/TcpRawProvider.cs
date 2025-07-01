// Industrial.IoT.Platform.Devices.Adam - TCP Raw Socket Provider Implementation
// Concrete implementation for managing raw TCP connections following ModbusDeviceManager patterns

using System.Diagnostics;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Industrial.IoT.Platform.Core;
using Industrial.IoT.Platform.Core.Models;
using Microsoft.Extensions.Logging;

namespace Industrial.IoT.Platform.Devices.Adam.Transport;

/// <summary>
/// Manages raw TCP socket connections to ADAM-4571 devices with automatic retry and error handling
/// Follows the exact same patterns as the existing ModbusDeviceManager implementation
/// </summary>
public class TcpRawProvider : ITcpRawProvider
{
    private readonly TcpEndpoint _endpoint;
    private readonly ILogger<TcpRawProvider> _logger;
    private TcpClient? _tcpClient;
    private NetworkStream? _networkStream;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private volatile bool _isConnected;
    private DateTimeOffset _lastConnectionAttempt = DateTimeOffset.MinValue;

    // Reactive streams for data and connection status
    private readonly Subject<byte[]> _dataReceivedSubject = new();
    private readonly Subject<bool> _connectionStatusSubject = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    /// <summary>
    /// Target endpoint configuration
    /// </summary>
    public TcpEndpoint Endpoint => _endpoint;

    /// <summary>
    /// Current connection status
    /// </summary>
    public bool IsConnected => _isConnected;

    /// <summary>
    /// Observable stream of raw data received from the TCP connection
    /// </summary>
    public IObservable<byte[]> DataReceived => _dataReceivedSubject.AsObservable();

    /// <summary>
    /// Observable stream of connection status changes
    /// </summary>
    public IObservable<bool> ConnectionStatus => _connectionStatusSubject.AsObservable();

    /// <summary>
    /// Initialize a new TCP raw provider
    /// </summary>
    /// <param name="endpoint">TCP endpoint configuration</param>
    /// <param name="logger">Logger for diagnostic information</param>
    public TcpRawProvider(TcpEndpoint endpoint, ILogger<TcpRawProvider> logger)
    {
        _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Establish connection to the TCP endpoint with retry logic and connection throttling
    /// Follows the exact same pattern as ModbusDeviceManager.ConnectAsync
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
            await DisconnectInternalAsync();

            // Create and configure TCP client
            _tcpClient = new TcpClient
            {
                ReceiveTimeout = _endpoint.TimeoutMs,
                SendTimeout = _endpoint.TimeoutMs,
                NoDelay = !_endpoint.EnableNagle,
                ReceiveBufferSize = _endpoint.ReceiveBufferSize,
                SendBufferSize = _endpoint.SendBufferSize
            };

            // Configure TCP keep-alive if enabled
            if (_endpoint.KeepAlive)
                _tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            // Establish TCP connection
            await _tcpClient.ConnectAsync(_endpoint.IpAddress, _endpoint.Port);
            _networkStream = _tcpClient.GetStream();

            _isConnected = true;
            _connectionStatusSubject.OnNext(true);

            _logger.LogInformation("Connected to TCP endpoint {IpAddress}:{Port}", 
                _endpoint.IpAddress, _endpoint.Port);

            // Start background data reading task
            _ = Task.Run(BackgroundDataReader, _cancellationTokenSource.Token);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to TCP endpoint {IpAddress}:{Port}", 
                _endpoint.IpAddress, _endpoint.Port);
            _isConnected = false;
            _connectionStatusSubject.OnNext(false);
            await DisconnectInternalAsync(); // Clean up partial connection
            return false;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Send raw data to the connected TCP endpoint
    /// </summary>
    /// <param name="data">Data to send</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation</param>
    /// <returns>Result containing success status and error information</returns>
    public async Task<TcpOperationResult> SendAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        // Ensure we have a connection
        if (!_isConnected && !await ConnectAsync(cancellationToken))
        {
            return TcpOperationResult.CreateFailure(
                new InvalidOperationException("TCP endpoint not connected"), 
                TimeSpan.Zero);
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            if (_networkStream == null)
                throw new InvalidOperationException("Network stream not available");

            await _networkStream.WriteAsync(data, cancellationToken);
            await _networkStream.FlushAsync(cancellationToken);

            _logger.LogDebug("Sent {ByteCount} bytes to TCP endpoint {IpAddress}:{Port}", 
                data.Length, _endpoint.IpAddress, _endpoint.Port);

            return TcpOperationResult.CreateSuccess(Array.Empty<byte>(), stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send data to TCP endpoint {IpAddress}:{Port}", 
                _endpoint.IpAddress, _endpoint.Port);
            _isConnected = false;
            _connectionStatusSubject.OnNext(false);
            return TcpOperationResult.CreateFailure(ex, stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Send raw data and wait for response with timeout
    /// </summary>
    /// <param name="data">Data to send</param>
    /// <param name="timeoutMs">Timeout in milliseconds for the response</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation</param>
    /// <returns>Result containing response data or error information</returns>
    public async Task<TcpOperationResult> SendAndReceiveAsync(byte[] data, int timeoutMs = 5000, CancellationToken cancellationToken = default)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Send the data
            var sendResult = await SendAsync(data, cancellationToken);
            if (!sendResult.Success)
                return sendResult;

            // Wait for response with timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeoutMs);

            var response = await DataReceived
                .Take(1)
                .Timeout(TimeSpan.FromMilliseconds(timeoutMs))
                .FirstOrDefaultAsync();

            if (response?.Length > 0)
            {
                _logger.LogDebug("Received {ByteCount} bytes from TCP endpoint {IpAddress}:{Port}", 
                    response.Length, _endpoint.IpAddress, _endpoint.Port);
                return TcpOperationResult.CreateSuccess(response, stopwatch.Elapsed);
            }

            return TcpOperationResult.CreateFailure(
                new TimeoutException($"No response received within {timeoutMs}ms"), 
                stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Send and receive operation failed for TCP endpoint {IpAddress}:{Port}", 
                _endpoint.IpAddress, _endpoint.Port);
            return TcpOperationResult.CreateFailure(ex, stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Test connection to the endpoint with a lightweight operation
    /// Following the same pattern as ModbusDeviceManager.TestConnectionAsync
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to abort the test</param>
    /// <returns>True if the endpoint is responsive</returns>
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // For ADAM-4571, we can test with an empty command or status request
            var testData = new byte[] { 0x0D }; // Simple carriage return
            var result = await SendAsync(testData, cancellationToken);
            return result.Success;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Disconnect from the endpoint and clean up resources
    /// </summary>
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
    /// Background task to continuously read data from the TCP stream
    /// </summary>
    private async Task BackgroundDataReader()
    {
        var buffer = new byte[Constants.DefaultReceiveBufferSize];
        
        try
        {
            while (_isConnected && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                if (_networkStream == null || !_networkStream.CanRead)
                    break;

                try
                {
                    var bytesRead = await _networkStream.ReadAsync(buffer, _cancellationTokenSource.Token);
                    if (bytesRead > 0)
                    {
                        var data = new byte[bytesRead];
                        Array.Copy(buffer, data, bytesRead);
                        _dataReceivedSubject.OnNext(data);
                    }
                    else
                    {
                        // Connection closed by remote
                        _logger.LogInformation("TCP connection closed by remote endpoint {IpAddress}:{Port}", 
                            _endpoint.IpAddress, _endpoint.Port);
                        break;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Error reading from TCP stream {IpAddress}:{Port}", 
                        _endpoint.IpAddress, _endpoint.Port);
                    break;
                }
            }
        }
        finally
        {
            if (_isConnected)
            {
                _isConnected = false;
                _connectionStatusSubject.OnNext(false);
            }
        }
    }

    /// <summary>
    /// Internal disconnect method without locking
    /// Follows the same pattern as ModbusDeviceManager.Disconnect
    /// </summary>
    private async Task DisconnectInternalAsync()
    {
        try
        {
            if (_networkStream != null)
            {
                await _networkStream.DisposeAsync();
                _networkStream = null;
            }

            _tcpClient?.Close();
            _tcpClient?.Dispose();
            _tcpClient = null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during disconnect for TCP endpoint {IpAddress}:{Port}", 
                _endpoint.IpAddress, _endpoint.Port);
        }
        finally
        {
            if (_isConnected)
            {
                _isConnected = false;
                _connectionStatusSubject.OnNext(false);
            }
        }
    }

    /// <summary>
    /// Dispose of all resources used by this TCP provider
    /// </summary>
    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        DisconnectAsync().Wait(TimeSpan.FromSeconds(5));
        
        _dataReceivedSubject.Dispose();
        _connectionStatusSubject.Dispose();
        _connectionLock.Dispose();
        _cancellationTokenSource.Dispose();
    }
}