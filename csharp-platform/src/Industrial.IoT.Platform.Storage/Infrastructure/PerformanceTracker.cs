// Industrial.IoT.Platform.Storage - Performance Tracking Infrastructure
// Performance monitoring and metrics collection for storage operations
// Following existing ADAM logger patterns for metrics and monitoring

using System.Collections.Concurrent;
using Industrial.IoT.Platform.Core.Models;

namespace Industrial.IoT.Platform.Storage.Infrastructure;

/// <summary>
/// Performance tracker for storage operations
/// Provides real-time metrics collection and analysis following existing ADAM logger patterns
/// </summary>
internal sealed class PerformanceTracker
{
    private readonly object _metricsLock = new();
    private readonly ConcurrentQueue<OperationMetric> _recentOperations = new();
    private readonly TimeSpan _metricsWindow = TimeSpan.FromMinutes(5);
    
    // Performance counters
    private long _totalWrites;
    private long _totalQueries;
    private long _totalWriteErrors;
    private long _totalQueryErrors;
    private long _totalPointsWritten;
    private long _totalPointsQueried;
    
    // Timing accumulators
    private double _totalWriteTime;
    private double _totalQueryTime;
    
    // Connection tracking
    private volatile int _activeConnections = 1; // Start with 1 for main connection
    private volatile int _queueSize;
    
    /// <summary>
    /// Record a write operation
    /// </summary>
    /// <param name="pointsWritten">Number of points written</param>
    /// <param name="duration">Operation duration</param>
    /// <param name="pointsFailed">Number of points that failed</param>
    public void RecordWrite(int pointsWritten, TimeSpan duration, int pointsFailed = 0)
    {
        var metric = new OperationMetric
        {
            OperationType = OperationType.Write,
            Timestamp = DateTimeOffset.UtcNow,
            Duration = duration,
            PointsProcessed = pointsWritten,
            PointsFailed = pointsFailed,
            Success = pointsFailed == 0
        };
        
        _recentOperations.Enqueue(metric);
        CleanupOldMetrics();
        
        lock (_metricsLock)
        {
            _totalWrites++;
            _totalPointsWritten += pointsWritten;
            _totalWriteTime += duration.TotalMilliseconds;
            
            if (pointsFailed > 0)
                _totalWriteErrors++;
        }
    }
    
    /// <summary>
    /// Record a query operation
    /// </summary>
    /// <param name="pointsReturned">Number of points returned</param>
    /// <param name="duration">Operation duration</param>
    /// <param name="failed">Whether the query failed</param>
    public void RecordQuery(int pointsReturned, TimeSpan duration, bool failed = false)
    {
        var metric = new OperationMetric
        {
            OperationType = OperationType.Query,
            Timestamp = DateTimeOffset.UtcNow,
            Duration = duration,
            PointsProcessed = pointsReturned,
            Success = !failed
        };
        
        _recentOperations.Enqueue(metric);
        CleanupOldMetrics();
        
        lock (_metricsLock)
        {
            _totalQueries++;
            _totalPointsQueried += pointsReturned;
            _totalQueryTime += duration.TotalMilliseconds;
            
            if (failed)
                _totalQueryErrors++;
        }
    }
    
    /// <summary>
    /// Update connection count
    /// </summary>
    /// <param name="connectionCount">Current number of active connections</param>
    public void UpdateConnectionCount(int connectionCount)
    {
        _activeConnections = connectionCount;
    }
    
    /// <summary>
    /// Update queue size
    /// </summary>
    /// <param name="queueSize">Current queue size</param>
    public void UpdateQueueSize(int queueSize)
    {
        _queueSize = queueSize;
    }
    
    /// <summary>
    /// Get current performance metrics
    /// </summary>
    /// <returns>Current storage metrics</returns>
    public StorageMetrics GetCurrentMetrics()
    {
        lock (_metricsLock)
        {
            var recentMetrics = GetRecentMetrics();
            
            // Calculate averages
            var avgWriteLatency = _totalWrites > 0 ? _totalWriteTime / _totalWrites : 0;
            var avgQueryLatency = _totalQueries > 0 ? _totalQueryTime / _totalQueries : 0;
            
            // Calculate throughput (operations per second over recent window)
            var recentOps = recentMetrics.Count;
            var windowSeconds = _metricsWindow.TotalSeconds;
            var throughput = recentOps > 0 ? recentOps / windowSeconds : 0;
            
            // Calculate error rates
            var totalOps = _totalWrites + _totalQueries;
            var totalErrors = _totalWriteErrors + _totalQueryErrors;
            var errorRate = totalOps > 0 ? (double)totalErrors / totalOps * 100 : 0;
            
            return new StorageMetrics
            {
                AverageWriteLatency = avgWriteLatency,
                AverageQueryLatency = avgQueryLatency,
                Throughput = throughput,
                ErrorRate = errorRate,
                ActiveConnections = _activeConnections,
                QueueSize = _queueSize
            };
        }
    }
    
    /// <summary>
    /// Get detailed performance statistics
    /// </summary>
    /// <returns>Detailed performance statistics</returns>
    public PerformanceStatistics GetDetailedStatistics()
    {
        lock (_metricsLock)
        {
            var recentMetrics = GetRecentMetrics();
            
            // Calculate percentiles for recent operations
            var recentWriteDurations = recentMetrics
                .Where(m => m.OperationType == OperationType.Write && m.Success)
                .Select(m => m.Duration.TotalMilliseconds)
                .OrderBy(d => d)
                .ToList();
            
            var recentQueryDurations = recentMetrics
                .Where(m => m.OperationType == OperationType.Query && m.Success)
                .Select(m => m.Duration.TotalMilliseconds)
                .OrderBy(d => d)
                .ToList();
            
            return new PerformanceStatistics
            {
                TotalWrites = _totalWrites,
                TotalQueries = _totalQueries,
                TotalWriteErrors = _totalWriteErrors,
                TotalQueryErrors = _totalQueryErrors,
                TotalPointsWritten = _totalPointsWritten,
                TotalPointsQueried = _totalPointsQueried,
                AverageWriteLatency = _totalWrites > 0 ? _totalWriteTime / _totalWrites : 0,
                AverageQueryLatency = _totalQueries > 0 ? _totalQueryTime / _totalQueries : 0,
                WriteLatencyP50 = CalculatePercentile(recentWriteDurations, 0.5),
                WriteLatencyP95 = CalculatePercentile(recentWriteDurations, 0.95),
                WriteLatencyP99 = CalculatePercentile(recentWriteDurations, 0.99),
                QueryLatencyP50 = CalculatePercentile(recentQueryDurations, 0.5),
                QueryLatencyP95 = CalculatePercentile(recentQueryDurations, 0.95),
                QueryLatencyP99 = CalculatePercentile(recentQueryDurations, 0.99),
                RecentOperationsCount = recentMetrics.Count,
                ActiveConnections = _activeConnections,
                QueueSize = _queueSize
            };
        }
    }
    
    /// <summary>
    /// Reset all performance counters
    /// </summary>
    public void Reset()
    {
        lock (_metricsLock)
        {
            _totalWrites = 0;
            _totalQueries = 0;
            _totalWriteErrors = 0;
            _totalQueryErrors = 0;
            _totalPointsWritten = 0;
            _totalPointsQueried = 0;
            _totalWriteTime = 0;
            _totalQueryTime = 0;
            
            // Clear recent operations
            while (_recentOperations.TryDequeue(out _)) { }
        }
    }
    
    #region Private Methods
    
    /// <summary>
    /// Get recent operations within the metrics window
    /// </summary>
    /// <returns>Recent operation metrics</returns>
    private List<OperationMetric> GetRecentMetrics()
    {
        var cutoffTime = DateTimeOffset.UtcNow - _metricsWindow;
        
        return _recentOperations
            .Where(m => m.Timestamp >= cutoffTime)
            .ToList();
    }
    
    /// <summary>
    /// Remove old metrics outside the tracking window
    /// </summary>
    private void CleanupOldMetrics()
    {
        var cutoffTime = DateTimeOffset.UtcNow - _metricsWindow;
        
        while (_recentOperations.TryPeek(out var oldestMetric) && oldestMetric.Timestamp < cutoffTime)
        {
            _recentOperations.TryDequeue(out _);
        }
    }
    
    /// <summary>
    /// Calculate percentile from ordered list of values
    /// </summary>
    /// <param name="values">Ordered list of values</param>
    /// <param name="percentile">Percentile to calculate (0.0 to 1.0)</param>
    /// <returns>Percentile value</returns>
    private static double CalculatePercentile(IList<double> values, double percentile)
    {
        if (!values.Any())
            return 0;
        
        if (values.Count == 1)
            return values[0];
        
        var index = percentile * (values.Count - 1);
        var lowerIndex = (int)Math.Floor(index);
        var upperIndex = (int)Math.Ceiling(index);
        
        if (lowerIndex == upperIndex)
            return values[lowerIndex];
        
        var weight = index - lowerIndex;
        return values[lowerIndex] * (1 - weight) + values[upperIndex] * weight;
    }
    
    #endregion
}

/// <summary>
/// Individual operation metric for tracking
/// </summary>
internal sealed record OperationMetric
{
    /// <summary>
    /// Type of operation (Read, Write, Query, etc.)
    /// </summary>
    public required OperationType OperationType { get; init; }
    
    /// <summary>
    /// Timestamp when operation occurred
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }
    
    /// <summary>
    /// Duration of the operation
    /// </summary>
    public required TimeSpan Duration { get; init; }
    
    /// <summary>
    /// Number of data points processed
    /// </summary>
    public int PointsProcessed { get; init; }
    
    /// <summary>
    /// Number of data points that failed
    /// </summary>
    public int PointsFailed { get; init; }
    
    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    public bool Success { get; init; }
}

/// <summary>
/// Type of storage operation
/// </summary>
internal enum OperationType
{
    /// <summary>
    /// Write operation
    /// </summary>
    Write,
    
    /// <summary>
    /// Query/read operation
    /// </summary>
    Query,
    
    /// <summary>
    /// Delete operation
    /// </summary>
    Delete,
    
    /// <summary>
    /// Connection operation
    /// </summary>
    Connection,
    
    /// <summary>
    /// Health check operation
    /// </summary>
    HealthCheck
}

/// <summary>
/// Detailed performance statistics
/// </summary>
public sealed record PerformanceStatistics
{
    /// <summary>
    /// Total number of write operations
    /// </summary>
    public long TotalWrites { get; init; }
    
    /// <summary>
    /// Total number of query operations
    /// </summary>
    public long TotalQueries { get; init; }
    
    /// <summary>
    /// Total number of write errors
    /// </summary>
    public long TotalWriteErrors { get; init; }
    
    /// <summary>
    /// Total number of query errors
    /// </summary>
    public long TotalQueryErrors { get; init; }
    
    /// <summary>
    /// Total data points written
    /// </summary>
    public long TotalPointsWritten { get; init; }
    
    /// <summary>
    /// Total data points queried
    /// </summary>
    public long TotalPointsQueried { get; init; }
    
    /// <summary>
    /// Average write latency in milliseconds
    /// </summary>
    public double AverageWriteLatency { get; init; }
    
    /// <summary>
    /// Average query latency in milliseconds
    /// </summary>
    public double AverageQueryLatency { get; init; }
    
    /// <summary>
    /// 50th percentile write latency in milliseconds
    /// </summary>
    public double WriteLatencyP50 { get; init; }
    
    /// <summary>
    /// 95th percentile write latency in milliseconds
    /// </summary>
    public double WriteLatencyP95 { get; init; }
    
    /// <summary>
    /// 99th percentile write latency in milliseconds
    /// </summary>
    public double WriteLatencyP99 { get; init; }
    
    /// <summary>
    /// 50th percentile query latency in milliseconds
    /// </summary>
    public double QueryLatencyP50 { get; init; }
    
    /// <summary>
    /// 95th percentile query latency in milliseconds
    /// </summary>
    public double QueryLatencyP95 { get; init; }
    
    /// <summary>
    /// 99th percentile query latency in milliseconds
    /// </summary>
    public double QueryLatencyP99 { get; init; }
    
    /// <summary>
    /// Number of recent operations being tracked
    /// </summary>
    public int RecentOperationsCount { get; init; }
    
    /// <summary>
    /// Current number of active connections
    /// </summary>
    public int ActiveConnections { get; init; }
    
    /// <summary>
    /// Current queue size
    /// </summary>
    public int QueueSize { get; init; }
    
    /// <summary>
    /// Overall success rate as percentage
    /// </summary>
    public double SuccessRate => (TotalWrites + TotalQueries) > 0 
        ? (double)(TotalWrites + TotalQueries - TotalWriteErrors - TotalQueryErrors) / (TotalWrites + TotalQueries) * 100 
        : 100;
}