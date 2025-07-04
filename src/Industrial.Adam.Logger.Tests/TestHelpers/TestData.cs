// Industrial.Adam.Logger.Tests - Test Data Generator
// Accurate test data matching actual model structures

using Industrial.Adam.Logger.Models;
using Bogus;

namespace Industrial.Adam.Logger.Tests.TestHelpers;

/// <summary>
/// Generator class for creating consistent test data matching actual model structures
/// </summary>
public static class TestData
{
    private static readonly Faker _faker = new();

    /// <summary>
    /// Create a valid Adam data reading matching the actual record structure
    /// </summary>
    /// <param name="deviceId">Optional device ID override</param>
    /// <param name="channel">Optional channel override</param>
    /// <returns>Valid AdamDataReading matching actual structure</returns>
    public static AdamDataReading ValidReading(string? deviceId = null, int? channel = null)
    {
        return new AdamDataReading
        {
            // REQUIRED properties
            DeviceId = deviceId ?? "TEST_DEVICE_001",
            Channel = channel ?? 1,
            RawValue = _faker.Random.Long(0, 1000000),
            Timestamp = DateTimeOffset.UtcNow,
            
            // OPTIONAL properties
            ProcessedValue = _faker.Random.Double(0, 1000),
            Rate = _faker.Random.Double(0, 100),
            Quality = DataQuality.Good,
            Unit = "counts",
            AcquisitionTime = TimeSpan.FromMilliseconds(_faker.Random.Double(1, 100)),
            Tags = new Dictionary<string, object>
            {
                { "sensor_type", "counter" },
                { "location", "test_facility" },
                { "data_source", "modbus" }
            },
            ErrorMessage = null
        };
    }

    /// <summary>
    /// Create a reading with specific quality for testing
    /// </summary>
    /// <param name="quality">Desired data quality</param>
    /// <returns>Reading with specified quality</returns>
    public static AdamDataReading ReadingWithQuality(DataQuality quality)
    {
        var reading = ValidReading();
        
        return reading with 
        { 
            Quality = quality,
            ProcessedValue = quality switch
            {
                DataQuality.Good => _faker.Random.Double(100, 900),
                DataQuality.Uncertain => _faker.Random.Double(900, 950),
                DataQuality.Bad => _faker.Random.Double(1000, 2000), // Out of range
                DataQuality.ConfigurationError => double.NaN,
                DataQuality.DeviceFailure => 0,
                DataQuality.Timeout => null,
                DataQuality.Overflow => double.MaxValue,
                _ => reading.ProcessedValue
            },
            ErrorMessage = quality switch
            {
                DataQuality.Bad => "Value out of range",
                DataQuality.ConfigurationError => "Invalid configuration",
                DataQuality.DeviceFailure => "Device communication failed",
                DataQuality.Timeout => "Read operation timed out",
                DataQuality.Overflow => "Counter overflow detected",
                _ => null
            }
        };
    }

    /// <summary>
    /// Create valid register data array for testing
    /// </summary>
    /// <param name="registerCount">Number of registers</param>
    /// <param name="pattern">Optional pattern for values</param>
    /// <returns>Array of register values</returns>
    public static ushort[] ValidRegisterData(int registerCount = 2, string? pattern = null)
    {
        return pattern switch
        {
            "incremental" => Enumerable.Range(1, registerCount).Select(i => (ushort)(i * 1000)).ToArray(),
            "alternating" => Enumerable.Range(0, registerCount).Select(i => (ushort)(i % 2 == 0 ? 0x1234 : 0x5678)).ToArray(),
            "maximum" => Enumerable.Repeat((ushort)0xFFFF, registerCount).ToArray(),
            "minimum" => Enumerable.Repeat((ushort)0x0000, registerCount).ToArray(),
            _ => Enumerable.Range(0, registerCount).Select(i => (ushort)_faker.Random.UShort()).ToArray()
        };
    }

    /// <summary>
    /// Create a healthy device status matching actual record structure
    /// </summary>
    /// <param name="deviceId">Optional device ID override</param>
    /// <returns>Healthy device status</returns>
    public static AdamDeviceHealth HealthyDevice(string? deviceId = null)
    {
        return new AdamDeviceHealth
        {
            // REQUIRED properties
            DeviceId = deviceId ?? "TEST_DEVICE_001",
            Timestamp = DateTimeOffset.UtcNow,
            Status = DeviceStatus.Online,
            
            // OPTIONAL properties
            IsConnected = true,
            LastSuccessfulRead = TimeSpan.FromSeconds(30),
            ConsecutiveFailures = 0,
            CommunicationLatency = _faker.Random.Double(10, 100),
            LastError = null,
            TotalReads = _faker.Random.Int(10000, 100000),
            SuccessfulReads = _faker.Random.Int(9800, 99900) // High success rate
        };
    }

    /// <summary>
    /// Create an unhealthy device status for testing
    /// </summary>
    /// <param name="deviceId">Optional device ID override</param>
    /// <param name="status">Specific unhealthy status</param>
    /// <returns>Unhealthy device status</returns>
    public static AdamDeviceHealth UnhealthyDevice(string? deviceId = null, DeviceStatus? status = null)
    {
        var deviceStatus = status ?? _faker.PickRandom(DeviceStatus.Offline, DeviceStatus.Error, DeviceStatus.Warning);
        var totalReads = _faker.Random.Int(1000, 10000);
        var successfulReads = deviceStatus switch
        {
            DeviceStatus.Offline => 0,
            DeviceStatus.Error => _faker.Random.Int(0, totalReads / 10), // Very low success
            DeviceStatus.Warning => _faker.Random.Int(totalReads / 2, totalReads * 3 / 4), // Moderate success
            _ => totalReads
        };
        
        return new AdamDeviceHealth
        {
            // REQUIRED properties
            DeviceId = deviceId ?? "TEST_DEVICE_001",
            Timestamp = DateTimeOffset.UtcNow,
            Status = deviceStatus,
            
            // OPTIONAL properties
            IsConnected = deviceStatus != DeviceStatus.Offline,
            LastSuccessfulRead = deviceStatus == DeviceStatus.Offline 
                ? TimeSpan.FromMinutes(10) 
                : TimeSpan.FromMinutes(1),
            ConsecutiveFailures = deviceStatus switch
            {
                DeviceStatus.Offline => _faker.Random.Int(10, 100),
                DeviceStatus.Error => _faker.Random.Int(5, 20),
                DeviceStatus.Warning => _faker.Random.Int(1, 5),
                _ => 0
            },
            CommunicationLatency = deviceStatus == DeviceStatus.Offline 
                ? null 
                : _faker.Random.Double(100, 5000),
            LastError = deviceStatus switch
            {
                DeviceStatus.Error => "Modbus exception: Function code not supported",
                DeviceStatus.Warning => "High response time detected",
                DeviceStatus.Offline => "Unable to establish TCP connection",
                _ => null
            },
            TotalReads = totalReads,
            SuccessfulReads = successfulReads
        };
    }

    /// <summary>
    /// Create readings with a specific pattern for rate calculation testing
    /// </summary>
    /// <param name="baseValue">Starting value</param>
    /// <param name="increment">Increment between readings</param>
    /// <param name="count">Number of readings</param>
    /// <param name="intervalSeconds">Seconds between readings</param>
    /// <returns>Readings with consistent rate pattern</returns>
    public static List<AdamDataReading> ReadingsWithRate(
        long baseValue, 
        long increment, 
        int count, 
        int intervalSeconds = 60)
    {
        var readings = new List<AdamDataReading>();
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-count);

        for (int i = 0; i < count; i++)
        {
            var rawValue = baseValue + (i * increment);
            var reading = ValidReading();
            
            readings.Add(reading with
            {
                RawValue = rawValue,
                ProcessedValue = rawValue,
                Timestamp = baseTime.AddSeconds(i * intervalSeconds)
            });
        }

        return readings;
    }

    /// <summary>
    /// Create readings that simulate counter overflow for testing
    /// </summary>
    /// <param name="overflowPoint">Point where overflow occurs</param>
    /// <returns>Readings showing counter overflow</returns>
    public static List<AdamDataReading> ReadingsWithOverflow(long overflowPoint = 0xFFFF)
    {
        var readings = new List<AdamDataReading>();
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-5);

        // Values before overflow
        readings.Add(ValidReading() with 
        { 
            RawValue = overflowPoint - 100,
            ProcessedValue = overflowPoint - 100,
            Timestamp = baseTime 
        });
        
        readings.Add(ValidReading() with 
        { 
            RawValue = overflowPoint - 50,
            ProcessedValue = overflowPoint - 50,
            Timestamp = baseTime.AddMinutes(1) 
        });
        
        // Overflow point
        readings.Add(ValidReading() with 
        { 
            RawValue = overflowPoint,
            ProcessedValue = overflowPoint,
            Timestamp = baseTime.AddMinutes(2),
            Quality = DataQuality.Overflow
        });
        
        // Values after overflow (reset to 0)
        readings.Add(ValidReading() with 
        { 
            RawValue = 50,
            ProcessedValue = 50,
            Timestamp = baseTime.AddMinutes(3) 
        });
        
        readings.Add(ValidReading() with 
        { 
            RawValue = 100,
            ProcessedValue = 100,
            Timestamp = baseTime.AddMinutes(4) 
        });

        return readings;
    }

    /// <summary>
    /// Create readings with boundary values for validation testing
    /// </summary>
    /// <param name="minValue">Minimum valid value</param>
    /// <param name="maxValue">Maximum valid value</param>
    /// <returns>Readings at boundary conditions</returns>
    public static List<AdamDataReading> BoundaryValueReadings(long minValue, long maxValue)
    {
        var baseReading = ValidReading();
        
        return new List<AdamDataReading>
        {
            // Below minimum
            baseReading with { RawValue = minValue - 1, ProcessedValue = minValue - 1, Quality = DataQuality.Bad },
            
            // At minimum
            baseReading with { RawValue = minValue, ProcessedValue = minValue, Quality = DataQuality.Good },
            
            // Just above minimum
            baseReading with { RawValue = minValue + 1, ProcessedValue = minValue + 1, Quality = DataQuality.Good },
            
            // Middle value
            baseReading with { RawValue = (minValue + maxValue) / 2, ProcessedValue = (minValue + maxValue) / 2, Quality = DataQuality.Good },
            
            // Just below maximum
            baseReading with { RawValue = maxValue - 1, ProcessedValue = maxValue - 1, Quality = DataQuality.Good },
            
            // At maximum
            baseReading with { RawValue = maxValue, ProcessedValue = maxValue, Quality = DataQuality.Good },
            
            // Above maximum
            baseReading with { RawValue = maxValue + 1, ProcessedValue = maxValue + 1, Quality = DataQuality.Bad }
        };
    }

    /// <summary>
    /// Create a collection of valid readings for testing
    /// </summary>
    /// <param name="count">Number of readings to create</param>
    /// <param name="deviceId">Optional device ID for all readings</param>
    /// <returns>Collection of valid readings</returns>
    public static List<AdamDataReading> ValidReadings(int count, string? deviceId = null)
    {
        var readings = new List<AdamDataReading>();
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-count);

        for (int i = 0; i < count; i++)
        {
            var reading = ValidReading(deviceId, (i % 4) + 1);
            readings.Add(reading with 
            { 
                Timestamp = baseTime.AddMinutes(i),
                RawValue = 1000 + (i * 100L), // Incremental values for rate calculation
                ProcessedValue = 1000 + (i * 100)
            });
        }

        return readings;
    }

    /// <summary>
    /// Create a set of readings for different devices
    /// </summary>
    /// <param name="deviceIds">Device IDs to create readings for</param>
    /// <param name="readingsPerDevice">Number of readings per device</param>
    /// <returns>Multi-device readings collection</returns>
    public static List<AdamDataReading> MultiDeviceReadings(IEnumerable<string> deviceIds, int readingsPerDevice = 5)
    {
        var allReadings = new List<AdamDataReading>();
        
        foreach (var deviceId in deviceIds)
        {
            allReadings.AddRange(ValidReadings(readingsPerDevice, deviceId));
        }
        
        return allReadings.OrderBy(r => r.Timestamp).ToList();
    }

    /// <summary>
    /// Create test tags dictionary
    /// </summary>
    /// <param name="includeStandard">Include standard industrial tags</param>
    /// <returns>Test tags dictionary</returns>
    public static Dictionary<string, object> TestTags(bool includeStandard = true)
    {
        var tags = new Dictionary<string, object>();

        if (includeStandard)
        {
            tags.Add("device_type", "adam-6051");
            tags.Add("production_line", "line_001");
            tags.Add("work_center", "station_05");
            tags.Add("shift", "day");
        }

        tags.Add("test_scenario", _faker.Lorem.Word());
        tags.Add("test_category", "unit_test");

        return tags;
    }
}