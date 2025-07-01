// Industrial.IoT.Platform.Storage - SQL Server Transactional Repository
// High-performance transactional data storage for discrete scale data and configurations
// Implements industrial-grade patterns from existing ADAM logger codebase

using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Industrial.IoT.Platform.Core.Interfaces;
using Industrial.IoT.Platform.Core.Models;
using Industrial.IoT.Platform.Storage.Configuration;
using Industrial.IoT.Platform.Storage.Data;
using Industrial.IoT.Platform.Storage.Data.Entities;
using Industrial.IoT.Platform.Storage.Infrastructure;
using System.Reactive.Subjects;
using System.Reactive.Linq;

namespace Industrial.IoT.Platform.Storage.Repositories;

/// <summary>
/// SQL Server-based transactional repository implementation
/// Provides ACID-compliant storage for discrete scale data, configurations, and protocol templates
/// Following industrial-grade patterns from existing ADAM logger codebase
/// </summary>
public sealed class SqlServerTransactionalRepository : ITransactionalRepository, IConfigurationRepository, IHealthCheck, IDisposable
{
    private readonly ILogger<SqlServerTransactionalRepository> _logger;
    private readonly SqlServerConfiguration _configuration;
    private readonly Subject<StorageMetricsEventArgs> _metricsSubject = new();
    private readonly Subject<StorageFailureEventArgs> _failureSubject = new();
    
    private IndustrialIoTDbContext? _dbContext;
    private volatile bool _isConnected;
    private volatile bool _isDisposed;
    private readonly object _connectionLock = new();
    
    // Performance tracking
    private readonly PerformanceTracker _performanceTracker = new();
    
    /// <summary>
    /// Storage type identifier
    /// </summary>
    public string StorageType => "SQLServer";
    
    /// <summary>
    /// Whether the repository is connected and ready for operations
    /// </summary>
    public bool IsConnected => _isConnected && _dbContext != null;
    
    /// <summary>
    /// Observable stream of storage metrics updates
    /// </summary>
    public IObservable<StorageMetricsEventArgs> MetricsStream => _metricsSubject.AsObservable();
    
    /// <summary>
    /// Observable stream of storage failure events
    /// </summary>
    public IObservable<StorageFailureEventArgs> FailureStream => _failureSubject.AsObservable();
    
    /// <summary>
    /// Initialize SQL Server transactional repository
    /// </summary>
    /// <param name="configuration">SQL Server configuration options</param>
    /// <param name="logger">Logger for diagnostics and monitoring</param>
    public SqlServerTransactionalRepository(
        IOptions<SqlServerConfiguration> configuration,
        ILogger<SqlServerTransactionalRepository> logger)
    {
        _configuration = configuration?.Value ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        ValidateConfiguration();
        _logger.LogInformation("SQL Server transactional repository initialized for database: {Database}", _configuration.Database);
    }
    
    /// <summary>
    /// Connect to SQL Server backend
    /// </summary>
    /// <param name="connectionString">SQL Server connection string (optional override)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if connection was successful</returns>
    public async Task<bool> ConnectAsync(string? connectionString = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (_isConnected && _dbContext != null)
        {
            _logger.LogDebug("Already connected to SQL Server");
            return true;
        }
        
        lock (_connectionLock)
        {
            try
            {
                _logger.LogInformation("Connecting to SQL Server database: {Database}", _configuration.Database);
                
                var options = new DbContextOptionsBuilder<IndustrialIoTDbContext>()
                    .UseSqlServer(connectionString ?? _configuration.GetConnectionString(), sqlOptions =>
                    {
                        sqlOptions.CommandTimeout(_configuration.CommandTimeoutSeconds);
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: _configuration.MaxRetries,
                            maxRetryDelay: TimeSpan.FromSeconds(_configuration.RetryDelaySeconds),
                            errorNumbersToAdd: null);
                    })
                    .EnableSensitiveDataLogging(_configuration.EnableSensitiveDataLogging)
                    .EnableDetailedErrors(_configuration.EnableDetailedErrors)
                    .Options;
                
                _dbContext = new IndustrialIoTDbContext(options);
                
                // Test connection
                _dbContext.Database.CanConnect();
                
                _isConnected = true;
                _logger.LogInformation("Successfully connected to SQL Server");
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to SQL Server");
                _ = PublishFailureAsync("Connection", ex.Message, ex);
                return false;
            }
        }
    }
    
    /// <summary>
    /// Disconnect from SQL Server backend
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when disconnected</returns>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (!_isConnected || _dbContext == null)
            return;
        
        lock (_connectionLock)
        {
            try
            {
                _dbContext?.Dispose();
                _dbContext = null;
                _isConnected = false;
                
                _logger.LogInformation("Disconnected from SQL Server");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during SQL Server disconnection");
            }
        }
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Test connectivity to SQL Server
    /// </summary>
    /// <param name="timeout">Test timeout</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Connectivity test result</returns>
    public async Task<StorageTestResult> TestConnectivityAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var startTime = DateTime.UtcNow;
        
        try
        {
            using var timeoutCts = new CancellationTokenSource(timeout);
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            
            if (!IsConnected)
            {
                var connected = await ConnectAsync(cancellationToken: combinedCts.Token);
                if (!connected)
                {
                    return new StorageTestResult
                    {
                        Success = false,
                        StorageBackend = StorageType,
                        Duration = DateTime.UtcNow - startTime,
                        ErrorMessage = "Failed to establish connection"
                    };
                }
            }
            
            // Test with a simple query
            var testStartTime = DateTime.UtcNow;
            await _dbContext!.Database.ExecuteSqlRawAsync("SELECT 1", combinedCts.Token);
            var latency = DateTime.UtcNow - testStartTime;
            
            return new StorageTestResult
            {
                Success = true,
                StorageBackend = StorageType,
                Duration = DateTime.UtcNow - startTime,
                Latency = latency,
                Diagnostics = new Dictionary<string, object>
                {
                    ["QueryLatency"] = latency.TotalMilliseconds,
                    ["Database"] = _configuration.Database,
                    ["Server"] = _configuration.Server
                }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new StorageTestResult
            {
                Success = false,
                StorageBackend = StorageType,
                Duration = DateTime.UtcNow - startTime,
                ErrorMessage = "Test was cancelled"
            };
        }
        catch (OperationCanceledException)
        {
            return new StorageTestResult
            {
                Success = false,
                StorageBackend = StorageType,
                Duration = DateTime.UtcNow - startTime,
                ErrorMessage = "Test timed out"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SQL Server connectivity test failed");
            return new StorageTestResult
            {
                Success = false,
                StorageBackend = StorageType,
                Duration = DateTime.UtcNow - startTime,
                ErrorMessage = ex.Message
            };
        }
    }
    
    /// <summary>
    /// Get health status of SQL Server backend
    /// </summary>
    /// <returns>Storage health information</returns>
    public async Task<StorageHealth> GetHealthAsync()
    {
        ThrowIfDisposed();
        var startTime = DateTimeOffset.UtcNow;
        
        try
        {
            if (!IsConnected)
            {
                return new StorageHealth
                {
                    StorageBackend = StorageType,
                    IsHealthy = false,
                    IsConnected = false,
                    LastChecked = startTime,
                    LastError = "Not connected to SQL Server",
                    Diagnostics = new Dictionary<string, object>
                    {
                        ["ConnectionStatus"] = "Disconnected"
                    }
                };
            }
            
            // Test basic connectivity and get database info
            var testResult = await TestConnectivityAsync(TimeSpan.FromSeconds(5));
            var metrics = _performanceTracker.GetCurrentMetrics();
            
            // Get database size and capacity if possible
            StorageCapacity? capacity = null;
            try
            {
                capacity = await GetDatabaseCapacityAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get database capacity information");
            }
            
            return new StorageHealth
            {
                StorageBackend = StorageType,
                IsHealthy = testResult.Success && metrics.ErrorRate < 10.0, // Healthy if error rate < 10%
                IsConnected = IsConnected,
                LastChecked = startTime,
                AverageResponseTime = testResult.Latency?.TotalMilliseconds ?? 0,
                UtilizationPercentage = CalculateUtilization(capacity),
                Capacity = capacity,
                LastError = testResult.Success ? null : testResult.ErrorMessage,
                Diagnostics = new Dictionary<string, object>
                {
                    ["WriteLatency"] = metrics.AverageWriteLatency,
                    ["QueryLatency"] = metrics.AverageQueryLatency,
                    ["Throughput"] = metrics.Throughput,
                    ["ErrorRate"] = metrics.ErrorRate,
                    ["ActiveConnections"] = metrics.ActiveConnections,
                    ["QueueSize"] = metrics.QueueSize,
                    ["Database"] = _configuration.Database,
                    ["Server"] = _configuration.Server
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get SQL Server health status");
            return new StorageHealth
            {
                StorageBackend = StorageType,
                IsHealthy = false,
                IsConnected = IsConnected,
                LastChecked = startTime,
                LastError = ex.Message
            };
        }
    }
    
    /// <summary>
    /// Write discrete data records with transaction support
    /// </summary>
    /// <param name="records">Data records to write</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Write operation result</returns>
    public async Task<StorageWriteResult> WriteRecordsAsync(IReadOnlyList<IDataReading> records, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (!records.Any())
        {
            return new StorageWriteResult
            {
                Success = true,
                RecordsWritten = 0,
                StorageBackend = StorageType,
                Duration = TimeSpan.Zero
            };
        }
        
        var startTime = DateTime.UtcNow;
        
        try
        {
            if (!IsConnected)
            {
                var connected = await ConnectAsync(cancellationToken: cancellationToken);
                if (!connected)
                    throw new InvalidOperationException("Failed to connect to SQL Server");
            }
            
            using var transaction = await _dbContext!.Database.BeginTransactionAsync(cancellationToken);
            
            try
            {
                var scaleReadings = new List<ScaleDataEntity>();
                
                foreach (var record in records)
                {
                    // Convert to entity based on record type
                    if (record is IScaleDataReading scaleReading)
                    {
                        var entity = ConvertToScaleEntity(scaleReading);
                        scaleReadings.Add(entity);
                    }
                }
                
                // Bulk insert for better performance
                if (scaleReadings.Any())
                {
                    _dbContext.ScaleData.AddRange(scaleReadings);
                }
                
                var recordsWritten = await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                
                var duration = DateTime.UtcNow - startTime;
                _performanceTracker.RecordWrite(recordsWritten, duration);
                
                // Publish metrics
                await PublishMetricsAsync();
                
                return new StorageWriteResult
                {
                    Success = true,
                    RecordsWritten = recordsWritten,
                    Duration = duration,
                    StorageBackend = StorageType,
                    Metadata = new Dictionary<string, object>
                    {
                        ["TransactionId"] = transaction.TransactionId,
                        ["AverageLatency"] = duration.TotalMilliseconds / Math.Max(1, recordsWritten)
                    }
                };
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _performanceTracker.RecordWrite(0, duration, records.Count);
            
            _logger.LogError(ex, "Failed to write {Count} records to SQL Server", records.Count);
            await PublishFailureAsync("WriteRecords", ex.Message, ex, records);
            
            return new StorageWriteResult
            {
                Success = false,
                RecordsWritten = 0,
                RecordsFailed = records.Count,
                Duration = duration,
                StorageBackend = StorageType,
                ErrorMessage = ex.Message
            };
        }
    }
    
    /// <summary>
    /// Query records with complex filtering and joins
    /// </summary>
    /// <param name="query">Relational query parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Query results</returns>
    public async Task<RelationalQueryResult> QueryRecordsAsync(RelationalQuery query, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to SQL Server");
        
        var startTime = DateTime.UtcNow;
        
        try
        {
            var results = new List<IReadOnlyDictionary<string, object>>();
            
            // Build and execute query based on table name
            switch (query.TableName.ToLowerInvariant())
            {
                case "scaledata":
                    results = await QueryScaleDataAsync(query, cancellationToken);
                    break;
                
                case "protocoltemplates":
                    results = await QueryProtocolTemplatesAsync(query, cancellationToken);
                    break;
                
                case "deviceconfigurations":
                    results = await QueryDeviceConfigurationsAsync(query, cancellationToken);
                    break;
                
                default:
                    throw new ArgumentException($"Unknown table name: {query.TableName}");
            }
            
            var duration = DateTime.UtcNow - startTime;
            _performanceTracker.RecordQuery(results.Count, duration);
            
            await PublishMetricsAsync();
            
            return new RelationalQueryResult
            {
                Success = true,
                Records = results,
                TotalRecords = results.Count,
                ExecutionTime = duration
            };
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _performanceTracker.RecordQuery(0, duration, failed: true);
            
            _logger.LogError(ex, "Failed to execute SQL Server query for table {TableName}", query.TableName);
            await PublishFailureAsync("QueryRecords", ex.Message, ex, query);
            
            return new RelationalQueryResult
            {
                Success = false,
                ExecutionTime = duration,
                ErrorMessage = ex.Message
            };
        }
    }
    
    /// <summary>
    /// Execute a transaction with multiple operations
    /// </summary>
    /// <param name="operations">Operations to execute in transaction</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Transaction result</returns>
    public async Task<TransactionResult> ExecuteTransactionAsync(IReadOnlyList<StorageOperation> operations, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to SQL Server");
        
        var startTime = DateTime.UtcNow;
        
        using var transaction = await _dbContext!.Database.BeginTransactionAsync(cancellationToken);
        
        try
        {
            var operationResults = new List<StorageWriteResult>();
            
            foreach (var operation in operations)
            {
                var result = await ExecuteStorageOperationAsync(operation, cancellationToken);
                operationResults.Add(result);
                
                if (!result.Success)
                {
                    throw new InvalidOperationException($"Operation {operation.OperationType} failed: {result.ErrorMessage}");
                }
            }
            
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            
            return new TransactionResult
            {
                Success = true,
                OperationsExecuted = operations.Count,
                OperationResults = operationResults,
                ExecutionTime = DateTime.UtcNow - startTime,
                TransactionId = transaction.TransactionId.ToString()
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            
            _logger.LogError(ex, "Transaction failed with {OperationCount} operations", operations.Count);
            await PublishFailureAsync("ExecuteTransaction", ex.Message, ex, operations);
            
            return new TransactionResult
            {
                Success = false,
                OperationsExecuted = 0,
                ExecutionTime = DateTime.UtcNow - startTime,
                ErrorMessage = ex.Message
            };
        }
    }
    
    /// <summary>
    /// Get record count for specified criteria
    /// </summary>
    /// <param name="criteria">Count criteria</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Record count</returns>
    public async Task<long> CountRecordsAsync(CountCriteria criteria, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to SQL Server");
        
        try
        {
            return criteria.TableName.ToLowerInvariant() switch
            {
                "scaledata" => await CountScaleDataAsync(criteria, cancellationToken),
                "protocoltemplates" => await CountProtocolTemplatesAsync(criteria, cancellationToken),
                "deviceconfigurations" => await CountDeviceConfigurationsAsync(criteria, cancellationToken),
                _ => throw new ArgumentException($"Unknown table name: {criteria.TableName}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to count records in table {TableName}", criteria.TableName);
            await PublishFailureAsync("CountRecords", ex.Message, ex, criteria);
            throw;
        }
    }
    
    #region IConfigurationRepository Implementation
    
    /// <summary>
    /// Save device configuration
    /// </summary>
    /// <param name="configuration">Device configuration to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Save operation result</returns>
    public async Task<StorageWriteResult> SaveDeviceConfigurationAsync(IDeviceConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        var startTime = DateTime.UtcNow;
        
        try
        {
            if (!IsConnected)
            {
                var connected = await ConnectAsync(cancellationToken: cancellationToken);
                if (!connected)
                    throw new InvalidOperationException("Failed to connect to SQL Server");
            }
            
            var entity = ConvertToDeviceConfigurationEntity(configuration);
            
            // Check if configuration already exists
            var existing = await _dbContext!.DeviceConfigurations
                .FirstOrDefaultAsync(dc => dc.DeviceId == configuration.DeviceId, cancellationToken);
            
            if (existing != null)
            {
                // Update existing
                existing.ConfigurationJson = entity.ConfigurationJson;
                existing.ModifiedAt = DateTimeOffset.UtcNow;
                _dbContext.DeviceConfigurations.Update(existing);
            }
            else
            {
                // Add new
                _dbContext.DeviceConfigurations.Add(entity);
            }
            
            var recordsWritten = await _dbContext.SaveChangesAsync(cancellationToken);
            var duration = DateTime.UtcNow - startTime;
            
            return new StorageWriteResult
            {
                Success = true,
                RecordsWritten = recordsWritten,
                Duration = duration,
                StorageBackend = StorageType
            };
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "Failed to save device configuration for {DeviceId}", configuration.DeviceId);
            await PublishFailureAsync("SaveDeviceConfiguration", ex.Message, ex, configuration);
            
            return new StorageWriteResult
            {
                Success = false,
                RecordsWritten = 0,
                RecordsFailed = 1,
                Duration = duration,
                StorageBackend = StorageType,
                ErrorMessage = ex.Message
            };
        }
    }
    
    /// <summary>
    /// Load device configuration by ID
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Device configuration or null if not found</returns>
    public async Task<IDeviceConfiguration?> LoadDeviceConfigurationAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        try
        {
            if (!IsConnected)
                throw new InvalidOperationException("Not connected to SQL Server");
            
            var entity = await _dbContext!.DeviceConfigurations
                .FirstOrDefaultAsync(dc => dc.DeviceId == deviceId, cancellationToken);
            
            return entity != null ? ConvertFromDeviceConfigurationEntity(entity) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load device configuration for {DeviceId}", deviceId);
            await PublishFailureAsync("LoadDeviceConfiguration", ex.Message, ex, deviceId);
            throw;
        }
    }
    
    /// <summary>
    /// Save protocol template
    /// </summary>
    /// <param name="template">Protocol template to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Save operation result</returns>
    public async Task<StorageWriteResult> SaveProtocolTemplateAsync(IProtocolTemplate template, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        var startTime = DateTime.UtcNow;
        
        try
        {
            if (!IsConnected)
            {
                var connected = await ConnectAsync(cancellationToken: cancellationToken);
                if (!connected)
                    throw new InvalidOperationException("Failed to connect to SQL Server");
            }
            
            var entity = ConvertToProtocolTemplateEntity(template);
            
            // Check if template already exists
            var existing = await _dbContext!.ProtocolTemplates
                .FirstOrDefaultAsync(pt => pt.TemplateName == template.TemplateName, cancellationToken);
            
            if (existing != null)
            {
                // Update existing
                existing.DisplayName = entity.DisplayName;
                existing.Description = entity.Description;
                existing.CommunicationSettingsJson = entity.CommunicationSettingsJson;
                existing.CommandTemplatesJson = entity.CommandTemplatesJson;
                existing.ResponsePatternsJson = entity.ResponsePatternsJson;
                existing.ModifiedAt = DateTimeOffset.UtcNow;
                _dbContext.ProtocolTemplates.Update(existing);
            }
            else
            {
                // Add new
                _dbContext.ProtocolTemplates.Add(entity);
            }
            
            var recordsWritten = await _dbContext.SaveChangesAsync(cancellationToken);
            var duration = DateTime.UtcNow - startTime;
            
            return new StorageWriteResult
            {
                Success = true,
                RecordsWritten = recordsWritten,
                Duration = duration,
                StorageBackend = StorageType
            };
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "Failed to save protocol template {TemplateName}", template.TemplateName);
            await PublishFailureAsync("SaveProtocolTemplate", ex.Message, ex, template);
            
            return new StorageWriteResult
            {
                Success = false,
                RecordsWritten = 0,
                RecordsFailed = 1,
                Duration = duration,
                StorageBackend = StorageType,
                ErrorMessage = ex.Message
            };
        }
    }
    
    /// <summary>
    /// Load protocol template by ID
    /// </summary>
    /// <param name="templateId">Template identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Protocol template or null if not found</returns>
    public async Task<IProtocolTemplate?> LoadProtocolTemplateAsync(string templateId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        try
        {
            if (!IsConnected)
                throw new InvalidOperationException("Not connected to SQL Server");
            
            var entity = await _dbContext!.ProtocolTemplates
                .FirstOrDefaultAsync(pt => pt.TemplateName == templateId, cancellationToken);
            
            return entity != null ? ConvertFromProtocolTemplateEntity(entity) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load protocol template {TemplateId}", templateId);
            await PublishFailureAsync("LoadProtocolTemplate", ex.Message, ex, templateId);
            throw;
        }
    }
    
    /// <summary>
    /// List all available protocol templates
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Available protocol templates</returns>
    public async Task<IReadOnlyList<IProtocolTemplate>> ListProtocolTemplatesAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        try
        {
            if (!IsConnected)
                throw new InvalidOperationException("Not connected to SQL Server");
            
            var entities = await _dbContext!.ProtocolTemplates
                .Where(pt => pt.IsActive)
                .OrderBy(pt => pt.DisplayName)
                .ToListAsync(cancellationToken);
            
            return entities.Select(ConvertFromProtocolTemplateEntity).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list protocol templates");
            await PublishFailureAsync("ListProtocolTemplates", ex.Message, ex);
            throw;
        }
    }
    
    /// <summary>
    /// Delete configuration or template
    /// </summary>
    /// <param name="id">Configuration/template identifier</param>
    /// <param name="type">Type of item to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Delete operation result</returns>
    public async Task<StorageWriteResult> DeleteAsync(string id, string type, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        var startTime = DateTime.UtcNow;
        
        try
        {
            if (!IsConnected)
                throw new InvalidOperationException("Not connected to SQL Server");
            
            var recordsDeleted = 0;
            
            switch (type.ToLowerInvariant())
            {
                case "deviceconfiguration":
                    var deviceConfig = await _dbContext!.DeviceConfigurations
                        .FirstOrDefaultAsync(dc => dc.DeviceId == id, cancellationToken);
                    if (deviceConfig != null)
                    {
                        _dbContext.DeviceConfigurations.Remove(deviceConfig);
                        recordsDeleted = 1;
                    }
                    break;
                
                case "protocoltemplate":
                    var template = await _dbContext!.ProtocolTemplates
                        .FirstOrDefaultAsync(pt => pt.TemplateName == id, cancellationToken);
                    if (template != null && !template.IsBuiltIn) // Don't allow deletion of built-in templates
                    {
                        _dbContext.ProtocolTemplates.Remove(template);
                        recordsDeleted = 1;
                    }
                    break;
                
                default:
                    throw new ArgumentException($"Unknown type: {type}");
            }
            
            if (recordsDeleted > 0)
            {
                await _dbContext!.SaveChangesAsync(cancellationToken);
            }
            
            var duration = DateTime.UtcNow - startTime;
            
            return new StorageWriteResult
            {
                Success = true,
                RecordsWritten = recordsDeleted,
                Duration = duration,
                StorageBackend = StorageType
            };
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "Failed to delete {Type} with ID {Id}", type, id);
            await PublishFailureAsync("Delete", ex.Message, ex, new { id, type });
            
            return new StorageWriteResult
            {
                Success = false,
                RecordsWritten = 0,
                RecordsFailed = 1,
                Duration = duration,
                StorageBackend = StorageType,
                ErrorMessage = ex.Message
            };
        }
    }
    
    #endregion
    
    /// <summary>
    /// Health check implementation for ASP.NET Core health checks
    /// </summary>
    /// <param name="context">Health check context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health check result</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var health = await GetHealthAsync();
            
            if (health.IsHealthy)
            {
                return HealthCheckResult.Healthy($"SQL Server is healthy", health.Diagnostics);
            }
            else
            {
                return HealthCheckResult.Degraded($"SQL Server issues detected: {health.LastError}", null, health.Diagnostics);
            }
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SQL Server health check failed", ex);
        }
    }
    
    #region Private Methods
    
    /// <summary>
    /// Validate configuration settings
    /// </summary>
    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_configuration.Server))
            throw new ArgumentException("SQL Server name is required", nameof(_configuration.Server));
        
        if (string.IsNullOrWhiteSpace(_configuration.Database))
            throw new ArgumentException("Database name is required", nameof(_configuration.Database));
    }
    
    /// <summary>
    /// Convert platform scale data reading to SQL entity
    /// </summary>
    /// <param name="scaleReading">Platform scale data reading</param>
    /// <returns>SQL entity</returns>
    private ScaleDataEntity ConvertToScaleEntity(IScaleDataReading scaleReading)
    {
        return new ScaleDataEntity
        {
            DeviceId = scaleReading.DeviceId,
            Channel = scaleReading.Channel,
            Timestamp = scaleReading.Timestamp,
            WeightKg = (decimal)scaleReading.WeightValue,
            RawWeight = scaleReading.RawValue,
            Unit = scaleReading.Unit,
            Status = scaleReading.Status,
            Quality = scaleReading.Quality.ToString(),
            AcquisitionTime = scaleReading.AcquisitionTime,
            StabilityScore = scaleReading.StabilityScore,
            ErrorMessage = scaleReading.ErrorMessage,
            Manufacturer = scaleReading.Manufacturer,
            Model = scaleReading.Model,
            SerialNumber = scaleReading.SerialNumber,
            ProtocolTemplate = scaleReading.ProtocolTemplate,
            MetadataJson = scaleReading.Metadata != null ? System.Text.Json.JsonSerializer.Serialize(scaleReading.Metadata) : null,
            CreatedAt = DateTimeOffset.UtcNow,
            ModifiedAt = DateTimeOffset.UtcNow
        };
    }
    
    /// <summary>
    /// Convert platform device configuration to SQL entity
    /// </summary>
    /// <param name="configuration">Platform device configuration</param>
    /// <returns>SQL entity</returns>
    private DeviceConfigurationEntity ConvertToDeviceConfigurationEntity(IDeviceConfiguration configuration)
    {
        return new DeviceConfigurationEntity
        {
            DeviceId = configuration.DeviceId,
            DeviceType = configuration.DeviceType,
            DeviceName = configuration.DeviceName,
            Description = configuration.Description,
            IpAddress = configuration.IpAddress,
            Port = configuration.Port,
            SerialPort = configuration.SerialPort,
            BaudRate = configuration.BaudRate,
            DataBits = configuration.DataBits,
            Parity = configuration.Parity,
            StopBits = configuration.StopBits,
            FlowControl = configuration.FlowControl,
            ProtocolTemplate = configuration.ProtocolTemplate,
            ConfigurationJson = System.Text.Json.JsonSerializer.Serialize(configuration),
            ChannelConfigurationsJson = configuration.ChannelConfigurations != null ? System.Text.Json.JsonSerializer.Serialize(configuration.ChannelConfigurations) : null,
            AcquisitionIntervalMs = configuration.AcquisitionIntervalMs,
            ConnectionTimeoutMs = configuration.ConnectionTimeoutMs,
            ReadTimeoutMs = configuration.ReadTimeoutMs,
            MaxRetries = configuration.MaxRetries,
            RetryDelayMs = configuration.RetryDelayMs,
            IsActive = configuration.IsActive,
            EnableHealthMonitoring = configuration.EnableHealthMonitoring,
            EnableStabilityMonitoring = configuration.EnableStabilityMonitoring,
            StabilityThreshold = configuration.StabilityThreshold,
            EnvironmentalOptimization = configuration.EnvironmentalOptimization,
            Location = configuration.Location,
            Department = configuration.Department,
            Manufacturer = configuration.Manufacturer,
            Model = configuration.Model,
            SerialNumber = configuration.SerialNumber,
            FirmwareVersion = configuration.FirmwareVersion,
            TagsJson = configuration.Tags != null ? System.Text.Json.JsonSerializer.Serialize(configuration.Tags) : null,
            CreatedAt = DateTimeOffset.UtcNow,
            ModifiedAt = DateTimeOffset.UtcNow
        };
    }
    
    /// <summary>
    /// Convert platform protocol template to SQL entity
    /// </summary>
    /// <param name="template">Platform protocol template</param>
    /// <returns>SQL entity</returns>
    private ProtocolTemplateEntity ConvertToProtocolTemplateEntity(IProtocolTemplate template)
    {
        return new ProtocolTemplateEntity
        {
            TemplateName = template.TemplateName,
            DisplayName = template.DisplayName,
            Manufacturer = template.Manufacturer,
            Model = template.Model,
            Description = template.Description,
            Version = template.Version,
            CommunicationSettingsJson = System.Text.Json.JsonSerializer.Serialize(template.CommunicationSettings),
            CommandTemplatesJson = System.Text.Json.JsonSerializer.Serialize(template.CommandTemplates),
            ResponsePatternsJson = System.Text.Json.JsonSerializer.Serialize(template.ResponsePatterns),
            ValidationRulesJson = template.ValidationRules != null ? System.Text.Json.JsonSerializer.Serialize(template.ValidationRules) : null,
            ErrorHandlingJson = template.ErrorHandling != null ? System.Text.Json.JsonSerializer.Serialize(template.ErrorHandling) : null,
            ConfigurationJson = template.Configuration != null ? System.Text.Json.JsonSerializer.Serialize(template.Configuration) : null,
            Priority = template.Priority,
            ConfidenceThreshold = template.ConfidenceThreshold,
            TimeoutMs = template.TimeoutMs,
            MaxRetries = template.MaxRetries,
            IsActive = template.IsActive,
            IsBuiltIn = template.IsBuiltIn,
            SupportedBaudRates = template.SupportedBaudRates != null ? string.Join(",", template.SupportedBaudRates) : null,
            EnvironmentalOptimization = template.EnvironmentalOptimization,
            TagsJson = template.Tags != null ? System.Text.Json.JsonSerializer.Serialize(template.Tags) : null,
            Author = template.Author,
            CreatedAt = DateTimeOffset.UtcNow,
            ModifiedAt = DateTimeOffset.UtcNow
        };
    }
    
    /// <summary>
    /// Convert SQL entity to platform device configuration
    /// </summary>
    /// <param name="entity">SQL entity</param>
    /// <returns>Platform device configuration</returns>
    private IDeviceConfiguration ConvertFromDeviceConfigurationEntity(DeviceConfigurationEntity entity)
    {
        // This would need to be implemented based on the actual IDeviceConfiguration interface
        // For now, return a simplified implementation
        throw new NotImplementedException("Device configuration deserialization not implemented");
    }
    
    /// <summary>
    /// Convert SQL entity to platform protocol template
    /// </summary>
    /// <param name="entity">SQL entity</param>
    /// <returns>Platform protocol template</returns>
    private IProtocolTemplate ConvertFromProtocolTemplateEntity(ProtocolTemplateEntity entity)
    {
        // This would need to be implemented based on the actual IProtocolTemplate interface
        // For now, return a simplified implementation
        throw new NotImplementedException("Protocol template deserialization not implemented");
    }
    
    /// <summary>
    /// Execute individual storage operation
    /// </summary>
    /// <param name="operation">Storage operation to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Operation result</returns>
    private async Task<StorageWriteResult> ExecuteStorageOperationAsync(StorageOperation operation, CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            var recordsAffected = 0;
            
            switch (operation.OperationType.ToUpperInvariant())
            {
                case "INSERT":
                    recordsAffected = await ExecuteInsertOperationAsync(operation, cancellationToken);
                    break;
                
                case "UPDATE":
                    recordsAffected = await ExecuteUpdateOperationAsync(operation, cancellationToken);
                    break;
                
                case "DELETE":
                    recordsAffected = await ExecuteDeleteOperationAsync(operation, cancellationToken);
                    break;
                
                default:
                    throw new ArgumentException($"Unknown operation type: {operation.OperationType}");
            }
            
            return new StorageWriteResult
            {
                Success = true,
                RecordsWritten = recordsAffected,
                Duration = DateTime.UtcNow - startTime,
                StorageBackend = StorageType
            };
        }
        catch (Exception ex)
        {
            return new StorageWriteResult
            {
                Success = false,
                RecordsWritten = 0,
                RecordsFailed = 1,
                Duration = DateTime.UtcNow - startTime,
                StorageBackend = StorageType,
                ErrorMessage = ex.Message
            };
        }
    }
    
    /// <summary>
    /// Execute INSERT operation
    /// </summary>
    private async Task<int> ExecuteInsertOperationAsync(StorageOperation operation, CancellationToken cancellationToken)
    {
        // Implementation would depend on the specific operation target and data
        // This is a simplified placeholder
        return await Task.FromResult(1);
    }
    
    /// <summary>
    /// Execute UPDATE operation
    /// </summary>
    private async Task<int> ExecuteUpdateOperationAsync(StorageOperation operation, CancellationToken cancellationToken)
    {
        // Implementation would depend on the specific operation target and data
        // This is a simplified placeholder
        return await Task.FromResult(1);
    }
    
    /// <summary>
    /// Execute DELETE operation
    /// </summary>
    private async Task<int> ExecuteDeleteOperationAsync(StorageOperation operation, CancellationToken cancellationToken)
    {
        // Implementation would depend on the specific operation target and data
        // This is a simplified placeholder
        return await Task.FromResult(1);
    }
    
    /// <summary>
    /// Query scale data with filters
    /// </summary>
    private async Task<List<IReadOnlyDictionary<string, object>>> QueryScaleDataAsync(RelationalQuery query, CancellationToken cancellationToken)
    {
        var queryable = _dbContext!.ScaleData.AsQueryable();
        
        // Apply filters
        foreach (var condition in query.WhereConditions)
        {
            // This would need proper expression building for complex queries
            // Simplified implementation for demonstration
        }
        
        // Apply ordering
        if (query.OrderBy.Any())
        {
            // Apply ordering logic
        }
        
        // Apply pagination
        if (query.Offset > 0)
            queryable = queryable.Skip(query.Offset);
        
        if (query.Limit.HasValue)
            queryable = queryable.Take(query.Limit.Value);
        
        var entities = await queryable.ToListAsync(cancellationToken);
        
        return entities.Select(e => new Dictionary<string, object>
        {
            ["Id"] = e.Id,
            ["DeviceId"] = e.DeviceId,
            ["Channel"] = e.Channel,
            ["Timestamp"] = e.Timestamp,
            ["WeightKg"] = e.WeightKg,
            ["RawWeight"] = e.RawWeight ?? (object)DBNull.Value,
            ["Unit"] = e.Unit ?? (object)DBNull.Value,
            ["Status"] = e.Status ?? (object)DBNull.Value,
            ["Quality"] = e.Quality,
            ["StabilityScore"] = e.StabilityScore ?? (object)DBNull.Value,
            ["Manufacturer"] = e.Manufacturer ?? (object)DBNull.Value,
            ["Model"] = e.Model ?? (object)DBNull.Value
        } as IReadOnlyDictionary<string, object>).ToList();
    }
    
    /// <summary>
    /// Query protocol templates with filters
    /// </summary>
    private async Task<List<IReadOnlyDictionary<string, object>>> QueryProtocolTemplatesAsync(RelationalQuery query, CancellationToken cancellationToken)
    {
        var queryable = _dbContext!.ProtocolTemplates.AsQueryable();
        
        // Apply filters, ordering, pagination similar to scale data
        var entities = await queryable.ToListAsync(cancellationToken);
        
        return entities.Select(e => new Dictionary<string, object>
        {
            ["Id"] = e.Id,
            ["TemplateName"] = e.TemplateName,
            ["DisplayName"] = e.DisplayName,
            ["Manufacturer"] = e.Manufacturer,
            ["Model"] = e.Model ?? (object)DBNull.Value,
            ["Description"] = e.Description ?? (object)DBNull.Value,
            ["Version"] = e.Version,
            ["Priority"] = e.Priority,
            ["IsActive"] = e.IsActive,
            ["CreatedAt"] = e.CreatedAt,
            ["ModifiedAt"] = e.ModifiedAt
        } as IReadOnlyDictionary<string, object>).ToList();
    }
    
    /// <summary>
    /// Query device configurations with filters
    /// </summary>
    private async Task<List<IReadOnlyDictionary<string, object>>> QueryDeviceConfigurationsAsync(RelationalQuery query, CancellationToken cancellationToken)
    {
        var queryable = _dbContext!.DeviceConfigurations.AsQueryable();
        
        // Apply filters, ordering, pagination similar to scale data
        var entities = await queryable.ToListAsync(cancellationToken);
        
        return entities.Select(e => new Dictionary<string, object>
        {
            ["Id"] = e.Id,
            ["DeviceId"] = e.DeviceId,
            ["DeviceType"] = e.DeviceType,
            ["DeviceName"] = e.DeviceName,
            ["IpAddress"] = e.IpAddress ?? (object)DBNull.Value,
            ["Port"] = e.Port ?? (object)DBNull.Value,
            ["IsActive"] = e.IsActive,
            ["Location"] = e.Location ?? (object)DBNull.Value,
            ["CreatedAt"] = e.CreatedAt,
            ["ModifiedAt"] = e.ModifiedAt
        } as IReadOnlyDictionary<string, object>).ToList();
    }
    
    /// <summary>
    /// Count scale data records
    /// </summary>
    private async Task<long> CountScaleDataAsync(CountCriteria criteria, CancellationToken cancellationToken)
    {
        var queryable = _dbContext!.ScaleData.AsQueryable();
        
        // Apply filters based on criteria
        if (criteria.TimeRange != null)
        {
            queryable = queryable.Where(sd => sd.Timestamp >= criteria.TimeRange.Start &&
                                             sd.Timestamp <= criteria.TimeRange.End);
        }
        
        return await queryable.CountAsync(cancellationToken);
    }
    
    /// <summary>
    /// Count protocol template records
    /// </summary>
    private async Task<long> CountProtocolTemplatesAsync(CountCriteria criteria, CancellationToken cancellationToken)
    {
        var queryable = _dbContext!.ProtocolTemplates.AsQueryable();
        return await queryable.CountAsync(cancellationToken);
    }
    
    /// <summary>
    /// Count device configuration records
    /// </summary>
    private async Task<long> CountDeviceConfigurationsAsync(CountCriteria criteria, CancellationToken cancellationToken)
    {
        var queryable = _dbContext!.DeviceConfigurations.AsQueryable();
        return await queryable.CountAsync(cancellationToken);
    }
    
    /// <summary>
    /// Get database capacity information
    /// </summary>
    private async Task<StorageCapacity> GetDatabaseCapacityAsync()
    {
        try
        {
            var query = @"
                SELECT 
                    SUM(size * 8192.0) as TotalSizeBytes,
                    SUM(CASE WHEN max_size = -1 THEN size * 8192.0 ELSE max_size * 8192.0 END) as MaxSizeBytes
                FROM sys.database_files 
                WHERE type = 0"; // Data files only
            
            using var command = _dbContext!.Database.GetDbConnection().CreateCommand();
            command.CommandText = query;
            
            if (command.Connection?.State != ConnectionState.Open)
                await command.Connection!.OpenAsync();
            
            using var reader = await command.ExecuteReaderAsync();
            
            if (await reader.ReadAsync())
            {
                var totalSize = reader.IsDBNull(0) ? 0L : Convert.ToInt64(reader[0]);
                var maxSize = reader.IsDBNull(1) ? totalSize : Convert.ToInt64(reader[1]);
                
                return new StorageCapacity
                {
                    TotalCapacity = maxSize,
                    UsedCapacity = totalSize
                };
            }
            
            return new StorageCapacity { TotalCapacity = 0, UsedCapacity = 0 };
        }
        catch
        {
            return new StorageCapacity { TotalCapacity = 0, UsedCapacity = 0 };
        }
    }
    
    /// <summary>
    /// Calculate current utilization percentage
    /// </summary>
    /// <param name="capacity">Storage capacity information</param>
    /// <returns>Utilization percentage (0-100)</returns>
    private double CalculateUtilization(StorageCapacity? capacity)
    {
        if (capacity == null || capacity.TotalCapacity <= 0)
            return 0;
        
        return (double)capacity.UsedCapacity / capacity.TotalCapacity * 100;
    }
    
    /// <summary>
    /// Publish storage metrics event
    /// </summary>
    private async Task PublishMetricsAsync()
    {
        try
        {
            var metrics = _performanceTracker.GetCurrentMetrics();
            var metricsEvent = new StorageMetricsEventArgs
            {
                StorageBackend = StorageType,
                Metrics = metrics
            };
            
            _metricsSubject.OnNext(metricsEvent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish storage metrics");
        }
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Publish storage failure event
    /// </summary>
    /// <param name="operationType">Type of operation that failed</param>
    /// <param name="errorMessage">Error message</param>
    /// <param name="exception">Exception that caused the failure</param>
    /// <param name="failedData">Data that failed to be processed</param>
    private async Task PublishFailureAsync(string operationType, string errorMessage, Exception? exception = null, object? failedData = null)
    {
        try
        {
            var failureEvent = new StorageFailureEventArgs
            {
                StorageBackend = StorageType,
                OperationType = operationType,
                ErrorMessage = errorMessage,
                Exception = exception,
                FailedData = failedData
            };
            
            _failureSubject.OnNext(failureEvent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish storage failure event");
        }
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Throw if the repository has been disposed
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(SqlServerTransactionalRepository));
    }
    
    #endregion
    
    /// <summary>
    /// Dispose of repository resources
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;
        
        _isDisposed = true;
        
        try
        {
            _metricsSubject.OnCompleted();
            _metricsSubject.Dispose();
            
            _failureSubject.OnCompleted();
            _failureSubject.Dispose();
            
            _ = DisconnectAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during SQL Server repository disposal");
        }
        
        _logger.LogInformation("SQL Server transactional repository disposed");
    }
}