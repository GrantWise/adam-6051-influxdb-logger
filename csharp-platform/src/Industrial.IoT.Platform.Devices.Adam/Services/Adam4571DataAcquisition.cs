// Industrial.IoT.Platform.Devices.Adam - ADAM-4571 Data Acquisition Service
// Focused service for data reading and processing following SRP

using System.Reactive.Linq;
using System.Reactive.Subjects;
using Industrial.IoT.Platform.Core;
using Industrial.IoT.Platform.Core.Interfaces;
using Industrial.IoT.Platform.Devices.Adam.Configuration;
using Industrial.IoT.Platform.Devices.Adam.Models;
using Industrial.IoT.Platform.Devices.Adam.Services;
using Industrial.IoT.Platform.Protocols.Services;
using Microsoft.Extensions.Logging;

namespace Industrial.IoT.Platform.Devices.Adam.Services;

/// <summary>
/// Handles data acquisition and processing for ADAM-4571 devices
/// Single Responsibility: Data reading, parsing, and quality assessment
/// </summary>
public class Adam4571DataAcquisition : IDisposable
{
    private readonly Adam4571Configuration _config;
    private readonly Adam4571ConnectionManager _connectionManager;
    private readonly Adam4571HealthMonitor _healthMonitor;
    private readonly SignalStabilityMonitor _stabilityMonitor;
    private readonly ILogger<Adam4571DataAcquisition> _logger;

    private readonly Subject<IDataReading> _dataSubject = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly List<double> _stabilityBuffer = new();

    private Task? _dataAcquisitionTask;
    private volatile bool _isRunning;

    /// <summary>
    /// Observable stream of all data readings from this device
    /// </summary>
    public IObservable<IDataReading> DataStream => _dataSubject.AsObservable();

    /// <summary>
    /// Initialize the data acquisition service
    /// </summary>
    /// <param name="config">Device configuration</param>
    /// <param name="connectionManager">Connection manager service</param>
    /// <param name="healthMonitor">Health monitor service</param>
    /// <param name="stabilityMonitor">Signal stability monitor for RS232 reliability</param>
    /// <param name="logger">Logger for diagnostic information</param>
    public Adam4571DataAcquisition(
        Adam4571Configuration config,
        Adam4571ConnectionManager connectionManager,
        Adam4571HealthMonitor healthMonitor,
        SignalStabilityMonitor stabilityMonitor,
        ILogger<Adam4571DataAcquisition> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        _healthMonitor = healthMonitor ?? throw new ArgumentNullException(nameof(healthMonitor));
        _stabilityMonitor = stabilityMonitor ?? throw new ArgumentNullException(nameof(stabilityMonitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Start data acquisition from the device
    /// </summary>
    /// <returns>Task that completes when data acquisition has started</returns>
    public Task StartAsync()
    {
        if (_isRunning)
            return Task.CompletedTask;

        if (!_connectionManager.IsConnected)
            throw new InvalidOperationException($"Device {_config.DeviceId} is not connected. Establish connection first.");

        _isRunning = true;
        _logger.LogInformation("Starting data acquisition for ADAM-4571 device {DeviceId}", _config.DeviceId);

        // Start data acquisition task
        _dataAcquisitionTask = Task.Run(async () => await DataAcquisitionLoopAsync(_cancellationTokenSource.Token));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stop data acquisition from the device
    /// </summary>
    /// <returns>Task that completes when data acquisition has stopped</returns>
    public async Task StopAsync()
    {
        if (!_isRunning)
            return;

        _logger.LogInformation("Stopping data acquisition for ADAM-4571 device {DeviceId}", _config.DeviceId);

        _isRunning = false;
        _cancellationTokenSource.Cancel();

        if (_dataAcquisitionTask != null)
        {
            try
            {
                await _dataAcquisitionTask.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Timeout waiting for data acquisition task to complete for device {DeviceId}", _config.DeviceId);
            }
        }

        _logger.LogInformation("Stopped data acquisition for ADAM-4571 device {DeviceId}", _config.DeviceId);
    }

    /// <summary>
    /// Background loop for continuous data acquisition
    /// Follows the same pattern as AdamLoggerService data acquisition
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the background loop</returns>
    private async Task DataAcquisitionLoopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting data acquisition loop for device {DeviceId}", _config.DeviceId);

        while (!cancellationToken.IsCancellationRequested && _isRunning)
        {
            try
            {
                var startTime = DateTimeOffset.UtcNow;

                // Read scale data
                var reading = await ReadScaleDataAsync(cancellationToken);
                
                var acquisitionTime = DateTimeOffset.UtcNow - startTime;

                if (reading != null)
                {
                    _dataSubject.OnNext(reading);
                    _healthMonitor.RecordSuccessfulRead(acquisitionTime.TotalMilliseconds);

                    // Update stability buffer for stability detection
                    UpdateStabilityBuffer(reading.ProcessedWeight ?? reading.RawWeight);
                }
                else
                {
                    _healthMonitor.RecordFailedRead("No data received from device");
                }

                // Wait for next poll interval
                await Task.Delay(_config.PollingIntervalMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during data acquisition for device {DeviceId}", _config.DeviceId);
                _healthMonitor.RecordFailedRead(ex.Message);

                await Task.Delay(_config.RetryDelayMs, cancellationToken);
            }
        }

        _logger.LogDebug("Data acquisition loop stopped for device {DeviceId}", _config.DeviceId);
    }

    /// <summary>
    /// Read scale data from the device with signal stability monitoring
    /// Enhanced with RS232 reliability features for industrial environments
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Scale data reading or null if failed</returns>
    private async Task<ScaleDataReading?> ReadScaleDataAsync(CancellationToken cancellationToken)
    {
        try
        {
            var tcpProvider = _connectionManager.TcpProvider;
            if (tcpProvider == null || !_connectionManager.IsConnected)
                return null;

            // Check signal stability before attempting read
            var diagnostics = _stabilityMonitor.GetDiagnostics();
            if (diagnostics.CurrentState == SignalStabilityState.Disconnected)
            {
                _logger.LogWarning("Signal disconnected - skipping data read for device {DeviceId}", _config.DeviceId);
                return null;
            }

            // Placeholder implementation - will be replaced with protocol-specific commands in Phase 4
            var testCommand = System.Text.Encoding.ASCII.GetBytes("?\r");
            var result = await tcpProvider.SendAndReceiveAsync(testCommand, _config.TimeoutMs, cancellationToken);

            if (result.Success && result.Data?.Length > 0)
            {
                var timestamp = DateTime.UtcNow;
                
                // Add sample to stability monitor for continuous monitoring
                _stabilityMonitor.AddSample(result.Data, timestamp, true);
                
                // Filter the received data based on signal stability
                var filteredData = _stabilityMonitor.FilterIncomingData(result.Data);
                if (filteredData == null)
                {
                    _logger.LogDebug("Filtered out unstable data for device {DeviceId} - Signal state: {State}", 
                        _config.DeviceId, diagnostics.CurrentState);
                    return null;
                }

                // Parse the filtered response
                var response = System.Text.Encoding.ASCII.GetString(filteredData);
                var weight = ExtractWeightFromResponse(response);

                // Validate weight makes sense (additional stability check)
                if (!IsWeightReasonable(weight))
                {
                    _logger.LogDebug("Weight reading {Weight} appears unreasonable for device {DeviceId} - possible signal corruption", 
                        weight, _config.DeviceId);
                    _stabilityMonitor.AddSample(result.Data, timestamp, false); // Mark as invalid
                    return null;
                }

                // Apply processing based on configuration
                var processedWeight = ProcessRawWeight(weight);

                // Determine signal quality for the reading
                var signalQuality = CalculateReadingQuality(diagnostics);

                return new ScaleDataReading
                {
                    DeviceId = _config.DeviceId,
                    Channel = 0,
                    RawWeight = weight,
                    ProcessedWeight = processedWeight,
                    NetWeight = _config.EnableAutoTare ? processedWeight - GetTareWeight() : processedWeight,
                    TareWeight = _config.EnableAutoTare ? GetTareWeight() : null,
                    Timestamp = DateTimeOffset.UtcNow,
                    Stability = DetermineStability(processedWeight),
                    Quality = AssessDataQuality(weight, processedWeight),
                    Unit = _config.DefaultUnit,
                    Resolution = _config.Resolution,
                    Capacity = _config.Capacity,
                    AcquisitionTime = result.Duration,
                    Tags = new Dictionary<string, object>(_config.Tags)
                    {
                        ["Protocol"] = _connectionManager.DiscoveredProtocol ?? "Unknown",
                        ["StabilityBuffer"] = _stabilityBuffer.Count
                    }
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read scale data from device {DeviceId}", _config.DeviceId);
            return null;
        }
    }

    /// <summary>
    /// Extract weight value from device response (placeholder implementation)
    /// Will be replaced with protocol-specific parsing in Phase 4
    /// </summary>
    /// <param name="response">Raw response from device</param>
    /// <returns>Extracted weight value</returns>
    private double ExtractWeightFromResponse(string response)
    {
        // Placeholder implementation - will be replaced with protocol-specific parsing
        if (double.TryParse(response.Trim(), out var weight))
            return weight;
        
        return 0.0;
    }

    /// <summary>
    /// Process raw weight value applying configuration settings
    /// </summary>
    /// <param name="rawWeight">Raw weight from device</param>
    /// <returns>Processed weight value</returns>
    private double ProcessRawWeight(double rawWeight)
    {
        var processed = rawWeight;

        // Apply unit conversion if needed
        if (_config.EnableUnitConversion)
        {
            // Conversion logic will be implemented based on device protocol
        }

        // Apply averaging if enabled
        if (_config.EnableAveraging && _stabilityBuffer.Count >= _config.AveragingCount)
        {
            processed = _stabilityBuffer.TakeLast(_config.AveragingCount).Average();
        }

        // Round to configured decimal places
        processed = Math.Round(processed, _config.DecimalPlaces);

        return processed;
    }

    /// <summary>
    /// Check if weight reading is reasonable (additional stability validation)
    /// </summary>
    /// <param name="weight">Weight value to validate</param>
    /// <returns>True if weight appears reasonable</returns>
    private bool IsWeightReasonable(double weight)
    {
        // Basic sanity checks for weight values
        
        // Check for extreme values that suggest signal corruption
        if (double.IsNaN(weight) || double.IsInfinity(weight))
            return false;
            
        // Check if weight is within reasonable industrial scale range
        // Most industrial scales: -999999 to +999999 (varies by scale)
        if (Math.Abs(weight) > 1000000)
            return false;
            
        // If we have previous readings, check for sudden extreme changes
        if (_stabilityBuffer.Count > 0)
        {
            var lastWeight = _stabilityBuffer.LastOrDefault();
            var change = Math.Abs(weight - lastWeight);
            var percentChange = lastWeight != 0 ? (change / Math.Abs(lastWeight)) * 100 : 0;
            
            // Flag sudden changes >500% as likely corruption (configurable threshold)
            if (percentChange > 500 && change > 1.0) // And absolute change > 1 unit
                return false;
        }
        
        return true;
    }

    /// <summary>
    /// Calculate reading quality based on signal diagnostics
    /// </summary>
    /// <param name="diagnostics">Current signal diagnostics</param>
    /// <returns>Reading quality assessment</returns>
    private ScaleQuality CalculateReadingQuality(SignalDiagnostics diagnostics)
    {
        return diagnostics.CurrentState switch
        {
            SignalStabilityState.Stable => ScaleQuality.Good,
            SignalStabilityState.Noisy => diagnostics.StabilityScore > 70 ? ScaleQuality.Good : ScaleQuality.Uncertain,
            SignalStabilityState.Intermittent => ScaleQuality.Uncertain,
            SignalStabilityState.Unstable => ScaleQuality.Bad,
            SignalStabilityState.Corrupted => ScaleQuality.Bad,
            SignalStabilityState.Disconnected => ScaleQuality.Timeout,
            _ => ScaleQuality.Bad
        };
    }

    /// <summary>
    /// Get current tare weight (placeholder implementation)
    /// </summary>
    /// <returns>Tare weight value</returns>
    private double GetTareWeight()
    {
        // Placeholder - will be implemented with actual tare management in Phase 4
        return 0.0;
    }

    /// <summary>
    /// Update stability buffer and determine current stability
    /// </summary>
    /// <param name="weight">Current weight value</param>
    private void UpdateStabilityBuffer(double weight)
    {
        _stabilityBuffer.Add(weight);
        
        // Keep only recent readings for stability analysis
        while (_stabilityBuffer.Count > Constants.DefaultStabilityWindow)
            _stabilityBuffer.RemoveAt(0);
    }

    /// <summary>
    /// Determine stability based on recent weight readings
    /// </summary>
    /// <param name="currentWeight">Current weight reading</param>
    /// <returns>Stability assessment</returns>
    private ScaleStability DetermineStability(double currentWeight)
    {
        if (_stabilityBuffer.Count < 3)
            return ScaleStability.Unknown;

        var variance = CalculateVariance(_stabilityBuffer);
        
        if (variance <= _config.StabilityTolerance)
        {
            // Check if stable for required duration
            var stableTime = _stabilityBuffer.Count * _config.PollingIntervalMs;
            return stableTime >= _config.StabilityThresholdMs 
                ? ScaleStability.Stable 
                : ScaleStability.Settling;
        }

        return ScaleStability.Unstable;
    }

    /// <summary>
    /// Assess data quality based on weight values and configuration
    /// </summary>
    /// <param name="rawWeight">Raw weight value</param>
    /// <param name="processedWeight">Processed weight value</param>
    /// <returns>Quality assessment</returns>
    private ScaleQuality AssessDataQuality(double rawWeight, double processedWeight)
    {
        // Check for overload condition
        if (_config.Capacity.HasValue && processedWeight > _config.Capacity.Value)
            return ScaleQuality.Overload;

        // Check for underload condition
        if (processedWeight < 0 && Math.Abs(processedWeight) > (_config.Resolution ?? 0.1))
            return ScaleQuality.Underload;

        // Check for data validation if enabled
        if (_config.EnableDataValidation)
        {
            // Additional validation logic can be added here
            var difference = Math.Abs(rawWeight - processedWeight);
            if (difference > (processedWeight * 0.1)) // 10% difference threshold
                return ScaleQuality.Uncertain;
        }

        return ScaleQuality.Good;
    }

    /// <summary>
    /// Calculate variance of weight readings
    /// </summary>
    /// <param name="values">Weight values</param>
    /// <returns>Standard deviation (square root of variance)</returns>
    private static double CalculateVariance(IReadOnlyList<double> values)
    {
        if (values.Count < 2) return 0;

        var mean = values.Average();
        var variance = values.Sum(x => Math.Pow(x - mean, 2)) / (values.Count - 1);
        return Math.Sqrt(variance);
    }

    /// <summary>
    /// Dispose of all resources used by this data acquisition service
    /// </summary>
    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        
        try
        {
            StopAsync().Wait(TimeSpan.FromSeconds(10));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during disposal of data acquisition for device {DeviceId}", _config.DeviceId);
        }

        _dataSubject.Dispose();
        _cancellationTokenSource.Dispose();
    }
}