// Industrial.Adam.Logger.Tests - Test Configuration Builder
// Accurate configuration builders matching actual model structures

using Industrial.Adam.Logger.Configuration;
using Bogus;

namespace Industrial.Adam.Logger.Tests.TestHelpers;

/// <summary>
/// Builder class for creating test configurations that match actual model structures
/// </summary>
public static class TestConfigurationBuilder
{
    private static readonly Faker _faker = new();

    /// <summary>
    /// Create a valid AdamDeviceConfig for testing
    /// </summary>
    /// <param name="deviceId">Optional device ID override</param>
    /// <param name="channelCount">Number of channels to create</param>
    /// <returns>Valid device configuration</returns>
    public static AdamDeviceConfig ValidDeviceConfig(string? deviceId = null, int channelCount = 2)
    {
        var config = new AdamDeviceConfig
        {
            // REQUIRED properties
            DeviceId = deviceId ?? $"TEST_DEVICE_{_faker.Random.AlphaNumeric(6)}",
            IpAddress = _faker.Internet.Ip(),
            
            // OPTIONAL properties with defaults
            Port = Constants.DefaultModbusPort,
            UnitId = Constants.MinModbusUnitId,
            TimeoutMs = Constants.DefaultDeviceTimeoutMs,
            MaxRetries = Constants.DefaultMaxRetries,
            RetryDelayMs = Constants.DefaultRetryDelayMs,
            
            // Advanced connection settings
            KeepAlive = true,
            EnableNagle = false,
            ReceiveBufferSize = Constants.DefaultReceiveBufferSize,
            SendBufferSize = Constants.DefaultSendBufferSize,
            
            // Data processing options
            EnableRateCalculation = true,
            RateWindowSeconds = Constants.DefaultRateWindowSeconds,
            EnableDataValidation = true,
            OverflowThreshold = Constants.DefaultOverflowThreshold,
            
            // Collections
            Channels = new List<ChannelConfig>(),
            Tags = new Dictionary<string, object>
            {
                { "environment", "test" },
                { "location", "test_facility" },
                { "line", "test_line_001" }
            }
        };

        // Add channels
        for (int i = 0; i < channelCount; i++)
        {
            config.Channels.Add(ValidChannelConfig(i));
        }

        return config;
    }

    /// <summary>
    /// Create a valid ChannelConfig for testing
    /// </summary>
    /// <param name="channelNumber">Channel number (0-based)</param>
    /// <param name="name">Optional channel name</param>
    /// <returns>Valid channel configuration</returns>
    public static ChannelConfig ValidChannelConfig(int channelNumber = 0, string? name = null)
    {
        return new ChannelConfig
        {
            // REQUIRED properties
            ChannelNumber = channelNumber,
            Name = name ?? $"Channel_{channelNumber:D2}",
            
            // OPTIONAL properties with defaults
            Description = $"Test channel {channelNumber} for counter data",
            Enabled = true,
            
            // Modbus register configuration
            StartRegister = (ushort)(channelNumber * 10), // Non-overlapping registers
            RegisterCount = Constants.CounterRegisterCount,
            
            // Data processing configuration
            ScaleFactor = 1.0,
            Offset = 0.0,
            Unit = DefaultUnits.Counts,
            DecimalPlaces = Constants.DefaultDecimalPlaces,
            
            // Validation limits (optional)
            MinValue = 0,
            MaxValue = 1000000,
            MaxRateOfChange = 1000.0,
            
            // Tags
            Tags = new Dictionary<string, object>
            {
                { "channel_type", "counter" },
                { "measurement_type", "totalizer" },
                { "data_type", "accumulator" }
            }
        };
    }

    /// <summary>
    /// Create a valid AdamLoggerConfig for testing
    /// </summary>
    /// <param name="deviceCount">Number of devices to create</param>
    /// <returns>Valid logger configuration</returns>
    public static AdamLoggerConfig ValidLoggerConfig(int deviceCount = 2)
    {
        var config = new AdamLoggerConfig
        {
            // Timing configuration
            PollIntervalMs = Constants.DefaultPollIntervalMs,
            HealthCheckIntervalMs = Constants.DefaultHealthCheckIntervalMs,
            
            // Performance configuration
            MaxConcurrentDevices = Constants.DefaultMaxConcurrentDevices,
            DataBufferSize = Constants.DefaultDataBufferSize,
            BatchSize = Constants.DefaultBatchSize,
            BatchTimeoutMs = Constants.DefaultBatchTimeoutMs,
            
            // Error handling configuration
            EnableAutomaticRecovery = true,
            MaxConsecutiveFailures = Constants.DefaultMaxConsecutiveFailures,
            DeviceTimeoutMinutes = Constants.DefaultDeviceTimeoutMinutes,
            
            // Monitoring and diagnostics
            EnablePerformanceCounters = true,
            EnableDetailedLogging = false,
            
            // Devices collection
            Devices = new List<AdamDeviceConfig>()
        };

        // Add devices
        for (int i = 0; i < deviceCount; i++)
        {
            config.Devices.Add(ValidDeviceConfig($"DEVICE_{i:D3}"));
        }

        return config;
    }

    /// <summary>
    /// Create a device config with invalid settings for testing validation
    /// </summary>
    /// <param name="errorType">Type of validation error to create</param>
    /// <returns>Invalid device configuration</returns>
    public static AdamDeviceConfig InvalidDeviceConfig(string errorType = "empty_device_id")
    {
        var config = ValidDeviceConfig();
        
        switch (errorType)
        {
            case "empty_device_id":
                config.DeviceId = string.Empty;
                break;
            case "invalid_ip":
                config.IpAddress = "invalid.ip.address";
                break;
            case "invalid_port":
                config.Port = 0;
                break;
            case "invalid_unit_id":
                config.UnitId = 0;
                break;
            case "invalid_timeout":
                config.TimeoutMs = 0;
                break;
            case "invalid_retries":
                config.MaxRetries = -1;
                break;
            case "no_channels":
                config.Channels = new List<ChannelConfig>();
                break;
            case "duplicate_channels":
                config.Channels = new List<ChannelConfig> 
                { 
                    ValidChannelConfig(1, "Channel_1"),
                    ValidChannelConfig(1, "Channel_1_Duplicate") // Same channel number
                };
                break;
            case "long_device_id":
                config.DeviceId = new string('A', Constants.MaxDeviceIdLength + 1);
                break;
            case "invalid_receive_buffer":
                config.ReceiveBufferSize = 500; // Below minimum
                break;
            case "invalid_send_buffer":
                config.SendBufferSize = 100000; // Above maximum
                break;
            case "invalid_rate_window":
                config.RateWindowSeconds = 5; // Below minimum
                break;
            case "invalid_overflow_threshold":
                config.OverflowThreshold = 500; // Below minimum
                break;
        }
        
        return config;
    }

    /// <summary>
    /// Create a channel config with invalid settings for testing validation
    /// </summary>
    /// <param name="errorType">Type of validation error to create</param>
    /// <returns>Invalid channel configuration</returns>
    public static ChannelConfig InvalidChannelConfig(string errorType = "empty_name")
    {
        var config = ValidChannelConfig();
        
        switch (errorType)
        {
            case "empty_name":
                config.Name = string.Empty;
                break;
            case "long_name":
                config.Name = new string('A', Constants.MaxChannelNameLength + 1);
                break;
            case "invalid_channel_number":
                config.ChannelNumber = -1;
                break;
            case "invalid_register_count":
                config.RegisterCount = 0;
                break;
            case "zero_scale_factor":
                config.ScaleFactor = 0.0;
                break;
            case "invalid_decimal_places":
                config.DecimalPlaces = -1;
                break;
            case "invalid_min_max_range":
                config.MinValue = 1000;
                config.MaxValue = 100; // Min > Max
                break;
            case "invalid_rate_limit":
                config.MaxRateOfChange = -1.0;
                break;
            case "register_overflow":
                config.StartRegister = 65530;
                config.RegisterCount = 10; // Exceeds max address
                break;
            case "high_register_address":
                config.StartRegister = 65530; // Near max to trigger validation
                break;
            case "high_channel_number":
                config.ChannelNumber = 300; // Above max
                break;
        }
        
        return config;
    }

    /// <summary>
    /// Create a logger config with invalid settings for testing validation
    /// </summary>
    /// <param name="errorType">Type of validation error to create</param>
    /// <returns>Invalid logger configuration</returns>
    public static AdamLoggerConfig InvalidLoggerConfig(string errorType = "no_devices")
    {
        var config = ValidLoggerConfig();
        
        switch (errorType)
        {
            case "no_devices":
                config.Devices = new List<AdamDeviceConfig>();
                break;
            case "invalid_poll_interval":
                config.PollIntervalMs = 50; // Below minimum
                break;
            case "invalid_health_check_interval":
                config.HealthCheckIntervalMs = 1000; // Below minimum
                break;
            case "invalid_max_concurrent":
                config.MaxConcurrentDevices = 0;
                break;
            case "invalid_buffer_size":
                config.DataBufferSize = 50; // Below minimum
                break;
            case "invalid_batch_size":
                config.BatchSize = 0;
                break;
            case "invalid_batch_timeout":
                config.BatchTimeoutMs = 50; // Below minimum
                break;
            case "invalid_consecutive_failures":
                config.MaxConsecutiveFailures = 0;
                break;
            case "invalid_device_timeout":
                config.DeviceTimeoutMinutes = 0;
                break;
            case "duplicate_device_ids":
                config.Devices = new List<AdamDeviceConfig> 
                { 
                    ValidDeviceConfig("DUPLICATE_DEVICE"),
                    ValidDeviceConfig("DUPLICATE_DEVICE") // Same device ID
                };
                break;
            case "too_many_devices":
                config.MaxConcurrentDevices = 2;
                config.Devices = Enumerable.Range(0, 5)
                    .Select(i => ValidDeviceConfig($"DEVICE_{i:D3}"))
                    .ToList();
                break;
            case "short_poll_interval":
                config.PollIntervalMs = 100;
                config.Devices = Enumerable.Range(0, 3)
                    .Select(i => ValidDeviceConfig($"DEVICE_{i:D3}", 10)) // Many channels
                    .ToList();
                break;
        }
        
        return config;
    }

    /// <summary>
    /// Create a collection of valid device configs with different characteristics
    /// </summary>
    /// <param name="count">Number of devices to create</param>
    /// <returns>Collection of diverse device configurations</returns>
    public static List<AdamDeviceConfig> DiverseDeviceConfigs(int count = 3)
    {
        var configs = new List<AdamDeviceConfig>();
        
        for (int i = 0; i < count; i++)
        {
            var config = ValidDeviceConfig($"DIVERSE_DEVICE_{i:D3}", (i % 4) + 1); // 1-4 channels
            
            // Vary some settings
            config.Port = Constants.DefaultModbusPort + i;
            config.UnitId = (byte)(Constants.MinModbusUnitId + i);
            config.TimeoutMs = Constants.DefaultDeviceTimeoutMs + (i * 1000);
            config.EnableRateCalculation = i % 2 == 0;
            config.EnableDataValidation = i % 3 != 0;
            
            // Add device-specific tags
            config.Tags.Add("device_index", i);
            config.Tags.Add("test_variant", $"variant_{i}");
            
            configs.Add(config);
        }
        
        return configs;
    }

    /// <summary>
    /// Create a minimal valid device config for performance testing
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <returns>Minimal valid device configuration</returns>
    public static AdamDeviceConfig MinimalDeviceConfig(string deviceId = "MINIMAL_DEVICE")
    {
        return new AdamDeviceConfig
        {
            DeviceId = deviceId,
            IpAddress = "192.168.1.100",
            Channels = new List<ChannelConfig>
            {
                new ChannelConfig
                {
                    ChannelNumber = 0,
                    Name = "MinimalChannel"
                }
            }
        };
    }

    /// <summary>
    /// Create a comprehensive device config with all optional features enabled
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <returns>Comprehensive device configuration</returns>
    public static AdamDeviceConfig ComprehensiveDeviceConfig(string deviceId = "COMPREHENSIVE_DEVICE")
    {
        var config = ValidDeviceConfig(deviceId, 4);
        
        // Enable all features
        config.EnableRateCalculation = true;
        config.EnableDataValidation = true;
        config.KeepAlive = true;
        
        // Clear existing tags and add comprehensive tags
        config.Tags.Clear();
        config.Tags.Add("plant", "test_plant_001");
        config.Tags.Add("area", "production_area_A");
        config.Tags.Add("line", "line_01");
        config.Tags.Add("station", "station_05");
        config.Tags.Add("shift", "day_shift");
        config.Tags.Add("operator", "test_operator");
        
        // Configure channels with different settings
        for (int i = 0; i < config.Channels.Count; i++)
        {
            var channel = config.Channels[i];
            channel.ScaleFactor = 1.0 + (i * 0.1);
            channel.Offset = i * 10.0;
            channel.DecimalPlaces = i % 3;
            channel.MinValue = i * 100;
            channel.MaxValue = (i + 1) * 10000;
            channel.MaxRateOfChange = (i + 1) * 100.0;
            
            channel.Tags.Add("channel_purpose", $"measurement_{i + 1}");
            channel.Tags.Add("sensor_type", i % 2 == 0 ? "counter" : "rate");
        }
        
        return config;
    }
}