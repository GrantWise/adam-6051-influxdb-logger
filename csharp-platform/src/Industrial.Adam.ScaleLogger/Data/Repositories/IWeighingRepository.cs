// Industrial.Adam.ScaleLogger - Weighing Repository Interface
// Repository pattern for weighing transaction data access

using Industrial.Adam.ScaleLogger.Configuration;
using Industrial.Adam.ScaleLogger.Data.Entities;
using Industrial.Adam.ScaleLogger.Models;

namespace Industrial.Adam.ScaleLogger.Data.Repositories;

/// <summary>
/// Repository interface for weighing transaction operations
/// Following proven ADAM-6051 repository patterns
/// </summary>
public interface IWeighingRepository
{
    /// <summary>
    /// Save a weighing transaction
    /// </summary>
    /// <param name="reading">Scale data reading to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Saved transaction</returns>
    Task<WeighingTransaction> SaveWeighingAsync(ScaleDataReading reading, CancellationToken cancellationToken = default);

    /// <summary>
    /// Save multiple weighing transactions in a batch
    /// </summary>
    /// <param name="readings">Scale data readings to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of transactions saved</returns>
    Task<int> SaveWeighingsAsync(IEnumerable<ScaleDataReading> readings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get weighing transactions for a specific device
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="from">Start time filter</param>
    /// <param name="to">End time filter</param>
    /// <param name="limit">Maximum number of records to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Weighing transactions</returns>
    Task<IReadOnlyList<WeighingTransaction>> GetWeighingsAsync(
        string deviceId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int limit = 1000,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get weighing transactions by product code
    /// </summary>
    /// <param name="productCode">Product code</param>
    /// <param name="from">Start time filter</param>
    /// <param name="to">End time filter</param>
    /// <param name="limit">Maximum number of records to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Weighing transactions</returns>
    Task<IReadOnlyList<WeighingTransaction>> GetWeighingsByProductAsync(
        string productCode,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int limit = 1000,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get weighing transactions by batch number
    /// </summary>
    /// <param name="batchNumber">Batch number</param>
    /// <param name="from">Start time filter</param>
    /// <param name="to">End time filter</param>
    /// <param name="limit">Maximum number of records to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Weighing transactions</returns>
    Task<IReadOnlyList<WeighingTransaction>> GetWeighingsByBatchAsync(
        string batchNumber,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int limit = 1000,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get latest weighing for each device
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Latest weighing per device</returns>
    Task<IReadOnlyList<WeighingTransaction>> GetLatestWeighingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get weighing statistics for a device
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="from">Start time filter</param>
    /// <param name="to">End time filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Weighing statistics</returns>
    Task<WeighingStatistics> GetWeighingStatisticsAsync(
        string deviceId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete old weighing transactions (data retention)
    /// </summary>
    /// <param name="olderThan">Delete transactions older than this date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of transactions deleted</returns>
    Task<int> DeleteOldWeighingsAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for device management operations
/// </summary>
public interface IDeviceRepository
{
    /// <summary>
    /// Register or update a scale device
    /// </summary>
    /// <param name="deviceConfig">Device configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Scale device entity</returns>
    Task<ScaleDevice> UpsertDeviceAsync(ScaleDeviceConfig deviceConfig, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a scale device by ID
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Scale device or null if not found</returns>
    Task<ScaleDevice?> GetDeviceAsync(string deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all active scale devices
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Active scale devices</returns>
    Task<IReadOnlyList<ScaleDevice>> GetActiveDevicesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivate a scale device
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if device was deactivated</returns>
    Task<bool> DeactivateDeviceAsync(string deviceId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for system event operations
/// </summary>
public interface ISystemEventRepository
{
    /// <summary>
    /// Log a system event
    /// </summary>
    /// <param name="eventType">Event type</param>
    /// <param name="message">Event message</param>
    /// <param name="deviceId">Associated device ID (optional)</param>
    /// <param name="severity">Event severity</param>
    /// <param name="details">Additional event details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created system event</returns>
    Task<SystemEvent> LogEventAsync(
        string eventType,
        string message,
        string? deviceId = null,
        string severity = "Information",
        object? details = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get system events
    /// </summary>
    /// <param name="from">Start time filter</param>
    /// <param name="to">End time filter</param>
    /// <param name="eventType">Event type filter</param>
    /// <param name="deviceId">Device ID filter</param>
    /// <param name="severity">Severity filter</param>
    /// <param name="limit">Maximum number of records to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>System events</returns>
    Task<IReadOnlyList<SystemEvent>> GetEventsAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string? eventType = null,
        string? deviceId = null,
        string? severity = null,
        int limit = 1000,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete old system events (data retention)
    /// </summary>
    /// <param name="olderThan">Delete events older than this date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of events deleted</returns>
    Task<int> DeleteOldEventsAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default);
}

/// <summary>
/// Weighing statistics data
/// </summary>
public sealed record WeighingStatistics
{
    public required string DeviceId { get; init; }
    public required int TotalWeighings { get; init; }
    public required decimal MinWeight { get; init; }
    public required decimal MaxWeight { get; init; }
    public required decimal AverageWeight { get; init; }
    public required DateTimeOffset FirstWeighing { get; init; }
    public required DateTimeOffset LastWeighing { get; init; }
    public required int StableWeighings { get; init; }
    public required int GoodQualityWeighings { get; init; }
}