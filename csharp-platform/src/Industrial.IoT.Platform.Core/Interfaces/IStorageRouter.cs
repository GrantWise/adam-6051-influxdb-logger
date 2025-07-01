// Industrial.IoT.Platform.Core - Storage Router Abstractions
// Intelligent routing of data to appropriate storage backends based on data classification

using Industrial.IoT.Platform.Core.Models;

namespace Industrial.IoT.Platform.Core.Interfaces;

/// <summary>
/// Interface for intelligent data routing to appropriate storage backends
/// Routes data based on classification, performance requirements, and storage policies
/// </summary>
public interface IStorageRouter : IDisposable
{
    /// <summary>
    /// Route data reading to appropriate storage backend(s)
    /// </summary>
    /// <param name="dataReading">Data reading to store</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Routing result with storage outcomes</returns>
    Task<StorageRoutingResult> RouteDataAsync(IDataReading dataReading, CancellationToken cancellationToken = default);

    /// <summary>
    /// Route multiple data readings in batch for optimal performance
    /// </summary>
    /// <param name="dataReadings">Data readings to store</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Batch routing result</returns>
    Task<BatchStorageRoutingResult> RouteDataBatchAsync(IReadOnlyList<IDataReading> dataReadings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determine appropriate storage backend(s) for data classification
    /// </summary>
    /// <param name="dataClassification">Data classification</param>
    /// <param name="storagePolicy">Storage policy requirements</param>
    /// <returns>Recommended storage backends</returns>
    Task<StorageRecommendation> GetStorageRecommendationAsync(DataClassification dataClassification, StoragePolicy storagePolicy);

    /// <summary>
    /// Register a storage repository with the router
    /// </summary>
    /// <param name="repository">Storage repository to register</param>
    /// <param name="classification">Data classifications this repository handles</param>
    /// <param name="priority">Priority for this repository (higher = preferred)</param>
    /// <returns>Task that completes when repository is registered</returns>
    Task RegisterRepositoryAsync(IStorageRepository repository, DataClassification classification, int priority = 0);

    /// <summary>
    /// Unregister a storage repository from the router
    /// </summary>
    /// <param name="storageType">Storage type to unregister</param>
    /// <param name="classification">Data classification to unregister for</param>
    /// <returns>Task that completes when repository is unregistered</returns>
    Task UnregisterRepositoryAsync(string storageType, DataClassification classification);

    /// <summary>
    /// Get health status of all registered storage repositories
    /// </summary>
    /// <returns>Health status for each storage backend</returns>
    Task<IReadOnlyDictionary<string, StorageHealth>> GetStorageHealthAsync();

    /// <summary>
    /// Event raised when storage routing fails
    /// </summary>
    event EventHandler<StorageFailureEventArgs> StorageFailure;

    /// <summary>
    /// Event raised when storage performance metrics are updated
    /// </summary>
    event EventHandler<StorageMetricsEventArgs> MetricsUpdated;
}

/// <summary>
/// Result of routing data to storage backend(s)
/// </summary>
public sealed record StorageRoutingResult
{
    /// <summary>
    /// Whether routing was successful
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Data reading that was routed
    /// </summary>
    public required IDataReading DataReading { get; init; }

    /// <summary>
    /// Storage backends that received the data
    /// </summary>
    public IReadOnlyList<string> StorageBackends { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Results from each storage backend
    /// </summary>
    public IReadOnlyDictionary<string, StorageWriteResult> BackendResults { get; init; } = new Dictionary<string, StorageWriteResult>();

    /// <summary>
    /// Total time taken for routing and storage
    /// </summary>
    public TimeSpan TotalDuration { get; init; }

    /// <summary>
    /// Error message if routing failed
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Result of batch routing multiple data readings
/// </summary>
public sealed record BatchStorageRoutingResult
{
    /// <summary>
    /// Whether batch routing was successful
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Number of data readings processed
    /// </summary>
    public required int TotalReadings { get; init; }

    /// <summary>
    /// Number of readings successfully stored
    /// </summary>
    public required int SuccessfulReadings { get; init; }

    /// <summary>
    /// Results grouped by storage backend
    /// </summary>
    public IReadOnlyDictionary<string, BatchStorageResult> BackendResults { get; init; } = new Dictionary<string, BatchStorageResult>();

    /// <summary>
    /// Total time taken for batch processing
    /// </summary>
    public TimeSpan TotalDuration { get; init; }

    /// <summary>
    /// Individual routing results (for detailed analysis)
    /// </summary>
    public IReadOnlyList<StorageRoutingResult> IndividualResults { get; init; } = Array.Empty<StorageRoutingResult>();

    /// <summary>
    /// Error messages for failed routings
    /// </summary>
    public IReadOnlyList<string> ErrorMessages { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Storage recommendation for specific data classification and policy
/// </summary>
public sealed record StorageRecommendation
{
    /// <summary>
    /// Primary storage backend recommendation
    /// </summary>
    public required string PrimaryStorage { get; init; }

    /// <summary>
    /// Secondary storage backends for backup/archival
    /// </summary>
    public IReadOnlyList<string> SecondaryStorage { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Confidence score for recommendation (0-100)
    /// </summary>
    public required double ConfidenceScore { get; init; }

    /// <summary>
    /// Reasoning for storage recommendation
    /// </summary>
    public required string Reasoning { get; init; }

    /// <summary>
    /// Expected performance characteristics
    /// </summary>
    public StoragePerformanceExpectation Performance { get; init; } = new();
}

/// <summary>
/// Expected performance characteristics for storage recommendation
/// </summary>
public sealed record StoragePerformanceExpectation
{
    /// <summary>
    /// Expected write latency in milliseconds
    /// </summary>
    public double ExpectedWriteLatency { get; init; }

    /// <summary>
    /// Expected query latency in milliseconds
    /// </summary>
    public double ExpectedQueryLatency { get; init; }

    /// <summary>
    /// Expected throughput in operations per second
    /// </summary>
    public double ExpectedThroughput { get; init; }

    /// <summary>
    /// Expected storage efficiency (compression ratio)
    /// </summary>
    public double ExpectedCompressionRatio { get; init; } = 1.0;
}