// Industrial.Adam.ScaleLogger - Core Service Interface
// Following proven ADAM-6051 interface patterns for industrial reliability

using Industrial.Adam.ScaleLogger.Configuration;
using Industrial.Adam.ScaleLogger.Models;

namespace Industrial.Adam.ScaleLogger.Interfaces;

/// <summary>
/// Core interface for ADAM-4571 scale logging service
/// Following proven ADAM-6051 service patterns for industrial reliability
/// </summary>
public interface IScaleLoggerService : IDisposable
{
    /// <summary>
    /// Reactive stream of all scale weight readings
    /// </summary>
    IObservable<ScaleDataReading> DataStream { get; }

    /// <summary>
    /// Reactive stream of device health updates
    /// </summary>
    IObservable<ScaleDeviceHealth> HealthStream { get; }

    /// <summary>
    /// Whether the service is currently running
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Currently configured devices
    /// </summary>
    IReadOnlyList<string> ConfiguredDevices { get; }

    /// <summary>
    /// Start the scale logging service
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if started successfully</returns>
    Task<bool> StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop the scale logging service
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task completion</returns>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Add a new scale device at runtime
    /// </summary>
    /// <param name="deviceConfig">Device configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if device added successfully</returns>
    Task<bool> AddDeviceAsync(ScaleDeviceConfig deviceConfig, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove a scale device at runtime
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if device removed successfully</returns>
    Task<bool> RemoveDeviceAsync(string deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get current health status for a specific device
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <returns>Device health or null if device not found</returns>
    Task<ScaleDeviceHealth?> GetDeviceHealthAsync(string deviceId);

    /// <summary>
    /// Get health status for all devices
    /// </summary>
    /// <returns>List of device health information</returns>
    Task<IReadOnlyList<ScaleDeviceHealth>> GetAllDeviceHealthAsync();

    /// <summary>
    /// Test connectivity to a scale device without adding it
    /// </summary>
    /// <param name="deviceConfig">Device configuration to test</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Test result with success status and details</returns>
    Task<ConnectivityTestResult> TestDeviceConnectivityAsync(ScaleDeviceConfig deviceConfig, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Trigger immediate reading from a specific device
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Scale readings or empty list if failed</returns>
    Task<IReadOnlyList<ScaleDataReading>> ReadDeviceNowAsync(string deviceId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Discover protocol for a scale device
    /// </summary>
    /// <param name="host">Scale IP address</param>
    /// <param name="port">Scale TCP port</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Discovered protocol template or null if none found</returns>
    Task<ProtocolTemplate?> DiscoverProtocolAsync(string host, int port, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of device connectivity testing
/// </summary>
public sealed record ConnectivityTestResult
{
    /// <summary>
    /// Whether the test was successful
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Time taken to complete the test
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Error message if test failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Protocol template that worked (if any)
    /// </summary>
    public ProtocolTemplate? WorkingProtocol { get; init; }

    /// <summary>
    /// Sample readings obtained during test
    /// </summary>
    public IReadOnlyList<ScaleDataReading> TestReadings { get; init; } = Array.Empty<ScaleDataReading>();

    /// <summary>
    /// Additional diagnostic information
    /// </summary>
    public IReadOnlyDictionary<string, object> Diagnostics { get; init; } = 
        new Dictionary<string, object>();
}