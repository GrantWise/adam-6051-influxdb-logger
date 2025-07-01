// Industrial.IoT.Platform.Core - Storage Models
// Data models for storage operations and results following existing pattern quality

using Industrial.IoT.Platform.Core.Interfaces;

namespace Industrial.IoT.Platform.Core.Models;

/// <summary>
/// Result of storage write operations
/// </summary>
public sealed record StorageWriteResult
{
    /// <summary>
    /// Whether the write operation was successful
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Number of records written successfully
    /// </summary>
    public int RecordsWritten { get; init; }

    /// <summary>
    /// Number of records that failed to write
    /// </summary>
    public int RecordsFailed { get; init; }

    /// <summary>
    /// Total time taken for write operation
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Storage backend that performed the write
    /// </summary>
    public required string StorageBackend { get; init; }

    /// <summary>
    /// Error message if write failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Additional metadata from storage operation
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// Result of batch storage operations grouped by backend
/// </summary>
public sealed record BatchStorageResult
{
    /// <summary>
    /// Storage backend that processed the batch
    /// </summary>
    public required string StorageBackend { get; init; }

    /// <summary>
    /// Number of records in the batch
    /// </summary>
    public required int BatchSize { get; init; }

    /// <summary>
    /// Number of records written successfully
    /// </summary>
    public required int SuccessfulWrites { get; init; }

    /// <summary>
    /// Total time taken for batch processing
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Average write latency per record
    /// </summary>
    public double AverageLatency => BatchSize > 0 ? Duration.TotalMilliseconds / BatchSize : 0;

    /// <summary>
    /// Error messages for failed writes
    /// </summary>
    public IReadOnlyList<string> ErrorMessages { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Health status of a storage backend
/// </summary>
public sealed record StorageHealth
{
    /// <summary>
    /// Storage backend identifier
    /// </summary>
    public required string StorageBackend { get; init; }

    /// <summary>
    /// Whether storage is connected and operational
    /// </summary>
    public required bool IsHealthy { get; init; }

    /// <summary>
    /// Timestamp when health was last checked
    /// </summary>
    public required DateTimeOffset LastChecked { get; init; }

    /// <summary>
    /// Connection status to storage backend
    /// </summary>
    public bool IsConnected { get; init; }

    /// <summary>
    /// Average response time in milliseconds
    /// </summary>
    public double AverageResponseTime { get; init; }

    /// <summary>
    /// Current utilization percentage (0-100)
    /// </summary>
    public double UtilizationPercentage { get; init; }

    /// <summary>
    /// Available storage capacity information
    /// </summary>
    public StorageCapacity? Capacity { get; init; }

    /// <summary>
    /// Last error encountered (if any)
    /// </summary>
    public string? LastError { get; init; }

    /// <summary>
    /// Health check details and diagnostics
    /// </summary>
    public IReadOnlyDictionary<string, object> Diagnostics { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// Storage capacity information
/// </summary>
public sealed record StorageCapacity
{
    /// <summary>
    /// Total storage capacity in bytes
    /// </summary>
    public long TotalCapacity { get; init; }

    /// <summary>
    /// Used storage capacity in bytes
    /// </summary>
    public long UsedCapacity { get; init; }

    /// <summary>
    /// Available storage capacity in bytes
    /// </summary>
    public long AvailableCapacity => TotalCapacity - UsedCapacity;

    /// <summary>
    /// Usage percentage (0-100)
    /// </summary>
    public double UsagePercentage => TotalCapacity > 0 ? (double)UsedCapacity / TotalCapacity * 100 : 0;
}

/// <summary>
/// Result of storage connectivity testing
/// </summary>
public sealed record StorageTestResult
{
    /// <summary>
    /// Whether connectivity test was successful
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Storage backend that was tested
    /// </summary>
    public required string StorageBackend { get; init; }

    /// <summary>
    /// Time taken to complete the test
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Round-trip latency if applicable
    /// </summary>
    public TimeSpan? Latency { get; init; }

    /// <summary>
    /// Error message if test failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Additional test diagnostics
    /// </summary>
    public IReadOnlyDictionary<string, object> Diagnostics { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// Query parameters for time-series data
/// </summary>
public sealed record TimeSeriesQuery
{
    /// <summary>
    /// Device identifiers to query
    /// </summary>
    public IReadOnlyList<string> DeviceIds { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Start time for query range
    /// </summary>
    public required DateTimeOffset StartTime { get; init; }

    /// <summary>
    /// End time for query range
    /// </summary>
    public required DateTimeOffset EndTime { get; init; }

    /// <summary>
    /// Data aggregation interval (e.g., 1 minute, 5 minutes)
    /// </summary>
    public TimeSpan? AggregationInterval { get; init; }

    /// <summary>
    /// Aggregation function (e.g., "mean", "max", "min", "sum")
    /// </summary>
    public string? AggregationFunction { get; init; }

    /// <summary>
    /// Maximum number of data points to return
    /// </summary>
    public int? MaxDataPoints { get; init; }

    /// <summary>
    /// Additional filter criteria
    /// </summary>
    public IReadOnlyDictionary<string, object> Filters { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// Result of time-series query
/// </summary>
public sealed record TimeSeriesQueryResult
{
    /// <summary>
    /// Whether query was successful
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Data points returned by query
    /// </summary>
    public IReadOnlyList<TimeSeriesDataPoint> DataPoints { get; init; } = Array.Empty<TimeSeriesDataPoint>();

    /// <summary>
    /// Query execution time
    /// </summary>
    public TimeSpan ExecutionTime { get; init; }

    /// <summary>
    /// Total number of data points before limit/aggregation
    /// </summary>
    public long TotalDataPoints { get; init; }

    /// <summary>
    /// Error message if query failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Query metadata and statistics
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// Time-series data point for query results
/// </summary>
public sealed record TimeSeriesDataPoint
{
    /// <summary>
    /// Device identifier
    /// </summary>
    public required string DeviceId { get; init; }

    /// <summary>
    /// Channel or field identifier
    /// </summary>
    public string? Channel { get; init; }

    /// <summary>
    /// Timestamp of the data point
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Data value (can be numeric or other types)
    /// </summary>
    public required object Value { get; init; }

    /// <summary>
    /// Data quality indicator
    /// </summary>
    public DataQuality Quality { get; init; }

    /// <summary>
    /// Additional tags and metadata
    /// </summary>
    public IReadOnlyDictionary<string, object> Tags { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// Query parameters for relational data
/// </summary>
public sealed record RelationalQuery
{
    /// <summary>
    /// Table or entity name to query
    /// </summary>
    public required string TableName { get; init; }

    /// <summary>
    /// Columns to select
    /// </summary>
    public IReadOnlyList<string> SelectColumns { get; init; } = Array.Empty<string>();

    /// <summary>
    /// WHERE clause conditions
    /// </summary>
    public IReadOnlyDictionary<string, object> WhereConditions { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// ORDER BY clause
    /// </summary>
    public IReadOnlyDictionary<string, string> OrderBy { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Maximum number of records to return
    /// </summary>
    public int? Limit { get; init; }

    /// <summary>
    /// Number of records to skip (for pagination)
    /// </summary>
    public int Offset { get; init; } = 0;

    /// <summary>
    /// JOIN clauses for complex queries
    /// </summary>
    public IReadOnlyList<JoinClause> Joins { get; init; } = Array.Empty<JoinClause>();
}

/// <summary>
/// JOIN clause definition for relational queries
/// </summary>
public sealed record JoinClause
{
    /// <summary>
    /// Type of join (e.g., "INNER", "LEFT", "RIGHT")
    /// </summary>
    public required string JoinType { get; init; }

    /// <summary>
    /// Table to join with
    /// </summary>
    public required string TableName { get; init; }

    /// <summary>
    /// ON clause conditions
    /// </summary>
    public required string OnCondition { get; init; }
}

/// <summary>
/// Result of relational query
/// </summary>
public sealed record RelationalQueryResult
{
    /// <summary>
    /// Whether query was successful
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Query result records
    /// </summary>
    public IReadOnlyList<IReadOnlyDictionary<string, object>> Records { get; init; } = Array.Empty<IReadOnlyDictionary<string, object>>();

    /// <summary>
    /// Total number of records matching query (before limit)
    /// </summary>
    public long TotalRecords { get; init; }

    /// <summary>
    /// Query execution time
    /// </summary>
    public TimeSpan ExecutionTime { get; init; }

    /// <summary>
    /// Error message if query failed
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Storage operation for transaction execution
/// </summary>
public sealed record StorageOperation
{
    /// <summary>
    /// Operation type (e.g., "INSERT", "UPDATE", "DELETE")
    /// </summary>
    public required string OperationType { get; init; }

    /// <summary>
    /// Target table or collection
    /// </summary>
    public required string Target { get; init; }

    /// <summary>
    /// Operation data/parameters
    /// </summary>
    public required IReadOnlyDictionary<string, object> Data { get; init; }

    /// <summary>
    /// Operation-specific conditions
    /// </summary>
    public IReadOnlyDictionary<string, object> Conditions { get; init; } = new Dictionary<string, object>();
}

/// <summary>
/// Result of transaction execution
/// </summary>
public sealed record TransactionResult
{
    /// <summary>
    /// Whether transaction was successful
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Number of operations executed
    /// </summary>
    public required int OperationsExecuted { get; init; }

    /// <summary>
    /// Results for individual operations
    /// </summary>
    public IReadOnlyList<StorageWriteResult> OperationResults { get; init; } = Array.Empty<StorageWriteResult>();

    /// <summary>
    /// Total transaction execution time
    /// </summary>
    public TimeSpan ExecutionTime { get; init; }

    /// <summary>
    /// Error message if transaction failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Transaction identifier
    /// </summary>
    public string? TransactionId { get; init; }
}

/// <summary>
/// Criteria for counting records
/// </summary>
public sealed record CountCriteria
{
    /// <summary>
    /// Table or collection to count
    /// </summary>
    public required string TableName { get; init; }

    /// <summary>
    /// Filter conditions for counting
    /// </summary>
    public IReadOnlyDictionary<string, object> Conditions { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Time range for counting (if applicable)
    /// </summary>
    public DateTimeRange? TimeRange { get; init; }
}

/// <summary>
/// Date/time range specification
/// </summary>
public sealed record DateTimeRange
{
    /// <summary>
    /// Start of the time range
    /// </summary>
    public required DateTimeOffset Start { get; init; }

    /// <summary>
    /// End of the time range
    /// </summary>
    public required DateTimeOffset End { get; init; }
}

/// <summary>
/// Event arguments for storage failure events
/// </summary>
public sealed class StorageFailureEventArgs : EventArgs
{
    /// <summary>
    /// Storage backend that failed
    /// </summary>
    public required string StorageBackend { get; init; }

    /// <summary>
    /// Type of operation that failed
    /// </summary>
    public required string OperationType { get; init; }

    /// <summary>
    /// Error message describing the failure
    /// </summary>
    public required string ErrorMessage { get; init; }

    /// <summary>
    /// Exception that caused the failure (if any)
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Data that failed to be stored
    /// </summary>
    public object? FailedData { get; init; }

    /// <summary>
    /// Timestamp when failure occurred
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Event arguments for storage metrics updates
/// </summary>
public sealed class StorageMetricsEventArgs : EventArgs
{
    /// <summary>
    /// Storage backend these metrics apply to
    /// </summary>
    public required string StorageBackend { get; init; }

    /// <summary>
    /// Performance metrics
    /// </summary>
    public required StorageMetrics Metrics { get; init; }

    /// <summary>
    /// Timestamp when metrics were collected
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Storage performance metrics
/// </summary>
public sealed record StorageMetrics
{
    /// <summary>
    /// Average write latency in milliseconds
    /// </summary>
    public double AverageWriteLatency { get; init; }

    /// <summary>
    /// Average query latency in milliseconds
    /// </summary>
    public double AverageQueryLatency { get; init; }

    /// <summary>
    /// Operations per second throughput
    /// </summary>
    public double Throughput { get; init; }

    /// <summary>
    /// Error rate as percentage (0-100)
    /// </summary>
    public double ErrorRate { get; init; }

    /// <summary>
    /// Current connection count
    /// </summary>
    public int ActiveConnections { get; init; }

    /// <summary>
    /// Queue size for pending operations
    /// </summary>
    public int QueueSize { get; init; }
}

/// <summary>
/// Data classification for intelligent storage routing
/// </summary>
public enum DataClassification
{
    /// <summary>
    /// Time-series counter data (ADAM-6051)
    /// Routes to InfluxDB for high-performance time-series queries
    /// </summary>
    TimeSeries = 0,

    /// <summary>
    /// Discrete scale readings (ADAM-4571)
    /// Routes to SQL Server for relational analysis and auditing
    /// </summary>
    DiscreteReading = 1,

    /// <summary>
    /// Device configuration data
    /// Routes to SQL Server for structured configuration management
    /// </summary>
    Configuration = 2,

    /// <summary>
    /// Protocol template definitions
    /// Routes to SQL Server for template management
    /// </summary>
    ProtocolTemplate = 3,

    /// <summary>
    /// System logs and events
    /// Routes to appropriate logging backend
    /// </summary>
    SystemLog = 4,

    /// <summary>
    /// Unclassified data requiring manual routing decision
    /// </summary>
    Unknown = 99
}

/// <summary>
/// Storage policy configuration for data routing
/// </summary>
public sealed record StoragePolicy
{
    /// <summary>
    /// Data classification this policy applies to
    /// </summary>
    public required DataClassification DataClassification { get; init; }

    /// <summary>
    /// Primary storage backend for this data type
    /// </summary>
    public required string PrimaryBackend { get; init; }

    /// <summary>
    /// Fallback storage backend if primary is unavailable
    /// </summary>
    public string? FallbackBackend { get; init; }

    /// <summary>
    /// Data retention period
    /// </summary>
    public TimeSpan? RetentionPeriod { get; init; }

    /// <summary>
    /// Batch size for bulk operations
    /// </summary>
    public int BatchSize { get; init; } = 100;

    /// <summary>
    /// Flush interval for batched writes
    /// </summary>
    public TimeSpan FlushInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether to enable compression for this data type
    /// </summary>
    public bool EnableCompression { get; init; } = true;

    /// <summary>
    /// Whether to replicate data to secondary storage
    /// </summary>
    public bool EnableReplication { get; init; } = false;

    /// <summary>
    /// Custom routing rules and filters
    /// </summary>
    public IReadOnlyDictionary<string, object> RoutingRules { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Performance requirements for this data type
    /// </summary>
    public PerformanceRequirements? PerformanceRequirements { get; init; }
}

/// <summary>
/// Performance requirements for storage policies
/// </summary>
public sealed record PerformanceRequirements
{
    /// <summary>
    /// Maximum acceptable write latency in milliseconds
    /// </summary>
    public double MaxWriteLatency { get; init; } = 1000;

    /// <summary>
    /// Maximum acceptable query latency in milliseconds
    /// </summary>
    public double MaxQueryLatency { get; init; } = 5000;

    /// <summary>
    /// Minimum required throughput (operations per second)
    /// </summary>
    public double MinThroughput { get; init; } = 10;

    /// <summary>
    /// Maximum acceptable error rate (0-100)
    /// </summary>
    public double MaxErrorRate { get; init; } = 1.0;

    /// <summary>
    /// Whether high availability is required
    /// </summary>
    public bool RequireHighAvailability { get; init; } = false;
}