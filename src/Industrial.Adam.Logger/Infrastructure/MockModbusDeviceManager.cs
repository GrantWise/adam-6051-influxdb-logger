// Industrial.Adam.Logger - Mock Modbus Device Manager for Demo Mode
// Simulates ADAM device communication with realistic counter data

using Industrial.Adam.Logger.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Industrial.Adam.Logger.Infrastructure;

/// <summary>
/// Mock implementation of IModbusDeviceManager for demo mode
/// Generates realistic counter data without requiring actual hardware
/// </summary>
internal class MockModbusDeviceManager : IModbusDeviceManager
{
    private readonly ILogger<MockModbusDeviceManager> _logger;
    private readonly Random _random;
    private uint _counter;
    private DateTime _lastUpdate;
    private bool _disposed;

    public string DeviceId { get; }
    public AdamDeviceConfig Configuration { get; }
    public bool IsConnected { get; private set; }

    public MockModbusDeviceManager(AdamDeviceConfig configuration, ILogger<MockModbusDeviceManager> logger)
    {
        DeviceId = configuration.DeviceId;
        Configuration = configuration;
        _logger = logger;
        _random = new Random();
        _counter = (uint)_random.Next(1000, 10000); // Start with random counter value
        _lastUpdate = DateTime.UtcNow;
        
        _logger.LogInformation("Mock Modbus device manager created for {DeviceId}", DeviceId);
    }

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return false;

        // Simulate connection delay
        await Task.Delay(100, cancellationToken);
        
        IsConnected = true;
        _logger.LogInformation("Mock connection established to device {DeviceId}", DeviceId);
        return true;
    }

    public async Task<ModbusReadResult> ReadRegistersAsync(ushort startAddress, ushort count, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            var error = new ObjectDisposedException(nameof(MockModbusDeviceManager));
            return ModbusReadResult.CreateFailure(error, TimeSpan.Zero);
        }

        // Auto-connect if not connected (same behavior as real ModbusDeviceManager)
        if (!IsConnected && !await ConnectAsync(cancellationToken))
        {
            var error = new InvalidOperationException("Device not connected");
            return ModbusReadResult.CreateFailure(error, TimeSpan.Zero);
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Simulate realistic read delay
            await Task.Delay(_random.Next(10, 50), cancellationToken);

            // Update counter based on time elapsed (simulate production counting)
            var now = DateTime.UtcNow;
            var elapsed = now - _lastUpdate;
            
            // Simulate production rate: 1-5 counts per second on average
            var expectedCounts = (int)(elapsed.TotalSeconds * _random.NextDouble() * 5);
            _counter += (uint)Math.Max(0, expectedCounts);
            _lastUpdate = now;

            // Create register data (32-bit counter split across 2 registers)
            var data = new ushort[count];
            if (startAddress == 0 && count >= 2)
            {
                // Low word in register 0, high word in register 1 (typical ADAM-6051 format)
                data[0] = (ushort)(_counter & 0xFFFF);        // Low 16 bits
                data[1] = (ushort)((_counter >> 16) & 0xFFFF); // High 16 bits
                
                _logger.LogDebug("Mock device {DeviceId} generated counter value: {Counter} (Low: {Low}, High: {High})", 
                    DeviceId, _counter, data[0], data[1]);
            }
            else
            {
                // Fill with mock data for other register ranges
                for (int i = 0; i < count; i++)
                {
                    data[i] = (ushort)_random.Next(0, 65536);
                }
            }

            stopwatch.Stop();
            return ModbusReadResult.CreateSuccess(data, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Mock read operation failed for device {DeviceId}", DeviceId);
            return ModbusReadResult.CreateFailure(ex, stopwatch.Elapsed);
        }
    }

    public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return Task.FromResult(false);

        // Simulate connection test delay
        Task.Delay(50, cancellationToken);
        
        // Mock connection is always successful unless disposed
        return Task.FromResult(IsConnected);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        IsConnected = false;
        
        _logger.LogInformation("Mock Modbus device manager disposed for {DeviceId}", DeviceId);
    }
}