// Industrial.IoT.Platform.Storage - Intelligent Storage Router
// Routes data to appropriate storage backends based on classification and policies
// Implements industrial-grade patterns from existing ADAM logger codebase

using System.Collections.Concurrent;
using System.Reactive.Subjects;
using Industrial.IoT.Platform.Core.Interfaces;
using Industrial.IoT.Platform.Core.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Industrial.IoT.Platform.Storage.Services;

/// <summary>
/// Intelligent storage router that routes data to appropriate backends based on classification
/// Implements load balancing, failover, and performance optimization
/// Following industrial-grade patterns from existing ADAM logger codebase
/// </summary>
public sealed class StorageRouter : IStorageRouter, IHealthCheck, IDisposable
{
    private readonly ILogger<StorageRouter> _logger;
    private readonly ConcurrentDictionary<string, IStorageRepository> _repositories = new();
    private readonly ConcurrentDictionary<DataClassification, RoutingPolicy> _routingPolicies = new();
    private readonly Subject<StorageFailureEventArgs> _failureSubject = new();
    private readonly Subject<StorageMetricsEventArgs> _metricsSubject = new();
    private readonly Timer _healthCheckTimer;
    private readonly Timer _metricsTimer;
    
    private volatile bool _isDisposed;
    private readonly object _routingLock = new();

    /// <summary>
    /// Initialize storage router with default routing policies
    /// </summary>
    /// <param name="logger">Logger for diagnostics and monitoring</param>
    public StorageRouter(ILogger<StorageRouter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Initialize default routing policies
        InitializeDefaultRoutingPolicies();
        
        // Start background health monitoring
        _healthCheckTimer = new Timer(CheckRepositoryHealth, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        _metricsTimer = new Timer(CollectMetrics, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        
        _logger.LogInformation("Storage router initialized with intelligent data routing");
    }

    /// <summary>
    /// Event raised when storage routing fails
    /// </summary>
    public event EventHandler<StorageFailureEventArgs>? StorageFailure;

    /// <summary>
    /// Event raised when storage performance metrics are updated
    /// </summary>
    public event EventHandler<StorageMetricsEventArgs>? MetricsUpdated;

    /// <summary>
    /// Route data reading to appropriate storage backend(s)
    /// </summary>
    /// <param name="dataReading">Data reading to store</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Routing result with storage outcomes</returns>
    public async Task<StorageRoutingResult> RouteDataAsync(IDataReading dataReading, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (dataReading == null)
            throw new ArgumentNullException(nameof(dataReading));

        var startTime = DateTimeOffset.UtcNow;
        var classification = ClassifyData(dataReading);
        
        try
        {
            var policy = GetRoutingPolicy(classification);
            var targetRepositories = SelectRepositories(classification, policy);
            
            if (!targetRepositories.Any())
            {
                var error = $"No available repositories for data classification: {classification}";
                _logger.LogWarning(error);
                
                await NotifyFailure("StorageRouter", "RouteData", error, dataReading);
                
                return new StorageRoutingResult
                {
                    Success = false,
                    DataReading = dataReading,
                    TotalDuration = DateTimeOffset.UtcNow - startTime,
                    ErrorMessage = error
                };
            }

            var results = new Dictionary<string, StorageWriteResult>();
            var successfulBackends = new List<string>();

            // Route to primary repository
            var primaryResult = await RouteToRepository(targetRepositories.First(), dataReading, cancellationToken);
            results[primaryResult.StorageBackend] = primaryResult;
            
            if (primaryResult.Success)
            {
                successfulBackends.Add(primaryResult.StorageBackend);
            }
            else if (targetRepositories.Count > 1)
            {
                // Failover to secondary repositories
                _logger.LogWarning("Primary storage failed for {Classification}, attempting failover", classification);
                
                foreach (var fallbackRepo in targetRepositories.Skip(1))
                {
                    var fallbackResult = await RouteToRepository(fallbackRepo, dataReading, cancellationToken);
                    results[fallbackResult.StorageBackend] = fallbackResult;
                    
                    if (fallbackResult.Success)
                    {
                        successfulBackends.Add(fallbackResult.StorageBackend);
                        break; // Stop on first successful fallback
                    }
                }
            }

            var success = successfulBackends.Any();
            var duration = DateTimeOffset.UtcNow - startTime;

            if (!success)
            {
                var error = $"All storage repositories failed for classification: {classification}";
                await NotifyFailure("StorageRouter", "RouteData", error, dataReading);
            }

            return new StorageRoutingResult
            {
                Success = success,
                DataReading = dataReading,
                StorageBackends = successfulBackends,
                BackendResults = results,
                TotalDuration = duration,
                ErrorMessage = success ? null : "All storage repositories failed"
            };
        }
        catch (Exception ex)
        {
            var duration = DateTimeOffset.UtcNow - startTime;
            _logger.LogError(ex, "Unexpected error routing data for classification: {Classification}", classification);
            
            await NotifyFailure("StorageRouter", "RouteData", ex.Message, dataReading, ex);

            return new StorageRoutingResult
            {
                Success = false,
                DataReading = dataReading,
                TotalDuration = duration,
                ErrorMessage = $"Routing error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Route multiple data readings in batch for optimal performance
    /// </summary>
    /// <param name="dataReadings">Data readings to store</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Batch routing result</returns>
    public async Task<BatchStorageRoutingResult> RouteDataBatchAsync(IReadOnlyList<IDataReading> dataReadings, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (dataReadings == null || !dataReadings.Any())
        {
            return new BatchStorageRoutingResult
            {
                Success = true,
                TotalReadings = 0,
                SuccessfulReadings = 0
            };
        }

        var startTime = DateTimeOffset.UtcNow;
        var individualResults = new List<StorageRoutingResult>();
        var backendResults = new Dictionary<string, BatchStorageResult>();
        var errorMessages = new List<string>();

        try
        {
            // Group readings by classification for efficient batch processing
            var groupedReadings = dataReadings.GroupBy(ClassifyData).ToList();
            
            foreach (var group in groupedReadings)
            {
                var classification = group.Key;
                var readings = group.ToList();
                
                _logger.LogDebug("Processing batch of {Count} readings for classification: {Classification}", 
                    readings.Count, classification);

                // Route each classification group
                foreach (var reading in readings)
                {
                    var result = await RouteDataAsync(reading, cancellationToken);
                    individualResults.Add(result);
                    
                    if (!result.Success)
                    {
                        errorMessages.Add($"Failed to route {reading.DeviceId}: {result.ErrorMessage}");
                    }

                    // Aggregate backend results
                    foreach (var (backend, backendResult) in result.BackendResults)
                    {
                        if (!backendResults.ContainsKey(backend))
                        {
                            backendResults[backend] = new BatchStorageResult
                            {
                                StorageBackend = backend,
                                BatchSize = 0,
                                SuccessfulWrites = 0,
                                Duration = TimeSpan.Zero
                            };
                        }

                        var current = backendResults[backend];
                        backendResults[backend] = current with
                        {
                            BatchSize = current.BatchSize + 1,
                            SuccessfulWrites = current.SuccessfulWrites + (backendResult.Success ? backendResult.RecordsWritten : 0),
                            Duration = current.Duration + backendResult.Duration
                        };
                    }
                }
            }

            var successfulReadings = individualResults.Count(r => r.Success);
            var totalDuration = DateTimeOffset.UtcNow - startTime;

            return new BatchStorageRoutingResult
            {
                Success = successfulReadings > 0,
                TotalReadings = dataReadings.Count,
                SuccessfulReadings = successfulReadings,
                BackendResults = backendResults,
                TotalDuration = totalDuration,
                IndividualResults = individualResults,
                ErrorMessages = errorMessages
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing batch of {Count} readings", dataReadings.Count);
            
            return new BatchStorageRoutingResult
            {
                Success = false,
                TotalReadings = dataReadings.Count,
                SuccessfulReadings = 0,
                TotalDuration = DateTimeOffset.UtcNow - startTime,
                ErrorMessages = new[] { $"Batch processing error: {ex.Message}" }
            };
        }
    }

    /// <summary>
    /// Determine appropriate storage backend(s) for data classification
    /// </summary>
    /// <param name="dataClassification">Data classification</param>
    /// <param name="storagePolicy">Storage policy requirements</param>
    /// <returns>Recommended storage backends</returns>
    public async Task<StorageRecommendation> GetStorageRecommendationAsync(DataClassification dataClassification, StoragePolicy storagePolicy)
    {
        ThrowIfDisposed();

        try
        {
            var policy = GetRoutingPolicy(dataClassification);
            var availableRepositories = await GetHealthyRepositories(dataClassification);
            
            if (!availableRepositories.Any())
            {
                return new StorageRecommendation
                {
                    PrimaryStorage = "None",
                    ConfidenceScore = 0,
                    Reasoning = $"No healthy repositories available for {dataClassification}"
                };
            }

            // Select best repository based on performance and policy requirements
            var primaryRepo = availableRepositories
                .OrderByDescending(r => CalculateRepositoryScore(r, storagePolicy))
                .First();

            var secondaryRepos = availableRepositories
                .Where(r => r != primaryRepo)
                .Take(2)
                .Select(r => r.StorageType)
                .ToList();

            var confidence = CalculateConfidenceScore(primaryRepo, storagePolicy, availableRepositories.Count);

            return new StorageRecommendation
            {
                PrimaryStorage = primaryRepo.StorageType,
                SecondaryStorage = secondaryRepos,
                ConfidenceScore = confidence,
                Reasoning = $"Selected based on {dataClassification} classification, performance metrics, and policy requirements",
                Performance = await EstimatePerformance(primaryRepo, storagePolicy)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating storage recommendation for {Classification}", dataClassification);
            
            return new StorageRecommendation
            {
                PrimaryStorage = "Error",
                ConfidenceScore = 0,
                Reasoning = $"Error generating recommendation: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Register a storage repository with the router
    /// </summary>
    /// <param name="repository">Storage repository to register</param>
    /// <param name="classification">Data classifications this repository handles</param>
    /// <param name="priority">Priority for this repository (higher = preferred)</param>
    /// <returns>Task that completes when repository is registered</returns>
    public async Task RegisterRepositoryAsync(IStorageRepository repository, DataClassification classification, int priority = 0)
    {
        ThrowIfDisposed();
        
        if (repository == null)
            throw new ArgumentNullException(nameof(repository));

        lock (_routingLock)
        {
            _repositories[repository.StorageType] = repository;
            
            if (!_routingPolicies.ContainsKey(classification))
            {
                _routingPolicies[classification] = new RoutingPolicy
                {
                    DataClassification = classification,
                    Repositories = new List<RepositoryConfig>()
                };
            }

            var policy = _routingPolicies[classification];
            var repositories = policy.Repositories.ToList();
            
            // Remove existing entry for this storage type
            repositories.RemoveAll(r => r.StorageType == repository.StorageType);
            
            // Add new entry
            repositories.Add(new RepositoryConfig
            {
                StorageType = repository.StorageType,
                Priority = priority,
                IsEnabled = true
            });
            
            // Sort by priority (descending)
            repositories.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            
            _routingPolicies[classification] = policy with { Repositories = repositories };
        }

        _logger.LogInformation("Registered storage repository {StorageType} for classification {Classification} with priority {Priority}", 
            repository.StorageType, classification, priority);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Unregister a storage repository from the router
    /// </summary>
    /// <param name="storageType">Storage type to unregister</param>
    /// <param name="classification">Data classification to unregister for</param>
    /// <returns>Task that completes when repository is unregistered</returns>
    public async Task UnregisterRepositoryAsync(string storageType, DataClassification classification)
    {
        ThrowIfDisposed();

        lock (_routingLock)
        {
            if (_routingPolicies.TryGetValue(classification, out var policy))
            {
                var repositories = policy.Repositories.Where(r => r.StorageType != storageType).ToList();
                _routingPolicies[classification] = policy with { Repositories = repositories };
            }

            if (!_routingPolicies.Values.Any(p => p.Repositories.Any(r => r.StorageType == storageType)))
            {
                _repositories.TryRemove(storageType, out _);
            }
        }

        _logger.LogInformation("Unregistered storage repository {StorageType} for classification {Classification}", 
            storageType, classification);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Get health status of all registered storage repositories
    /// </summary>
    /// <returns>Health status for each storage backend</returns>
    public async Task<IReadOnlyDictionary<string, StorageHealth>> GetStorageHealthAsync()
    {
        ThrowIfDisposed();

        var healthResults = new Dictionary<string, StorageHealth>();

        foreach (var (storageType, repository) in _repositories)
        {
            try
            {
                // Test connectivity using the available interface methods
                var isConnected = repository.IsConnected;
                var testResult = await repository.TestConnectivityAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
                
                healthResults[storageType] = new StorageHealth
                {
                    StorageBackend = storageType,
                    IsHealthy = testResult.Success && isConnected,
                    LastChecked = DateTimeOffset.UtcNow,
                    IsConnected = isConnected,
                    LastError = testResult.Success ? null : testResult.ErrorMessage,
                    Diagnostics = new Dictionary<string, object>
                    {
                        ["TestDuration"] = testResult.Duration.TotalMilliseconds,
                        ["Latency"] = testResult.Latency?.TotalMilliseconds ?? 0,
                        ["Success"] = testResult.Success
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check health for repository {StorageType}", storageType);
                
                healthResults[storageType] = new StorageHealth
                {
                    StorageBackend = storageType,
                    IsHealthy = false,
                    LastChecked = DateTimeOffset.UtcNow,
                    IsConnected = false,
                    LastError = ex.Message,
                    Diagnostics = new Dictionary<string, object>
                    {
                        ["Exception"] = ex.Message,
                        ["ExceptionType"] = ex.GetType().Name
                    }
                };
            }
        }

        return healthResults;
    }

    /// <summary>
    /// Check health of the storage router
    /// </summary>
    /// <param name="context">Health check context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health check result</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            var repositoryHealth = await GetStorageHealthAsync();
            var healthyCount = repositoryHealth.Values.Count(h => h.IsHealthy);
            var totalCount = repositoryHealth.Count;

            if (totalCount == 0)
            {
                return HealthCheckResult.Unhealthy("No storage repositories registered");
            }

            if (healthyCount == 0)
            {
                return HealthCheckResult.Unhealthy("All storage repositories are unhealthy");
            }

            if (healthyCount < totalCount)
            {
                return HealthCheckResult.Degraded($"{healthyCount}/{totalCount} repositories healthy");
            }

            return HealthCheckResult.Healthy($"All {totalCount} repositories healthy");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Error checking storage router health", ex);
        }
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        _healthCheckTimer?.Dispose();
        _metricsTimer?.Dispose();
        _failureSubject?.Dispose();
        _metricsSubject?.Dispose();

        foreach (var repository in _repositories.Values)
        {
            if (repository is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing repository {StorageType}", repository.StorageType);
                }
            }
        }

        _repositories.Clear();
        _routingPolicies.Clear();

        _logger.LogInformation("Storage router disposed");
    }

    #region Private Methods

    /// <summary>
    /// Classify data reading to determine appropriate storage
    /// </summary>
    /// <param name="dataReading">Data reading to classify</param>
    /// <returns>Data classification</returns>
    private DataClassification ClassifyData(IDataReading dataReading)
    {
        // Scale data (ADAM-4571) goes to SQL Server for relational analysis
        if (dataReading is IScaleDataReading)
        {
            return DataClassification.DiscreteReading;
        }

        // Counter data (ADAM-6051) goes to InfluxDB for time-series analysis
        if (dataReading.Tags.ContainsKey("device_type") && 
            dataReading.Tags["device_type"].ToString()?.Contains("6051") == true)
        {
            return DataClassification.TimeSeries;
        }

        // Configuration data goes to SQL Server
        if (dataReading.Tags.ContainsKey("data_type") && 
            dataReading.Tags["data_type"].ToString() == "configuration")
        {
            return DataClassification.Configuration;
        }

        // Default to time-series for unknown data types
        return DataClassification.TimeSeries;
    }

    /// <summary>
    /// Get routing policy for data classification
    /// </summary>
    /// <param name="classification">Data classification</param>
    /// <returns>Routing policy</returns>
    private RoutingPolicy GetRoutingPolicy(DataClassification classification)
    {
        return _routingPolicies.GetValueOrDefault(classification, new RoutingPolicy
        {
            DataClassification = classification,
            Repositories = new List<RepositoryConfig>()
        });
    }

    /// <summary>
    /// Select repositories for data classification and policy
    /// </summary>
    /// <param name="classification">Data classification</param>
    /// <param name="policy">Routing policy</param>
    /// <returns>Ordered list of repositories to try</returns>
    private List<IStorageRepository> SelectRepositories(DataClassification classification, RoutingPolicy policy)
    {
        var availableRepos = new List<IStorageRepository>();

        foreach (var repoConfig in policy.Repositories.Where(r => r.IsEnabled))
        {
            if (_repositories.TryGetValue(repoConfig.StorageType, out var repository) && 
                repository.IsConnected)
            {
                availableRepos.Add(repository);
            }
        }

        return availableRepos;
    }

    /// <summary>
    /// Route data to specific repository
    /// </summary>
    /// <param name="repository">Target repository</param>
    /// <param name="dataReading">Data to store</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Storage write result</returns>
    private async Task<StorageWriteResult> RouteToRepository(IStorageRepository repository, IDataReading dataReading, CancellationToken cancellationToken)
    {
        try
        {
            if (repository is ITimeSeriesRepository timeSeriesRepo)
            {
                return await timeSeriesRepo.WriteTimeSeriesAsync(new[] { dataReading }, cancellationToken);
            }
            else if (repository is ITransactionalRepository transactionalRepo && dataReading is IScaleDataReading scaleReading)
            {
                // Use the correct method name from the interface
                var result = await transactionalRepo.ExecuteTransactionAsync(new[]
                {
                    new StorageOperation
                    {
                        OperationType = "INSERT",
                        Target = "ScaleData",
                        Data = new Dictionary<string, object>
                        {
                            ["DeviceId"] = scaleReading.DeviceId,
                            ["WeightValue"] = scaleReading.WeightValue,
                            ["Unit"] = scaleReading.Unit,
                            ["Timestamp"] = scaleReading.Timestamp
                        }
                    }
                }, cancellationToken);
                
                return new StorageWriteResult
                {
                    Success = result.Success,
                    RecordsWritten = result.OperationsExecuted,
                    StorageBackend = repository.StorageType,
                    Duration = result.ExecutionTime,
                    ErrorMessage = result.ErrorMessage
                };
            }
            else
            {
                return new StorageWriteResult
                {
                    Success = false,
                    StorageBackend = repository.StorageType,
                    ErrorMessage = "Incompatible repository type for data reading"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error routing data to repository {StorageType}", repository.StorageType);
            
            return new StorageWriteResult
            {
                Success = false,
                StorageBackend = repository.StorageType,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Get healthy repositories for data classification
    /// </summary>
    /// <param name="classification">Data classification</param>
    /// <returns>List of healthy repositories</returns>
    private async Task<List<IStorageRepository>> GetHealthyRepositories(DataClassification classification)
    {
        var healthStatus = await GetStorageHealthAsync();
        var policy = GetRoutingPolicy(classification);
        
        return policy.Repositories
            .Where(r => r.IsEnabled && 
                       _repositories.ContainsKey(r.StorageType) && 
                       healthStatus.GetValueOrDefault(r.StorageType)?.IsHealthy == true)
            .Select(r => _repositories[r.StorageType])
            .ToList();
    }

    /// <summary>
    /// Calculate repository score for selection
    /// </summary>
    /// <param name="repository">Repository to score</param>
    /// <param name="policy">Storage policy requirements</param>
    /// <returns>Repository score</returns>
    private double CalculateRepositoryScore(IStorageRepository repository, StoragePolicy policy)
    {
        double score = 0;

        // Prefer repositories that match the policy's primary backend
        if (repository.StorageType == policy.PrimaryBackend)
            score += 100;

        // Add points for connectivity
        if (repository.IsConnected)
            score += 50;

        // Add random factor for load balancing
        score += Random.Shared.NextDouble() * 10;

        return score;
    }

    /// <summary>
    /// Calculate confidence score for storage recommendation
    /// </summary>
    /// <param name="repository">Selected repository</param>
    /// <param name="policy">Storage policy</param>
    /// <param name="availableCount">Number of available repositories</param>
    /// <returns>Confidence score (0-100)</returns>
    private double CalculateConfidenceScore(IStorageRepository repository, StoragePolicy policy, int availableCount)
    {
        double confidence = 50; // Base confidence

        if (repository.StorageType == policy.PrimaryBackend)
            confidence += 30;

        if (repository.IsConnected)
            confidence += 15;

        if (availableCount > 1)
            confidence += 5; // Fallback options available

        return Math.Min(100, confidence);
    }

    /// <summary>
    /// Estimate performance characteristics for repository
    /// </summary>
    /// <param name="repository">Repository to analyze</param>
    /// <param name="policy">Storage policy</param>
    /// <returns>Performance expectation</returns>
    private async Task<StoragePerformanceExpectation> EstimatePerformance(IStorageRepository repository, StoragePolicy policy)
    {
        // Performance estimates based on storage type
        return repository.StorageType switch
        {
            "InfluxDB" => new StoragePerformanceExpectation
            {
                ExpectedWriteLatency = 10,
                ExpectedQueryLatency = 50,
                ExpectedThroughput = 10000,
                ExpectedCompressionRatio = 3.0
            },
            "SQLServer" => new StoragePerformanceExpectation
            {
                ExpectedWriteLatency = 25,
                ExpectedQueryLatency = 100,
                ExpectedThroughput = 1000,
                ExpectedCompressionRatio = 1.5
            },
            _ => new StoragePerformanceExpectation
            {
                ExpectedWriteLatency = 100,
                ExpectedQueryLatency = 500,
                ExpectedThroughput = 100,
                ExpectedCompressionRatio = 1.0
            }
        };
    }

    /// <summary>
    /// Initialize default routing policies for known data classifications
    /// </summary>
    private void InitializeDefaultRoutingPolicies()
    {
        // Time-series data (ADAM-6051 counters) -> InfluxDB
        _routingPolicies[DataClassification.TimeSeries] = new RoutingPolicy
        {
            DataClassification = DataClassification.TimeSeries,
            Repositories = new List<RepositoryConfig>
            {
                new() { StorageType = "InfluxDB", Priority = 100, IsEnabled = true }
            }
        };

        // Discrete readings (ADAM-4571 scales) -> SQL Server
        _routingPolicies[DataClassification.DiscreteReading] = new RoutingPolicy
        {
            DataClassification = DataClassification.DiscreteReading,
            Repositories = new List<RepositoryConfig>
            {
                new() { StorageType = "SQLServer", Priority = 100, IsEnabled = true }
            }
        };

        // Configuration data -> SQL Server
        _routingPolicies[DataClassification.Configuration] = new RoutingPolicy
        {
            DataClassification = DataClassification.Configuration,
            Repositories = new List<RepositoryConfig>
            {
                new() { StorageType = "SQLServer", Priority = 100, IsEnabled = true }
            }
        };

        // Protocol templates -> SQL Server
        _routingPolicies[DataClassification.ProtocolTemplate] = new RoutingPolicy
        {
            DataClassification = DataClassification.ProtocolTemplate,
            Repositories = new List<RepositoryConfig>
            {
                new() { StorageType = "SQLServer", Priority = 100, IsEnabled = true }
            }
        };
    }

    /// <summary>
    /// Notify about storage failures
    /// </summary>
    private async Task NotifyFailure(string storageBackend, string operationType, string errorMessage, object? failedData = null, Exception? exception = null)
    {
        var args = new StorageFailureEventArgs
        {
            StorageBackend = storageBackend,
            OperationType = operationType,
            ErrorMessage = errorMessage,
            Exception = exception,
            FailedData = failedData
        };

        _failureSubject.OnNext(args);
        StorageFailure?.Invoke(this, args);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Background health check for repositories
    /// </summary>
    private async void CheckRepositoryHealth(object? state)
    {
        if (_isDisposed)
            return;

        try
        {
            var healthResults = await GetStorageHealthAsync();
            var unhealthyRepos = healthResults.Where(kvp => !kvp.Value.IsHealthy).ToList();

            if (unhealthyRepos.Any())
            {
                _logger.LogWarning("Found {Count} unhealthy repositories: {Repositories}",
                    unhealthyRepos.Count,
                    string.Join(", ", unhealthyRepos.Select(r => r.Key)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during background health check");
        }
    }

    /// <summary>
    /// Collect and publish metrics
    /// </summary>
    private async void CollectMetrics(object? state)
    {
        if (_isDisposed)
            return;

        try
        {
            foreach (var (storageType, repository) in _repositories)
            {
                var args = new StorageMetricsEventArgs
                {
                    StorageBackend = storageType,
                    Metrics = new StorageMetrics
                    {
                        ActiveConnections = repository.IsConnected ? 1 : 0,
                        // Add more metrics collection as needed
                    }
                };

                _metricsSubject.OnNext(args);
                MetricsUpdated?.Invoke(this, args);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting storage metrics");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Throw if disposed
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(StorageRouter));
    }

    #endregion
}

/// <summary>
/// Routing policy for data classification
/// </summary>
internal record RoutingPolicy
{
    public required DataClassification DataClassification { get; init; }
    public required IReadOnlyList<RepositoryConfig> Repositories { get; init; }
}

/// <summary>
/// Repository configuration for routing
/// </summary>
internal record RepositoryConfig
{
    public required string StorageType { get; init; }
    public required int Priority { get; init; }
    public required bool IsEnabled { get; init; }
}