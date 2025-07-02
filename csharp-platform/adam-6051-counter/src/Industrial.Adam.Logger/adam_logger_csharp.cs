// Industrial.Adam.Logger - Reusable ADAM device data acquisition library
// Designed to be plugged into any industrial application (OEE, SCADA, MES, etc.)

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NModbus;

namespace Industrial.Adam.Logger
{
    #region Core Data Models

    /// <summary>
    /// Represents a data reading from an ADAM device channel
    /// </summary>
    public sealed record AdamDataReading
    {
        public required string DeviceId { get; init; }
        public required int Channel { get; init; }
        public required long RawValue { get; init; }
        public required DateTimeOffset Timestamp { get; init; }
        public double? ProcessedValue { get; init; }
        public double? Rate { get; init; }
        public DataQuality Quality { get; init; } = DataQuality.Good;
        public string? Unit { get; init; }
        public TimeSpan AcquisitionTime { get; init; }
        public Dictionary<string, object> Tags { get; init; } = new();
        public string? ErrorMessage { get; init; }
    }

    /// <summary>
    /// Device health and diagnostic information
    /// </summary>
    public sealed record AdamDeviceHealth
    {
        public required string DeviceId { get; init; }
        public required DateTimeOffset Timestamp { get; init; }
        public required DeviceStatus Status { get; init; }
        public bool IsConnected { get; init; }
        public TimeSpan? LastSuccessfulRead { get; init; }
        public int ConsecutiveFailures { get; init; }
        public double? CommunicationLatency { get; init; }
        public string? LastError { get; init; }
        public int TotalReads { get; init; }
        public int SuccessfulReads { get; init; }
        public double SuccessRate => TotalReads > 0 ? (double)SuccessfulReads / TotalReads * 100 : 0;
    }

    public enum DataQuality
    {
        Good = 0,
        Uncertain = 1,
        Bad = 2,
        Timeout = 3,
        DeviceFailure = 4,
        ConfigurationError = 5,
        Overflow = 6
    }

    public enum DeviceStatus
    {
        Online = 0,
        Warning = 1,
        Error = 2,
        Offline = 3,
        Unknown = 4
    }

    #endregion

    #region Configuration Models

    /// <summary>
    /// Configuration for an ADAM device
    /// </summary>
    public class AdamDeviceConfig : IValidatableObject
    {
        [Required]
        public string DeviceId { get; set; } = string.Empty;

        [Required]
        public string IpAddress { get; set; } = string.Empty;

        [Range(Constants.MinPortNumber, Constants.MaxPortNumber)]
        public int Port { get; set; } = Constants.DefaultModbusPort;

        [Range(Constants.MinModbusUnitId, Constants.MaxModbusUnitId)]
        public byte UnitId { get; set; } = Constants.MinModbusUnitId;

        [Range(500, 30000)]
        public int TimeoutMs { get; set; } = Constants.DefaultDeviceTimeoutMs;

        [Range(0, 10)]
        public int MaxRetries { get; set; } = Constants.DefaultMaxRetries;

        [Range(100, 10000)]
        public int RetryDelayMs { get; set; } = Constants.DefaultRetryDelayMs;

        [Required]
        public List<ChannelConfig> Channels { get; set; } = new();

        // Advanced connection settings
        public bool KeepAlive { get; set; } = true;
        public bool EnableNagle { get; set; } = false;
        public int ReceiveBufferSize { get; set; } = Constants.DefaultReceiveBufferSize;
        public int SendBufferSize { get; set; } = Constants.DefaultSendBufferSize;

        // Data processing options
        public bool EnableRateCalculation { get; set; } = true;
        public int RateWindowSeconds { get; set; } = Constants.DefaultRateWindowSeconds;
        public bool EnableDataValidation { get; set; } = true;
        public long OverflowThreshold { get; set; } = Constants.DefaultOverflowThreshold;

        // Custom tags that will be attached to all readings from this device
        public Dictionary<string, object> Tags { get; set; } = new();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(DeviceId))
                yield return new ValidationResult("DeviceId is required", new[] { nameof(DeviceId) });

            if (string.IsNullOrWhiteSpace(IpAddress) || !System.Net.IPAddress.TryParse(IpAddress, out _))
                yield return new ValidationResult("Valid IP address is required", new[] { nameof(IpAddress) });

            if (!Channels.Any())
                yield return new ValidationResult("At least one channel must be configured", new[] { nameof(Channels) });

            var duplicateChannels = Channels.GroupBy(c => c.ChannelNumber).Where(g => g.Count() > 1);
            if (duplicateChannels.Any())
                yield return new ValidationResult("Duplicate channel numbers are not allowed", new[] { nameof(Channels) });
        }
    }

    /// <summary>
    /// Configuration for a specific channel on an ADAM device
    /// </summary>
    public class ChannelConfig : IValidatableObject
    {
        [Range(0, 255)]
        public int ChannelNumber { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
        public bool Enabled { get; set; } = true;

        // Modbus register configuration
        [Range(0, Constants.MaxModbusRegisterAddress)]
        public ushort StartRegister { get; set; }

        [Range(1, 4)]
        public int RegisterCount { get; set; } = Constants.CounterRegisterCount; // Default for 32-bit counter

        // Data processing
        public double ScaleFactor { get; set; } = 1.0;
        public double Offset { get; set; } = 0.0;
        public string Unit { get; set; } = DefaultUnits.Counts;
        public int DecimalPlaces { get; set; } = Constants.DefaultDecimalPlaces;

        // Validation limits
        public long? MinValue { get; set; }
        public long? MaxValue { get; set; }
        public double? MaxRateOfChange { get; set; }

        // Channel-specific tags
        public Dictionary<string, object> Tags { get; set; } = new();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(Name))
                yield return new ValidationResult("Channel name is required", new[] { nameof(Name) });

            if (MinValue.HasValue && MaxValue.HasValue && MinValue > MaxValue)
                yield return new ValidationResult("MinValue cannot be greater than MaxValue");

            if (ScaleFactor == 0)
                yield return new ValidationResult("ScaleFactor cannot be zero", new[] { nameof(ScaleFactor) });
        }
    }

    /// <summary>
    /// Main configuration for the ADAM logger service
    /// </summary>
    public class AdamLoggerConfig
    {
        [Required]
        public List<AdamDeviceConfig> Devices { get; set; } = new();

        [Range(100, 300000)]
        public int PollIntervalMs { get; set; } = Constants.DefaultPollIntervalMs;

        [Range(5000, 300000)]
        public int HealthCheckIntervalMs { get; set; } = Constants.DefaultHealthCheckIntervalMs;

        [Range(1, 50)]
        public int MaxConcurrentDevices { get; set; } = Constants.DefaultMaxConcurrentDevices;

        // Data buffering and performance
        [Range(100, 100000)]
        public int DataBufferSize { get; set; } = Constants.DefaultDataBufferSize;

        [Range(1, 1000)]
        public int BatchSize { get; set; } = Constants.DefaultBatchSize;

        [Range(100, 30000)]
        public int BatchTimeoutMs { get; set; } = Constants.DefaultBatchTimeoutMs;

        // Error handling
        public bool EnableAutomaticRecovery { get; set; } = true;
        public int MaxConsecutiveFailures { get; set; } = Constants.DefaultMaxConsecutiveFailures;
        public int DeviceTimeoutMinutes { get; set; } = Constants.DefaultDeviceTimeoutMinutes;

        // Performance monitoring
        public bool EnablePerformanceCounters { get; set; } = true;
        public bool EnableDetailedLogging { get; set; } = false;
    }

    #endregion

    #region Core Interfaces

    /// <summary>
    /// Main interface for the ADAM logger service - this is what consuming applications will use
    /// </summary>
    public interface IAdamLoggerService : IDisposable
    {
        /// <summary>
        /// Stream of all data readings from all configured devices
        /// </summary>
        IObservable<AdamDataReading> DataStream { get; }

        /// <summary>
        /// Stream of device health updates
        /// </summary>
        IObservable<AdamDeviceHealth> HealthStream { get; }

        /// <summary>
        /// Start data acquisition
        /// </summary>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stop data acquisition
        /// </summary>
        Task StopAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get current health status for a specific device
        /// </summary>
        Task<AdamDeviceHealth?> GetDeviceHealthAsync(string deviceId);

        /// <summary>
        /// Get health status for all devices
        /// </summary>
        Task<IReadOnlyList<AdamDeviceHealth>> GetAllDeviceHealthAsync();

        /// <summary>
        /// Check if the service is currently running
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Add a device at runtime
        /// </summary>
        Task AddDeviceAsync(AdamDeviceConfig deviceConfig);

        /// <summary>
        /// Remove a device at runtime
        /// </summary>
        Task RemoveDeviceAsync(string deviceId);

        /// <summary>
        /// Update device configuration at runtime
        /// </summary>
        Task UpdateDeviceConfigAsync(AdamDeviceConfig deviceConfig);
    }

    /// <summary>
    /// Interface for processing raw Modbus data into application-specific readings
    /// </summary>
    public interface IDataProcessor
    {
        AdamDataReading ProcessRawData(string deviceId, ChannelConfig channel, ushort[] registers, DateTimeOffset timestamp, TimeSpan acquisitionTime);
        double? CalculateRate(string deviceId, int channelNumber, long currentValue, DateTimeOffset timestamp);
        DataQuality ValidateReading(ChannelConfig channel, long rawValue, double? rate);
    }

    /// <summary>
    /// Interface for custom data validation logic
    /// </summary>
    public interface IDataValidator
    {
        DataQuality ValidateReading(AdamDataReading reading, ChannelConfig channelConfig);
        bool IsValidRange(long value, ChannelConfig channelConfig);
        bool IsValidRateOfChange(double? rate, ChannelConfig channelConfig);
    }

    /// <summary>
    /// Interface for custom data transformation logic
    /// </summary>
    public interface IDataTransformer
    {
        double? TransformValue(long rawValue, ChannelConfig channelConfig);
        Dictionary<string, object> EnrichTags(Dictionary<string, object> baseTags, AdamDeviceConfig deviceConfig, ChannelConfig channelConfig);
    }

    #endregion

    #region Modbus Device Manager

    internal interface IModbusDeviceManager : IDisposable
    {
        string DeviceId { get; }
        AdamDeviceConfig Configuration { get; }
        bool IsConnected { get; }
        Task<bool> ConnectAsync(CancellationToken cancellationToken = default);
        Task<ModbusReadResult> ReadRegistersAsync(ushort startAddress, ushort count, CancellationToken cancellationToken = default);
        Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
    }

    internal record ModbusReadResult
    {
        public bool Success { get; init; }
        public ushort[]? Data { get; init; }
        public Exception? Error { get; init; }
        public TimeSpan Duration { get; init; }
    }

    internal class ModbusDeviceManager : IModbusDeviceManager
    {
        private readonly AdamDeviceConfig _config;
        private readonly ILogger<ModbusDeviceManager> _logger;
        private TcpClient? _tcpClient;
        private IModbusMaster? _modbusMaster;
        private readonly SemaphoreSlim _connectionLock = new(1, 1);
        private volatile bool _isConnected;
        private DateTimeOffset _lastConnectionAttempt = DateTimeOffset.MinValue;

        public string DeviceId => _config.DeviceId;
        public AdamDeviceConfig Configuration => _config;
        public bool IsConnected => _isConnected;

        public ModbusDeviceManager(AdamDeviceConfig config, ILogger<ModbusDeviceManager> logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            // Prevent connection spam
            if (DateTimeOffset.UtcNow - _lastConnectionAttempt < TimeSpan.FromSeconds(Constants.ConnectionRetryCooldownSeconds))
                return _isConnected;

            _lastConnectionAttempt = DateTimeOffset.UtcNow;

            await _connectionLock.WaitAsync(cancellationToken);
            try
            {
                Disconnect();

                _tcpClient = new TcpClient
                {
                    ReceiveTimeout = _config.TimeoutMs,
                    SendTimeout = _config.TimeoutMs,
                    NoDelay = !_config.EnableNagle,
                    ReceiveBufferSize = _config.ReceiveBufferSize,
                    SendBufferSize = _config.SendBufferSize
                };

                if (_config.KeepAlive)
                    _tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

                await _tcpClient.ConnectAsync(_config.IpAddress, _config.Port);

                var factory = new ModbusFactory();
                _modbusMaster = factory.CreateMaster(_tcpClient);
                _modbusMaster.Transport.ReadTimeout = _config.TimeoutMs;
                _modbusMaster.Transport.WriteTimeout = _config.TimeoutMs;
                _modbusMaster.Transport.Retries = Math.Max(0, _config.MaxRetries);

                _isConnected = true;
                _logger.LogInformation("Connected to ADAM device {DeviceId} at {IpAddress}:{Port}", 
                    _config.DeviceId, _config.IpAddress, _config.Port);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to connect to ADAM device {DeviceId} at {IpAddress}:{Port}", 
                    _config.DeviceId, _config.IpAddress, _config.Port);
                _isConnected = false;
                return false;
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        public async Task<ModbusReadResult> ReadRegistersAsync(ushort startAddress, ushort count, CancellationToken cancellationToken = default)
        {
            if (!_isConnected && !await ConnectAsync(cancellationToken))
            {
                return new ModbusReadResult
                {
                    Success = false,
                    Error = new InvalidOperationException("Device not connected"),
                    Duration = TimeSpan.Zero
                };
            }

            var stopwatch = Stopwatch.StartNew();
            
            for (int attempt = 0; attempt <= _config.MaxRetries; attempt++)
            {
                try
                {
                    if (_modbusMaster == null)
                        throw new InvalidOperationException("Modbus master not initialized");

                    var registers = await Task.Run(() => 
                        _modbusMaster.ReadHoldingRegisters(_config.UnitId, startAddress, count), cancellationToken);

                    return new ModbusReadResult
                    {
                        Success = true,
                        Data = registers,
                        Duration = stopwatch.Elapsed
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Modbus read attempt {Attempt} failed for device {DeviceId}", 
                        attempt + 1, _config.DeviceId);

                    if (attempt < _config.MaxRetries)
                    {
                        _isConnected = false;
                        await Task.Delay(_config.RetryDelayMs, cancellationToken);
                        await ConnectAsync(cancellationToken);
                    }
                    else
                    {
                        return new ModbusReadResult
                        {
                            Success = false,
                            Error = ex,
                            Duration = stopwatch.Elapsed
                        };
                    }
                }
            }

            return new ModbusReadResult
            {
                Success = false,
                Error = new TimeoutException("Max retries exceeded"),
                Duration = stopwatch.Elapsed
            };
        }

        public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Try to read a single register to test connectivity
                var result = await ReadRegistersAsync(0, 1, cancellationToken);
                return result.Success;
            }
            catch
            {
                return false;
            }
        }

        private void Disconnect()
        {
            try
            {
                _modbusMaster?.Dispose();
                _tcpClient?.Close();
                _tcpClient?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during disconnect for device {DeviceId}", _config.DeviceId);
            }
            finally
            {
                _modbusMaster = null;
                _tcpClient = null;
                _isConnected = false;
            }
        }

        public void Dispose()
        {
            Disconnect();
            _connectionLock.Dispose();
        }
    }

    #endregion

    #region Data Processing

    public class DefaultDataProcessor : IDataProcessor
    {
        private readonly ConcurrentDictionary<string, Dictionary<int, List<(DateTimeOffset timestamp, long value)>>> _rateHistory = new();
        private readonly IDataValidator _validator;
        private readonly IDataTransformer _transformer;
        private readonly ILogger<DefaultDataProcessor> _logger;

        public DefaultDataProcessor(
            IDataValidator validator,
            IDataTransformer transformer,
            ILogger<DefaultDataProcessor> logger)
        {
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _transformer = transformer ?? throw new ArgumentNullException(nameof(transformer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public AdamDataReading ProcessRawData(
            string deviceId, 
            ChannelConfig channel, 
            ushort[] registers, 
            DateTimeOffset timestamp,
            TimeSpan acquisitionTime)
        {
            try
            {
                // Convert registers to 32-bit value (assuming little-endian)
                long rawValue = registers.Length >= Constants.CounterRegisterCount 
                    ? ((long)registers[1] << Constants.ModbusRegisterBits) | registers[0]
                    : registers[0];

                // Apply transformation
                var processedValue = _transformer.TransformValue(rawValue, channel);

                // Calculate rate if enabled
                double? rate = null;
                if (processedValue.HasValue)
                {
                    rate = CalculateRate(deviceId, channel.ChannelNumber, rawValue, timestamp);
                }

                // Create base reading
                var reading = new AdamDataReading
                {
                    DeviceId = deviceId,
                    Channel = channel.ChannelNumber,
                    RawValue = rawValue,
                    ProcessedValue = processedValue,
                    Rate = rate,
                    Timestamp = timestamp,
                    Unit = channel.Unit,
                    AcquisitionTime = acquisitionTime,
                    Tags = _transformer.EnrichTags(channel.Tags, null!, channel), // Device config would be passed in real implementation
                    Quality = _validator.ValidateReading(new AdamDataReading 
                    { 
                        DeviceId = deviceId, 
                        Channel = channel.ChannelNumber, 
                        RawValue = rawValue, 
                        Rate = rate,
                        Timestamp = timestamp,
                        ProcessedValue = processedValue,
                        Unit = channel.Unit,
                        AcquisitionTime = acquisitionTime
                    }, channel)
                };

                return reading;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing data for device {DeviceId}, channel {Channel}", 
                    deviceId, channel.ChannelNumber);

                return new AdamDataReading
                {
                    DeviceId = deviceId,
                    Channel = channel.ChannelNumber,
                    RawValue = 0,
                    Timestamp = timestamp,
                    Quality = DataQuality.ConfigurationError,
                    ErrorMessage = ex.Message,
                    AcquisitionTime = acquisitionTime
                };
            }
        }

        public double? CalculateRate(string deviceId, int channelNumber, long currentValue, DateTimeOffset timestamp)
        {
            var key = $"{deviceId}_{channelNumber}";
            
            if (!_rateHistory.ContainsKey(deviceId))
                _rateHistory[deviceId] = new Dictionary<int, List<(DateTimeOffset, long)>>();

            if (!_rateHistory[deviceId].ContainsKey(channelNumber))
                _rateHistory[deviceId][channelNumber] = new List<(DateTimeOffset, long)>();

            var history = _rateHistory[deviceId][channelNumber];
            history.Add((timestamp, currentValue));

            // Keep only last 5 minutes of data for rate calculation
            var cutoff = timestamp.AddMinutes(-Constants.DefaultRateCalculationWindowMinutes);
            history.RemoveAll(h => h.Item1 < cutoff);

            if (history.Count < 2)
                return null;

            var oldest = history.First();
            var newest = history.Last();

            var timeDiff = (newest.Item1 - oldest.Item1).TotalSeconds;
            if (timeDiff <= 0)
                return null;

            var valueDiff = newest.Item2 - oldest.Item2;
            return valueDiff / timeDiff;
        }

        public DataQuality ValidateReading(ChannelConfig channel, long rawValue, double? rate)
        {
            return _validator.ValidateReading(new AdamDataReading 
            { 
                DeviceId = "", 
                Channel = channel.ChannelNumber, 
                RawValue = rawValue, 
                Rate = rate,
                Timestamp = DateTimeOffset.UtcNow
            }, channel);
        }
    }

    public class DefaultDataValidator : IDataValidator
    {
        public DataQuality ValidateReading(AdamDataReading reading, ChannelConfig channelConfig)
        {
            if (!IsValidRange(reading.RawValue, channelConfig))
                return DataQuality.Bad;

            if (!IsValidRateOfChange(reading.Rate, channelConfig))
                return DataQuality.Uncertain;

            return DataQuality.Good;
        }

        public bool IsValidRange(long value, ChannelConfig channelConfig)
        {
            if (channelConfig.MinValue.HasValue && value < channelConfig.MinValue.Value)
                return false;

            if (channelConfig.MaxValue.HasValue && value > channelConfig.MaxValue.Value)
                return false;

            return true;
        }

        public bool IsValidRateOfChange(double? rate, ChannelConfig channelConfig)
        {
            if (!rate.HasValue || !channelConfig.MaxRateOfChange.HasValue)
                return true;

            return Math.Abs(rate.Value) <= channelConfig.MaxRateOfChange.Value;
        }
    }

    public class DefaultDataTransformer : IDataTransformer
    {
        public double? TransformValue(long rawValue, ChannelConfig channelConfig)
        {
            var scaled = rawValue * channelConfig.ScaleFactor + channelConfig.Offset;
            return Math.Round(scaled, channelConfig.DecimalPlaces);
        }

        public Dictionary<string, object> EnrichTags(Dictionary<string, object> baseTags, AdamDeviceConfig deviceConfig, ChannelConfig channelConfig)
        {
            var enrichedTags = new Dictionary<string, object>(baseTags);
            
            // Add channel metadata
            enrichedTags["channel_name"] = channelConfig.Name;
            if (!string.IsNullOrWhiteSpace(channelConfig.Description))
                enrichedTags["channel_description"] = channelConfig.Description;

            // Add device metadata if provided
            if (deviceConfig?.Tags != null)
            {
                foreach (var tag in deviceConfig.Tags)
                    enrichedTags.TryAdd($"device_{tag.Key}", tag.Value);
            }

            return enrichedTags;
        }
    }

    #endregion

    #region Main Service Implementation

    public class AdamLoggerService : IAdamLoggerService, IHostedService, IHealthCheck
    {
        private readonly AdamLoggerConfig _config;
        private readonly IDataProcessor _dataProcessor;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AdamLoggerService> _logger;

        private readonly ConcurrentDictionary<string, IModbusDeviceManager> _deviceManagers = new();
        private readonly ConcurrentDictionary<string, AdamDeviceHealth> _deviceHealth = new();
        
        private readonly Subject<AdamDataReading> _dataSubject = new();
        private readonly Subject<AdamDeviceHealth> _healthSubject = new();
        
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private Task? _acquisitionTask;
        private Task? _healthCheckTask;

        public IObservable<AdamDataReading> DataStream => _dataSubject.AsObservable();
        public IObservable<AdamDeviceHealth> HealthStream => _healthSubject.AsObservable();
        public bool IsRunning { get; private set; }

        public AdamLoggerService(
            IOptions<AdamLoggerConfig> config,
            IDataProcessor dataProcessor,
            IServiceProvider serviceProvider,
            ILogger<AdamLoggerService> logger)
        {
            _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
            _dataProcessor = dataProcessor ?? throw new ArgumentNullException(nameof(dataProcessor));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            ValidateConfiguration();
        }

        private void ValidateConfiguration()
        {
            var validationResults = new List<ValidationResult>();
            var validationContext = new ValidationContext(_config);
            
            if (!Validator.TryValidateObject(_config, validationContext, validationResults, true))
            {
                var errors = string.Join(", ", validationResults.Select(r => r.ErrorMessage));
                throw new ArgumentException($"Invalid configuration: {errors}");
            }

            foreach (var device in _config.Devices)
            {
                var deviceValidationResults = new List<ValidationResult>();
                var deviceValidationContext = new ValidationContext(device);
                
                if (!Validator.TryValidateObject(device, deviceValidationContext, deviceValidationResults, true))
                {
                    var errors = string.Join(", ", deviceValidationResults.Select(r => r.ErrorMessage));
                    throw new ArgumentException($"Invalid device configuration for {device.DeviceId}: {errors}");
                }
            }
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (IsRunning)
                return Task.CompletedTask;

            _logger.LogInformation("Starting ADAM Logger Service with {DeviceCount} devices", _config.Devices.Count);

            // Initialize device managers
            foreach (var deviceConfig in _config.Devices)
            {
                var deviceLogger = _serviceProvider.GetRequiredService<ILogger<ModbusDeviceManager>>();
                var manager = new ModbusDeviceManager(deviceConfig, deviceLogger);
                _deviceManagers[deviceConfig.DeviceId] = manager;

                // Initialize health status
                _deviceHealth[deviceConfig.DeviceId] = new AdamDeviceHealth
                {
                    DeviceId = deviceConfig.DeviceId,
                    Timestamp = DateTimeOffset.UtcNow,
                    Status = DeviceStatus.Unknown,
                    IsConnected = false
                };
            }

            // Start acquisition and health check tasks
            _acquisitionTask = RunDataAcquisitionAsync(_cancellationTokenSource.Token);
            _healthCheckTask = RunHealthCheckAsync(_cancellationTokenSource.Token);

            IsRunning = true;
            _logger.LogInformation("ADAM Logger Service started successfully");
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (!IsRunning)
                return;

            _logger.LogInformation("Stopping ADAM Logger Service");

            _cancellationTokenSource.Cancel();

            try
            {
                if (_acquisitionTask != null)
                    await _acquisitionTask;
                if (_healthCheckTask != null)
                    await _healthCheckTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }

            // Dispose device managers
            foreach (var manager in _deviceManagers.Values)
            {
                manager.Dispose();
            }
            _deviceManagers.Clear();

            IsRunning = false;
            _logger.LogInformation("ADAM Logger Service stopped");
        }

        private async Task RunDataAcquisitionAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting data acquisition loop with {IntervalMs}ms interval", _config.PollIntervalMs);

            while (!cancellationToken.IsCancellationRequested)
            {
                var acquisitionStart = DateTimeOffset.UtcNow;

                try
                {
                    var tasks = _deviceManagers.Values.Select(async manager =>
                    {
                        try
                        {
                            await ReadDeviceDataAsync(manager, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error reading data from device {DeviceId}", manager.DeviceId);
                            UpdateDeviceHealth(manager.DeviceId, false, ex.Message);
                        }
                    });

                    await Task.WhenAll(tasks);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in data acquisition loop");
                }

                // Calculate sleep time to maintain interval
                var elapsed = DateTimeOffset.UtcNow - acquisitionStart;
                var sleepTime = TimeSpan.FromMilliseconds(_config.PollIntervalMs) - elapsed;

                if (sleepTime > TimeSpan.Zero)
                {
                    await Task.Delay(sleepTime, cancellationToken);
                }
                else
                {
                    _logger.LogWarning("Data acquisition took {ElapsedMs}ms, longer than interval {IntervalMs}ms", 
                        elapsed.TotalMilliseconds, _config.PollIntervalMs);
                }
            }
        }

        private async Task ReadDeviceDataAsync(IModbusDeviceManager manager, CancellationToken cancellationToken)
        {
            var deviceConfig = manager.Configuration;
            var timestamp = DateTimeOffset.UtcNow;

            foreach (var channel in deviceConfig.Channels.Where(c => c.Enabled))
            {
                try
                {
                    var result = await manager.ReadRegistersAsync(
                        channel.StartRegister, 
                        (ushort)channel.RegisterCount, 
                        cancellationToken);

                    if (result.Success && result.Data != null)
                    {
                        var reading = _dataProcessor.ProcessRawData(
                            deviceConfig.DeviceId, 
                            channel, 
                            result.Data, 
                            timestamp, 
                            result.Duration);

                        _dataSubject.OnNext(reading);
                        UpdateDeviceHealth(deviceConfig.DeviceId, true);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to read channel {Channel} from device {DeviceId}: {Error}", 
                            channel.ChannelNumber, deviceConfig.DeviceId, result.Error?.Message);
                        UpdateDeviceHealth(deviceConfig.DeviceId, false, result.Error?.Message);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading channel {Channel} from device {DeviceId}", 
                        channel.ChannelNumber, deviceConfig.DeviceId);
                    UpdateDeviceHealth(deviceConfig.DeviceId, false, ex.Message);
                }
            }
        }

        private async Task RunHealthCheckAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_config.HealthCheckIntervalMs, cancellationToken);

                    foreach (var manager in _deviceManagers.Values)
                    {
                        try
                        {
                            var isHealthy = await manager.TestConnectionAsync(cancellationToken);
                            UpdateDeviceHealth(manager.DeviceId, isHealthy);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Health check failed for device {DeviceId}", manager.DeviceId);
                            UpdateDeviceHealth(manager.DeviceId, false, ex.Message);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancelling
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in health check loop");
                }
            }
        }

        private void UpdateDeviceHealth(string deviceId, bool success, string? errorMessage = null)
        {
            if (_deviceHealth.TryGetValue(deviceId, out var currentHealth))
            {
                var newHealth = currentHealth with
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    IsConnected = success,
                    Status = success ? DeviceStatus.Online : DeviceStatus.Error,
                    ConsecutiveFailures = success ? 0 : currentHealth.ConsecutiveFailures + 1,
                    LastError = errorMessage,
                    TotalReads = currentHealth.TotalReads + 1,
                    SuccessfulReads = success ? currentHealth.SuccessfulReads + 1 : currentHealth.SuccessfulReads,
                    LastSuccessfulRead = success ? DateTimeOffset.UtcNow - currentHealth.Timestamp : currentHealth.LastSuccessfulRead
                };

                _deviceHealth[deviceId] = newHealth;
                _healthSubject.OnNext(newHealth);
            }
        }

        public Task<AdamDeviceHealth?> GetDeviceHealthAsync(string deviceId)
        {
            _deviceHealth.TryGetValue(deviceId, out var health);
            return Task.FromResult(health);
        }

        public Task<IReadOnlyList<AdamDeviceHealth>> GetAllDeviceHealthAsync()
        {
            var healthList = _deviceHealth.Values.ToList();
            return Task.FromResult<IReadOnlyList<AdamDeviceHealth>>(healthList);
        }

        public Task AddDeviceAsync(AdamDeviceConfig deviceConfig)
        {
            // Runtime device addition logic would go here
            throw new NotImplementedException("Runtime device addition not yet implemented");
        }

        public Task RemoveDeviceAsync(string deviceId)
        {
            // Runtime device removal logic would go here
            throw new NotImplementedException("Runtime device removal not yet implemented");
        }

        public Task UpdateDeviceConfigAsync(AdamDeviceConfig deviceConfig)
        {
            // Runtime device config update logic would go here
            throw new NotImplementedException("Runtime device config update not yet implemented");
        }

        // IHostedService implementation
        Task IHostedService.StartAsync(CancellationToken cancellationToken) => StartAsync(cancellationToken);
        Task IHostedService.StopAsync(CancellationToken cancellationToken) => StopAsync(cancellationToken);

        // IHealthCheck implementation
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            if (!IsRunning)
                return Task.FromResult(HealthCheckResult.Unhealthy("Service is not running"));

            var healthyDevices = _deviceHealth.Values.Count(h => h.Status == DeviceStatus.Online);
            var totalDevices = _deviceHealth.Count;

            if (healthyDevices == 0)
                return Task.FromResult(HealthCheckResult.Unhealthy($"No devices are online (0/{totalDevices})"));

            if (healthyDevices < totalDevices)
                return Task.FromResult(HealthCheckResult.Degraded($"Some devices are offline ({healthyDevices}/{totalDevices})"));

            return Task.FromResult(HealthCheckResult.Healthy($"All devices are online ({healthyDevices}/{totalDevices})"));
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            
            foreach (var manager in _deviceManagers.Values)
                manager.Dispose();

            _dataSubject.Dispose();
            _healthSubject.Dispose();
            _cancellationTokenSource.Dispose();
        }
    }

    #endregion

    #region Extension Methods for DI Registration

    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Register the ADAM Logger as a reusable service
        /// </summary>
        public static IServiceCollection AddAdamLogger(this IServiceCollection services, Action<AdamLoggerConfig> configureOptions)
        {
            services.Configure(configureOptions);
            
            // Register core services
            services.AddSingleton<IDataValidator, DefaultDataValidator>();
            services.AddSingleton<IDataTransformer, DefaultDataTransformer>();
            services.AddSingleton<IDataProcessor, DefaultDataProcessor>();
            services.AddSingleton<IAdamLoggerService, AdamLoggerService>();
            
            // Register as hosted service for automatic start/stop
            services.AddHostedService<AdamLoggerService>(provider => 
                (AdamLoggerService)provider.GetRequiredService<IAdamLoggerService>());
            
            // Register health check
            services.AddHealthChecks()
                .AddCheck<AdamLoggerService>("adam_logger");

            return services;
        }

        /// <summary>
        /// Register custom data processor for application-specific logic
        /// </summary>
        public static IServiceCollection AddCustomDataProcessor<T>(this IServiceCollection services) 
            where T : class, IDataProcessor
        {
            services.AddSingleton<IDataProcessor, T>();
            return services;
        }

        /// <summary>
        /// Register custom data validator for application-specific validation
        /// </summary>
        public static IServiceCollection AddCustomDataValidator<T>(this IServiceCollection services) 
            where T : class, IDataValidator
        {
            services.AddSingleton<IDataValidator, T>();
            return services;
        }

        /// <summary>
        /// Register custom data transformer for application-specific transformations
        /// </summary>
        public static IServiceCollection AddCustomDataTransformer<T>(this IServiceCollection services) 
            where T : class, IDataTransformer
        {
            services.AddSingleton<IDataTransformer, T>();
            return services;
        }
    }

    #endregion
}