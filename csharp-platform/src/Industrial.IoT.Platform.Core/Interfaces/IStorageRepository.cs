// Industrial.IoT.Platform.Core - Storage Repository Abstractions
// Base interfaces for multi-database storage following existing patterns

using Industrial.IoT.Platform.Core.Models;

namespace Industrial.IoT.Platform.Core.Interfaces;

/// <summary>
/// Base interface for data storage repositories providing common operations
/// Supports multiple storage backends (InfluxDB, SQL Server, Redis, etc.)
/// </summary>
public interface IStorageRepository : IDisposable
{
    /// <summary>
    /// Storage type identifier (e.g., "InfluxDB", "SQLServer", "Redis")
    /// </summary>
    string StorageType { get; }

    /// <summary>
    /// Whether the repository is connected and ready for operations
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Connect to the storage backend
    /// </summary>
    /// <param name="connectionString">Storage-specific connection configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task indicating connection success</returns>
    Task<bool> ConnectAsync(string connectionString, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnect from the storage backend
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when disconnected</returns>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Test connectivity to the storage backend
    /// </summary>
    /// <param name="timeout">Test timeout</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Connectivity test result</returns>
    Task<StorageTestResult> TestConnectivityAsync(TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get health status of the storage backend
    /// </summary>
    /// <returns>Storage health information</returns>
    Task<StorageHealth> GetHealthAsync();
}

/// <summary>
/// Interface for time-series data repositories (InfluxDB, TimescaleDB, etc.)
/// Optimized for high-frequency sensor data with time-based queries
/// </summary>
public interface ITimeSeriesRepository : IStorageRepository
{
    /// <summary>
    /// Write time-series data points in batches
    /// </summary>
    /// <param name="dataPoints">Data points to write</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Write operation result</returns>
    Task<StorageWriteResult> WriteTimeSeriesAsync(IReadOnlyList<IDataReading> dataPoints, CancellationToken cancellationToken = default);

    /// <summary>
    /// Query time-series data with time range and filters
    /// </summary>
    /// <param name="query">Time-series query parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Query results</returns>
    Task<TimeSeriesQueryResult> QueryTimeSeriesAsync(TimeSeriesQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get latest values for specified devices/channels
    /// </summary>
    /// <param name="deviceIds">Device identifiers</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Latest data points</returns>
    Task<IReadOnlyList<IDataReading>> GetLatestValuesAsync(IReadOnlyList<string> deviceIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete time-series data older than specified retention period
    /// </summary>
    /// <param name="retentionPeriod">Data retention period</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of data points deleted</returns>
    Task<long> CleanupOldDataAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for transactional data repositories (SQL Server, PostgreSQL, etc.)
/// Optimized for discrete records with ACID transactions and relational queries
/// </summary>
public interface ITransactionalRepository : IStorageRepository
{
    /// <summary>
    /// Write discrete data records with transaction support
    /// </summary>
    /// <param name="records">Data records to write</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Write operation result</returns>
    Task<StorageWriteResult> WriteRecordsAsync(IReadOnlyList<IDataReading> records, CancellationToken cancellationToken = default);

    /// <summary>
    /// Query records with complex filtering and joins
    /// </summary>
    /// <param name="query">Relational query parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Query results</returns>
    Task<RelationalQueryResult> QueryRecordsAsync(RelationalQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute a transaction with multiple operations
    /// </summary>
    /// <param name="operations">Operations to execute in transaction</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Transaction result</returns>
    Task<TransactionResult> ExecuteTransactionAsync(IReadOnlyList<StorageOperation> operations, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get record count for specified criteria
    /// </summary>
    /// <param name="criteria">Count criteria</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Record count</returns>
    Task<long> CountRecordsAsync(CountCriteria criteria, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for configuration and template repositories
/// Specialized for storing device configurations, protocol templates, and metadata
/// </summary>
public interface IConfigurationRepository : IStorageRepository
{
    /// <summary>
    /// Save device configuration
    /// </summary>
    /// <param name="configuration">Device configuration to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Save operation result</returns>
    Task<StorageWriteResult> SaveDeviceConfigurationAsync(IDeviceConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Load device configuration by ID
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Device configuration or null if not found</returns>
    Task<IDeviceConfiguration?> LoadDeviceConfigurationAsync(string deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Save protocol template
    /// </summary>
    /// <param name="template">Protocol template to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Save operation result</returns>
    Task<StorageWriteResult> SaveProtocolTemplateAsync(IProtocolTemplate template, CancellationToken cancellationToken = default);

    /// <summary>
    /// Load protocol template by ID
    /// </summary>
    /// <param name="templateId">Template identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Protocol template or null if not found</returns>
    Task<IProtocolTemplate?> LoadProtocolTemplateAsync(string templateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// List all available protocol templates
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Available protocol templates</returns>
    Task<IReadOnlyList<IProtocolTemplate>> ListProtocolTemplatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete configuration or template
    /// </summary>
    /// <param name="id">Configuration/template identifier</param>
    /// <param name="type">Type of item to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Delete operation result</returns>
    Task<StorageWriteResult> DeleteAsync(string id, string type, CancellationToken cancellationToken = default);
}