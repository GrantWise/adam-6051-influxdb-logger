// Industrial.Adam.ScaleLogger.Tests - Test Configuration Builder
// Adapted from ADAM-6051 patterns for scale logger configurations

using Industrial.Adam.ScaleLogger.Configuration;
using Industrial.Adam.ScaleLogger.Models;
using Bogus;

namespace Industrial.Adam.ScaleLogger.Tests.TestHelpers;

/// <summary>
/// Builder class for creating test configurations that match actual scale logger model structures
/// </summary>
public static class TestConfigurationBuilder
{
    private static readonly Faker _faker = new();

    /// <summary>
    /// Create a valid ScaleDeviceConfig for testing
    /// </summary>
    /// <param name="deviceId">Optional device ID override</param>
    /// <param name="channelCount">Number of channels to create</param>
    /// <returns>Valid scale device configuration</returns>
    public static ScaleDeviceConfig ValidScaleDeviceConfig(string? deviceId = null, int channelCount = 2)
    {
        return new ScaleDeviceConfig
        {
            // REQUIRED properties
            DeviceId = deviceId ?? $"SCALE_{_faker.Random.AlphaNumeric(6)}",
            Name = $"Test Scale {_faker.Random.AlphaNumeric(3)}",
            Host = _faker.Internet.Ip(),
            
            // OPTIONAL properties with defaults
            Location = $"Test Location {_faker.Random.AlphaNumeric(3)}",
            Port = 502,
            TimeoutMs = 5000,
            Channels = Enumerable.Range(1, channelCount).ToList(),
            ProtocolTemplate = null, // Will be auto-discovered
            Manufacturer = _faker.PickRandom("Mettler Toledo", "Sartorius", "Ohaus", "A&D Weighing"),
            Model = $"Model_{_faker.Random.AlphaNumeric(4)}"
        };
    }

    /// <summary>
    /// Create a valid Adam4571Config for testing
    /// </summary>
    /// <param name="deviceCount">Number of devices to create</param>
    /// <returns>Valid scale logger configuration</returns>
    public static Adam4571Config ValidScaleLoggerConfig(int deviceCount = 2)
    {
        var config = new Adam4571Config
        {
            // Timing configuration
            PollIntervalMs = 5000,
            HealthCheckIntervalMs = 30000,
            MaxRetryAttempts = 3,
            RetryDelayMs = 1000,
            
            // InfluxDB configuration
            InfluxDb = ValidInfluxDbConfig(),
            
            // Protocol discovery configuration
            Discovery = ValidProtocolDiscoveryConfig(),
            
            // Devices collection
            Devices = new List<ScaleDeviceConfig>()
        };

        // Add devices
        for (int i = 0; i < deviceCount; i++)
        {
            config.Devices.Add(ValidScaleDeviceConfig($"SCALE_{i:D3}"));
        }

        return config;
    }

    /// <summary>
    /// Create a valid InfluxDbConfig for testing
    /// </summary>
    /// <param name="bucket">Optional bucket name override</param>
    /// <returns>Valid InfluxDB configuration</returns>
    public static InfluxDbConfig ValidInfluxDbConfig(string? bucket = null)
    {
        return new InfluxDbConfig
        {
            Url = "http://localhost:8086",
            Token = "test-token-" + _faker.Random.AlphaNumeric(32),
            Organization = "test-organization",
            Bucket = bucket ?? $"test-bucket-{_faker.Random.AlphaNumeric(6)}",
            BatchSize = 100,
            FlushIntervalMs = 5000
        };
    }

    /// <summary>
    /// Create a valid ProtocolDiscoveryConfig for testing
    /// </summary>
    /// <returns>Valid protocol discovery configuration</returns>
    public static ProtocolDiscoveryConfig ValidProtocolDiscoveryConfig()
    {
        return new ProtocolDiscoveryConfig
        {
            Enabled = true,
            TimeoutSeconds = 60,
            TemplatesPath = "./Templates",
            ValidationReadings = 5
        };
    }

    /// <summary>
    /// Create a valid ProtocolTemplate for testing
    /// </summary>
    /// <param name="templateId">Optional template ID override</param>
    /// <returns>Valid protocol template</returns>
    public static ProtocolTemplate ValidProtocolTemplate(string? templateId = null)
    {
        return new ProtocolTemplate
        {
            Id = templateId ?? $"test-protocol-{_faker.Random.AlphaNumeric(6)}",
            Name = $"Test Protocol {_faker.Random.AlphaNumeric(3)}",
            Manufacturer = _faker.PickRandom("Mettler Toledo", "Sartorius", "Ohaus"),
            Description = $"Test protocol for {_faker.Random.Words(3)}",
            Commands = new List<string> { "W\\r\\n", "P\\r\\n", "T\\r\\n" },
            ExpectedResponses = new List<string> { "ST,\\w*,[\\+\\-]?\\d+\\.?\\d*" },
            WeightPattern = "([\\+\\-]?\\d+\\.?\\d*)",
            Unit = "kg"
        };
    }

    /// <summary>
    /// Create a scale device config with invalid settings for testing validation
    /// </summary>
    /// <param name="errorType">Type of validation error to create</param>
    /// <returns>Invalid scale device configuration</returns>
    public static ScaleDeviceConfig InvalidScaleDeviceConfig(string errorType = "empty_device_id")
    {
        var config = ValidScaleDeviceConfig();
        
        switch (errorType)
        {
            case "empty_device_id":
                config.DeviceId = string.Empty;
                break;
            case "empty_name":
                config.Name = string.Empty;
                break;
            case "invalid_host":
                config.Host = "invalid.host.address";
                break;
            case "invalid_port":
                config.Port = 0;
                break;
            case "port_too_high":
                config.Port = 70000;
                break;
            case "invalid_timeout":
                config.TimeoutMs = 0;
                break;
            case "timeout_too_high":
                config.TimeoutMs = 50000;
                break;
            case "no_channels":
                config.Channels = new List<int>();
                break;
            case "duplicate_channels":
                config.Channels = new List<int> { 1, 1, 2 }; // Duplicate channel 1
                break;
            case "channel_out_of_range":
                config.Channels = new List<int> { 1, 50 }; // Invalid channel number
                break;
        }
        
        return config;
    }

    /// <summary>
    /// Create an InfluxDB config with invalid settings for testing validation
    /// </summary>
    /// <param name="errorType">Type of validation error to create</param>
    /// <returns>Invalid InfluxDB configuration</returns>
    public static InfluxDbConfig InvalidInfluxDbConfig(string errorType = "empty_url")
    {
        var config = ValidInfluxDbConfig();
        
        switch (errorType)
        {
            case "empty_url":
                config.Url = string.Empty;
                break;
            case "invalid_url":
                config.Url = "not-a-valid-url";
                break;
            case "empty_token":
                config.Token = string.Empty;
                break;
            case "empty_organization":
                config.Organization = string.Empty;
                break;
            case "empty_bucket":
                config.Bucket = string.Empty;
                break;
            case "invalid_batch_size":
                config.BatchSize = 0;
                break;
            case "batch_size_too_large":
                config.BatchSize = 2000;
                break;
            case "invalid_flush_interval":
                config.FlushIntervalMs = 0;
                break;
            case "flush_interval_too_small":
                config.FlushIntervalMs = 100;
                break;
        }
        
        return config;
    }

    /// <summary>
    /// Create a logger config with invalid settings for testing validation
    /// </summary>
    /// <param name="errorType">Type of validation error to create</param>
    /// <returns>Invalid scale logger configuration</returns>
    public static Adam4571Config InvalidScaleLoggerConfig(string errorType = "no_devices")
    {
        var config = ValidScaleLoggerConfig();
        
        switch (errorType)
        {
            case "no_devices":
                config.Devices = new List<ScaleDeviceConfig>();
                break;
            case "invalid_poll_interval":
                config.PollIntervalMs = 100; // Too fast
                break;
            case "poll_interval_too_slow":
                config.PollIntervalMs = 300000; // Too slow (> 5 minutes)
                break;
            case "invalid_health_check_interval":
                config.HealthCheckIntervalMs = 1000; // Too fast
                break;
            case "invalid_retry_attempts":
                config.MaxRetryAttempts = 0;
                break;
            case "retry_attempts_too_high":
                config.MaxRetryAttempts = 20;
                break;
            case "invalid_retry_delay":
                config.RetryDelayMs = 50; // Too fast
                break;
            case "duplicate_device_ids":
                config.Devices = new List<ScaleDeviceConfig> 
                { 
                    ValidScaleDeviceConfig("DUPLICATE_DEVICE"),
                    ValidScaleDeviceConfig("DUPLICATE_DEVICE") // Same device ID
                };
                break;
            case "too_many_devices":
                config.Devices = Enumerable.Range(0, 100)
                    .Select(i => ValidScaleDeviceConfig($"SCALE_{i:D3}"))
                    .ToList();
                break;
        }
        
        return config;
    }

    /// <summary>
    /// Create a collection of valid scale device configs with different characteristics
    /// </summary>
    /// <param name="count">Number of devices to create</param>
    /// <returns>Collection of diverse scale device configurations</returns>
    public static List<ScaleDeviceConfig> DiverseScaleDeviceConfigs(int count = 3)
    {
        var configs = new List<ScaleDeviceConfig>();
        
        for (int i = 0; i < count; i++)
        {
            var config = ValidScaleDeviceConfig($"DIVERSE_SCALE_{i:D3}", (i % 4) + 1); // 1-4 channels
            
            // Vary some settings
            config.Port = 502 + i;
            config.TimeoutMs = 5000 + (i * 1000);
            config.Location = $"Test Location {i}";
            
            // Vary manufacturers
            var manufacturers = new[] { "Mettler Toledo", "Sartorius", "Ohaus", "A&D Weighing" };
            config.Manufacturer = manufacturers[i % manufacturers.Length];
            config.Model = $"Model_{i:D2}_{_faker.Random.AlphaNumeric(3)}";
            
            configs.Add(config);
        }
        
        return configs;
    }

    /// <summary>
    /// Create a minimal valid scale device config for performance testing
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <returns>Minimal valid scale device configuration</returns>
    public static ScaleDeviceConfig MinimalScaleDeviceConfig(string deviceId = "MINIMAL_SCALE")
    {
        return new ScaleDeviceConfig
        {
            DeviceId = deviceId,
            Name = "Minimal Test Scale",
            Host = "192.168.1.100",
            Port = 502,
            Channels = new List<int> { 1 }
        };
    }

    /// <summary>
    /// Create a comprehensive scale device config with all optional features
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <returns>Comprehensive scale device configuration</returns>
    public static ScaleDeviceConfig ComprehensiveScaleDeviceConfig(string deviceId = "COMPREHENSIVE_SCALE")
    {
        return new ScaleDeviceConfig
        {
            DeviceId = deviceId,
            Name = "Comprehensive Test Scale",
            Location = "Production Line A - Station 5",
            Host = "192.168.1.150",
            Port = 502,
            TimeoutMs = 10000,
            Channels = new List<int> { 1, 2, 3, 4 },
            ProtocolTemplate = "mettler-toledo-standard",
            Manufacturer = "Mettler Toledo",
            Model = "IND780-Comprehensive"
        };
    }

    /// <summary>
    /// Create test scale data reading
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="weight">Weight value</param>
    /// <param name="quality">Data quality</param>
    /// <returns>Test scale data reading</returns>
    public static ScaleDataReading ValidScaleDataReading(
        string deviceId = "TEST_SCALE", 
        double weight = 1234.56, 
        DataQuality quality = DataQuality.Good)
    {
        return new ScaleDataReading
        {
            DeviceId = deviceId,
            DeviceName = $"Test Scale for {deviceId}",
            Channel = 1,
            Timestamp = DateTimeOffset.UtcNow,
            WeightValue = weight,
            RawValue = $"ST,GS,{weight:F2}",
            Unit = "kg",
            StandardizedWeightKg = (decimal)weight,
            IsStable = quality == DataQuality.Good,
            Quality = quality,
            AcquisitionTime = TimeSpan.FromMilliseconds(_faker.Random.Int(50, 500)),
            Manufacturer = "Test Manufacturer",
            Model = "Test Model",
            ProtocolTemplate = "test-protocol",
            Status = quality == DataQuality.Good ? "OK" : "ERROR",
            Metadata = new Dictionary<string, object>
            {
                { "test_reading", true },
                { "simulation", true }
            }
        };
    }

    /// <summary>
    /// Create test scale device health
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="status">Device status</param>
    /// <returns>Test scale device health</returns>
    public static ScaleDeviceHealth ValidScaleDeviceHealth(
        string deviceId = "TEST_SCALE", 
        DeviceStatus status = DeviceStatus.Online)
    {
        return new ScaleDeviceHealth
        {
            DeviceId = deviceId,
            DeviceName = $"Test Scale {deviceId}",
            Timestamp = DateTimeOffset.UtcNow,
            Status = status,
            IsConnected = status == DeviceStatus.Online,
            LastSuccessfulRead = status == DeviceStatus.Online ? TimeSpan.FromSeconds(5) : null,
            ConsecutiveFailures = status == DeviceStatus.Online ? 0 : 3,
            CommunicationLatency = status == DeviceStatus.Online ? 150.0 : 5000.0,
            LastError = status == DeviceStatus.Online ? null : "Communication timeout",
            TotalReads = 1000,
            SuccessfulReads = status == DeviceStatus.Online ? 995 : 500,
            ProtocolTemplate = "test-protocol",
            Diagnostics = new Dictionary<string, object>
            {
                { "uptime", TimeSpan.FromHours(24).TotalSeconds },
                { "last_calibration", DateTimeOffset.UtcNow.AddDays(-30) },
                { "firmware_version", "1.2.3" }
            }
        };
    }
}