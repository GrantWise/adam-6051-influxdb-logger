// Industrial.Adam.Logger - Core Service Interfaces
// Main interfaces for the ADAM logger service and its components

using Industrial.Adam.Logger.Configuration;
using Industrial.Adam.Logger.Models;

namespace Industrial.Adam.Logger.Interfaces;

/// <summary>
/// Main interface for the ADAM logger service - this is what consuming applications will use
/// </summary>
public interface IAdamLoggerService : IDisposable
{
    /// <summary>
    /// Reactive stream of all data readings from all configured devices
    /// Subscribe to this stream to receive real-time data from ADAM devices
    /// </summary>
    IObservable<AdamDataReading> DataStream { get; }

    /// <summary>
    /// Reactive stream of device health updates
    /// Subscribe to this stream to monitor device connectivity and status
    /// </summary>
    IObservable<AdamDeviceHealth> HealthStream { get; }

    /// <summary>
    /// Start data acquisition from all configured devices
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to abort the start operation</param>
    /// <returns>Task that completes when the service has started</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop data acquisition and disconnect from all devices
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to abort the stop operation</param>
    /// <returns>Task that completes when the service has stopped</returns>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get current health status for a specific device
    /// </summary>
    /// <param name="deviceId">Unique identifier of the device</param>
    /// <returns>Current health status or null if device is not found</returns>
    Task<AdamDeviceHealth?> GetDeviceHealthAsync(string deviceId);

    /// <summary>
    /// Get health status for all configured devices
    /// </summary>
    /// <returns>Collection of device health statuses</returns>
    Task<IReadOnlyList<AdamDeviceHealth>> GetAllDeviceHealthAsync();

    /// <summary>
    /// Check if the service is currently running and acquiring data
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Add a new device to monitor at runtime
    /// </summary>
    /// <param name="deviceConfig">Configuration for the new device</param>
    /// <returns>Task that completes when the device has been added</returns>
    Task AddDeviceAsync(AdamDeviceConfig deviceConfig);

    /// <summary>
    /// Remove a device from monitoring at runtime
    /// </summary>
    /// <param name="deviceId">Unique identifier of the device to remove</param>
    /// <returns>Task that completes when the device has been removed</returns>
    Task RemoveDeviceAsync(string deviceId);

    /// <summary>
    /// Update device configuration at runtime
    /// </summary>
    /// <param name="deviceConfig">Updated configuration for the device</param>
    /// <returns>Task that completes when the configuration has been updated</returns>
    Task UpdateDeviceConfigAsync(AdamDeviceConfig deviceConfig);
}