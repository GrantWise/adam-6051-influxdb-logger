// Industrial.IoT.Platform.Core - Device Provider Abstractions
// Core interfaces for extensible device integration following existing patterns

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Industrial.IoT.Platform.Core.Models;

namespace Industrial.IoT.Platform.Core.Interfaces;

/// <summary>
/// Core interface for device providers enabling extensible device integration
/// Follows same patterns as existing IAdamLoggerService from Industrial.Adam.Logger
/// </summary>
public interface IDeviceProvider : IDisposable, IHealthCheck
{
    /// <summary>
    /// Type identifier for this device provider (e.g., "ADAM-6051", "ADAM-4571")
    /// </summary>
    string DeviceType { get; }

    /// <summary>
    /// Manufacturer name for this device provider (e.g., "Advantech", "Siemens")
    /// </summary>
    string Manufacturer { get; }

    /// <summary>
    /// Unique identifier for the specific device instance
    /// </summary>
    string DeviceId { get; }

    /// <summary>
    /// Current device configuration
    /// </summary>
    IDeviceConfiguration Configuration { get; }

    /// <summary>
    /// Reactive stream of all data readings from this device
    /// Compatible with existing reactive patterns from Industrial.Adam.Logger
    /// </summary>
    IObservable<IDataReading> DataStream { get; }

    /// <summary>
    /// Reactive stream of device health updates
    /// Compatible with existing health monitoring patterns
    /// </summary>
    IObservable<IDeviceHealth> HealthStream { get; }

    /// <summary>
    /// Check if the device provider is currently running and acquiring data
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Connect to the device and initialize communication
    /// </summary>
    /// <param name="configuration">Device-specific configuration</param>
    /// <param name="cancellationToken">Cancellation token to abort the connection</param>
    /// <returns>Task that completes when connection is established</returns>
    Task<bool> ConnectAsync(IDeviceConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Start data acquisition from the device
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to abort the start operation</param>
    /// <returns>Task that completes when data acquisition has started</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop data acquisition and disconnect from the device
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to abort the stop operation</param>
    /// <returns>Task that completes when the device has been stopped</returns>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get current health status for this device
    /// </summary>
    /// <returns>Current health status</returns>
    Task<IDeviceHealth> GetHealthAsync();

    /// <summary>
    /// Update device configuration at runtime
    /// </summary>
    /// <param name="configuration">Updated configuration for the device</param>
    /// <param name="cancellationToken">Cancellation token to abort the update</param>
    /// <returns>Task that completes when the configuration has been updated</returns>
    Task UpdateConfigurationAsync(IDeviceConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Test connectivity to the device without starting data acquisition
    /// </summary>
    /// <param name="configuration">Configuration to test</param>
    /// <param name="cancellationToken">Cancellation token to abort the test</param>
    /// <returns>Task with test result indicating success/failure and details</returns>
    Task<DeviceTestResult> TestConnectivityAsync(IDeviceConfiguration configuration, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of device connectivity testing
/// </summary>
public sealed record DeviceTestResult
{
    /// <summary>
    /// Whether the connectivity test was successful
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Time taken to complete the connectivity test
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Error message if the test failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Additional diagnostic information from the test
    /// </summary>
    public IReadOnlyDictionary<string, object> Diagnostics { get; init; } = new Dictionary<string, object>();
}