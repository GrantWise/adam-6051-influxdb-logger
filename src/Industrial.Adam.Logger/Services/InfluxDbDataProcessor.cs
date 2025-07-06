// Industrial.Adam.Logger - InfluxDB Data Processor
// Data processor that writes to InfluxDB

using Industrial.Adam.Logger.Configuration;
using Industrial.Adam.Logger.Interfaces;
using Industrial.Adam.Logger.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Industrial.Adam.Logger.Services;

/// <summary>
/// Data processor that writes processed data to InfluxDB
/// </summary>
public class InfluxDbDataProcessor : IDataProcessor
{
    private readonly DefaultDataProcessor _baseProcessor;
    private readonly IInfluxDbWriter _influxDbWriter;
    private readonly ILogger<InfluxDbDataProcessor> _logger;
    private readonly AdamLoggerConfig _config;
    private readonly ConcurrentQueue<AdamDataReading> _processingQueue;
    private readonly Timer? _batchTimer;
    private readonly SemaphoreSlim _batchSemaphore;
    private bool _disposed;

    public InfluxDbDataProcessor(
        IDataValidator validator,
        IDataTransformer transformer,
        IInfluxDbWriter influxDbWriter,
        IOptions<AdamLoggerConfig> config,
        ILogger<InfluxDbDataProcessor> logger)
    {
        var baseLogger = logger as ILogger<DefaultDataProcessor> ?? 
            Microsoft.Extensions.Logging.LoggerFactoryExtensions.CreateLogger<DefaultDataProcessor>(
                Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        _baseProcessor = new DefaultDataProcessor(validator, transformer, baseLogger);
        _influxDbWriter = influxDbWriter;
        _config = config.Value;
        _logger = logger;
        _processingQueue = new ConcurrentQueue<AdamDataReading>();
        _batchSemaphore = new SemaphoreSlim(1, 1);

        // Set up batch timer if InfluxDB is configured
        if (_config.InfluxDb != null)
        {
            var batchInterval = TimeSpan.FromMilliseconds(_config.InfluxDb.FlushIntervalMs);
            _batchTimer = new Timer(ProcessBatch, null, batchInterval, batchInterval);
            _logger.LogInformation("InfluxDB data processor initialized with batch interval: {Interval}ms", 
                _config.InfluxDb.FlushIntervalMs);
        }
    }

    /// <inheritdoc/>
    public AdamDataReading ProcessRawData(
        string deviceId, 
        ChannelConfig channel, 
        ushort[] registers, 
        DateTimeOffset timestamp,
        TimeSpan acquisitionTime)
    {
        // Use the base processor for all the heavy lifting
        var reading = _baseProcessor.ProcessRawData(deviceId, channel, registers, timestamp, acquisitionTime);
        
        // Queue for InfluxDB writing if configured and data quality is good
        if (_config.InfluxDb != null && reading.Quality == DataQuality.Good)
        {
            _processingQueue.Enqueue(reading);
        }
        
        return reading;
    }

    /// <inheritdoc/>
    public double? CalculateRate(string deviceId, int channelNumber, long currentValue, DateTimeOffset timestamp)
    {
        return _baseProcessor.CalculateRate(deviceId, channelNumber, currentValue, timestamp);
    }

    /// <inheritdoc/>
    public DataQuality ValidateReading(ChannelConfig channel, long rawValue, double? rate)
    {
        return _baseProcessor.ValidateReading(channel, rawValue, rate);
    }

    private async void ProcessBatch(object? state)
    {
        if (_disposed || _config.InfluxDb == null)
            return;

        await _batchSemaphore.WaitAsync();
        try
        {
            var batch = new List<AdamDataReading>();
            var batchSize = _config.InfluxDb.WriteBatchSize;
            
            // Collect readings for batch
            while (batch.Count < batchSize && _processingQueue.TryDequeue(out var reading))
            {
                batch.Add(reading);
            }

            if (batch.Any())
            {
                try
                {
                    await _influxDbWriter.WriteBatchAsync(batch);
                    _logger.LogDebug("Successfully wrote batch of {Count} readings to InfluxDB", batch.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to write batch of {Count} readings to InfluxDB", batch.Count);
                    
                    // Re-queue failed readings for retry (up to a limit to prevent memory issues)
                    if (_processingQueue.Count < 10000) // Prevent unbounded growth
                    {
                        foreach (var failedReading in batch)
                        {
                            _processingQueue.Enqueue(failedReading);
                        }
                    }
                }
            }
        }
        finally
        {
            _batchSemaphore.Release();
        }
    }

    /// <summary>
    /// Flush any pending data to InfluxDB
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the flush operation</returns>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || _config.InfluxDb == null)
            return;

        await _batchSemaphore.WaitAsync(cancellationToken);
        try
        {
            var batch = new List<AdamDataReading>();
            
            // Collect all remaining readings
            while (_processingQueue.TryDequeue(out var reading))
            {
                batch.Add(reading);
            }

            if (batch.Any())
            {
                await _influxDbWriter.WriteBatchAsync(batch, cancellationToken);
                _logger.LogInformation("Flushed {Count} readings to InfluxDB", batch.Count);
            }
        }
        finally
        {
            _batchSemaphore.Release();
        }
    }

    /// <summary>
    /// Get the current queue size for monitoring
    /// </summary>
    /// <returns>Number of readings waiting to be written</returns>
    public int GetQueueSize()
    {
        return _processingQueue.Count;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            _batchTimer?.Dispose();
            
            // Flush any remaining data
            FlushAsync().Wait(TimeSpan.FromSeconds(10));
            
            _batchSemaphore?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing InfluxDB data processor");
        }

        _logger.LogInformation("InfluxDB data processor disposed");
    }
}