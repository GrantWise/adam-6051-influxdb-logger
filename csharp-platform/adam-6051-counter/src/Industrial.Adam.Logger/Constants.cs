// Industrial.Adam.Logger - Application Constants
// Eliminates magic numbers and provides centralized configuration values

namespace Industrial.Adam.Logger
{
    /// <summary>
    /// Application-wide constants to eliminate magic numbers and provide clear, maintainable values
    /// </summary>
    public static class Constants
    {
        #region Modbus Protocol Constants

        /// <summary>
        /// Number of bits in a 16-bit Modbus register
        /// </summary>
        public const int ModbusRegisterBits = 16;

        /// <summary>
        /// Maximum value for a 16-bit unsigned integer (2^16)
        /// </summary>
        public const int ModbusRegisterMaxValue = 65536;

        /// <summary>
        /// Default Modbus TCP port
        /// </summary>
        public const int DefaultModbusPort = 502;

        /// <summary>
        /// Starting address for ADAM device holding registers
        /// </summary>
        public const ushort AdamHoldingRegisterStartAddress = 40001;

        /// <summary>
        /// Number of registers required for a 32-bit counter value
        /// </summary>
        public const int CounterRegisterCount = 2;

        #endregion

        #region Timing and Retry Constants

        /// <summary>
        /// Default connection retry cooldown period in seconds
        /// </summary>
        public const int ConnectionRetryCooldownSeconds = 5;

        /// <summary>
        /// Default rate calculation window in minutes for historical data
        /// </summary>
        public const int DefaultRateCalculationWindowMinutes = 5;

        /// <summary>
        /// Default rate calculation window in seconds for configuration
        /// </summary>
        public const int DefaultRateWindowSeconds = 60;

        /// <summary>
        /// Default device polling interval in milliseconds
        /// </summary>
        public const int DefaultPollIntervalMs = 5000;

        /// <summary>
        /// Default health check interval in milliseconds
        /// </summary>
        public const int DefaultHealthCheckIntervalMs = 30000;

        /// <summary>
        /// Default device timeout in milliseconds
        /// </summary>
        public const int DefaultDeviceTimeoutMs = 3000;

        /// <summary>
        /// Default retry delay in milliseconds
        /// </summary>
        public const int DefaultRetryDelayMs = 1000;

        /// <summary>
        /// Default maximum number of retry attempts
        /// </summary>
        public const int DefaultMaxRetries = 3;

        /// <summary>
        /// Maximum retry delay in seconds for device operations
        /// </summary>
        public const int MaxRetryDelaySeconds = 30;

        /// <summary>
        /// Network operation retry delay in milliseconds
        /// </summary>
        public const int NetworkRetryDelayMs = 500;

        /// <summary>
        /// Maximum retry delay in seconds for network operations
        /// </summary>
        public const int MaxNetworkRetryDelaySeconds = 10;

        /// <summary>
        /// Default jitter factor for retry delays (0.0 to 1.0)
        /// </summary>
        public const double DefaultJitterFactor = 0.1;

        /// <summary>
        /// Default batch timeout in milliseconds
        /// </summary>
        public const int DefaultBatchTimeoutMs = 5000;

        #endregion

        #region Buffer and Performance Constants

        /// <summary>
        /// Default TCP receive buffer size in bytes
        /// </summary>
        public const int DefaultReceiveBufferSize = 8192;

        /// <summary>
        /// Default TCP send buffer size in bytes
        /// </summary>
        public const int DefaultSendBufferSize = 8192;

        /// <summary>
        /// Default data buffer size for in-memory storage
        /// </summary>
        public const int DefaultDataBufferSize = 10000;

        /// <summary>
        /// Default batch size for data processing
        /// </summary>
        public const int DefaultBatchSize = 100;

        /// <summary>
        /// Default maximum concurrent devices
        /// </summary>
        public const int DefaultMaxConcurrentDevices = 10;

        #endregion

        #region Validation and Limits Constants

        /// <summary>
        /// Default overflow threshold for 32-bit counters (near 2^32 max value)
        /// </summary>
        public const long DefaultOverflowThreshold = 4_294_000_000L;

        /// <summary>
        /// Maximum value for a 32-bit unsigned integer
        /// </summary>
        public const long UInt32MaxValue = 4_294_967_295L;

        /// <summary>
        /// Default maximum consecutive failures before marking device offline
        /// </summary>
        public const int DefaultMaxConsecutiveFailures = 5;

        /// <summary>
        /// Default device timeout in minutes for health monitoring
        /// </summary>
        public const int DefaultDeviceTimeoutMinutes = 5;

        /// <summary>
        /// Maximum allowed device ID length
        /// </summary>
        public const int MaxDeviceIdLength = 50;

        /// <summary>
        /// Maximum allowed channel name length
        /// </summary>
        public const int MaxChannelNameLength = 100;

        #endregion

        #region Port and Address Range Constants

        /// <summary>
        /// Minimum valid TCP port number
        /// </summary>
        public const int MinPortNumber = 1;

        /// <summary>
        /// Maximum valid TCP port number
        /// </summary>
        public const int MaxPortNumber = 65535;

        /// <summary>
        /// Minimum Modbus unit ID
        /// </summary>
        public const byte MinModbusUnitId = 1;

        /// <summary>
        /// Maximum Modbus unit ID
        /// </summary>
        public const byte MaxModbusUnitId = 255;

        /// <summary>
        /// Maximum Modbus register address
        /// </summary>
        public const ushort MaxModbusRegisterAddress = 65535;

        /// <summary>
        /// Maximum number of registers that can be read in a single Modbus operation
        /// </summary>
        public const ushort MaxModbusRegisterCount = 125;

        #endregion

        #region Performance and Monitoring Constants

        /// <summary>
        /// High defect rate threshold percentage for alerting
        /// </summary>
        public const double HighDefectRateThreshold = 5.0;

        /// <summary>
        /// Minimum time interval for performance counter updates in milliseconds
        /// </summary>
        public const int PerformanceCounterUpdateIntervalMs = 1000;

        /// <summary>
        /// Maximum time allowed for a single data acquisition cycle in milliseconds
        /// </summary>
        public const int MaxAcquisitionCycleTimeMs = 30000;

        /// <summary>
        /// Default coefficient of variation threshold for predictive maintenance
        /// </summary>
        public const double DefaultCoefficientOfVariationThreshold = 0.3;

        #endregion

        #region String Format Constants

        /// <summary>
        /// Standard timestamp format for logging and data export
        /// </summary>
        public const string StandardTimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";

        /// <summary>
        /// ISO 8601 timestamp format for international compatibility
        /// </summary>
        public const string Iso8601TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";

        /// <summary>
        /// Default decimal places for counter values (whole numbers)
        /// </summary>
        public const int DefaultDecimalPlaces = 0;

        #endregion

        #region Health Check Constants

        /// <summary>
        /// Health check endpoint path
        /// </summary>
        public const string HealthCheckEndpoint = "/health";

        /// <summary>
        /// Readiness check endpoint path
        /// </summary>
        public const string ReadinessCheckEndpoint = "/health/ready";

        /// <summary>
        /// Liveness check endpoint path
        /// </summary>
        public const string LivenessCheckEndpoint = "/health/live";

        #endregion

        #region Error Message Templates

        /// <summary>
        /// Template for device connection failure messages
        /// </summary>
        public const string DeviceConnectionFailureTemplate = "Failed to connect to device {0} at {1}:{2}";

        /// <summary>
        /// Template for configuration validation error messages
        /// </summary>
        public const string ConfigurationValidationErrorTemplate = "Invalid {0} configuration: {1}";

        /// <summary>
        /// Template for data quality degradation messages
        /// </summary>
        public const string DataQualityDegradationTemplate = "Data quality degraded for device {0}, channel {1}: {2}";

        #endregion
    }

    /// <summary>
    /// Default unit strings for common measurement types
    /// </summary>
    public static class DefaultUnits
    {
        /// <summary>
        /// Generic count unit for discrete measurements
        /// </summary>
        public const string Counts = "counts";

        /// <summary>
        /// Parts unit for manufacturing and production counting
        /// </summary>
        public const string Parts = "parts";

        /// <summary>
        /// Rate unit for items per second measurements
        /// </summary>
        public const string UnitsPerSecond = "units/s";

        /// <summary>
        /// Rate unit for parts per minute measurements
        /// </summary>
        public const string PartsPerMinute = "parts/min";

        /// <summary>
        /// Percentage unit for ratio measurements
        /// </summary>
        public const string Percentage = "%";

        /// <summary>
        /// Milliseconds time unit for precise timing measurements
        /// </summary>
        public const string Milliseconds = "ms";

        /// <summary>
        /// Seconds time unit for standard timing measurements
        /// </summary>
        public const string Seconds = "s";

        /// <summary>
        /// Minutes time unit for longer duration measurements
        /// </summary>
        public const string Minutes = "min";

        /// <summary>
        /// Hours time unit for extended duration measurements
        /// </summary>
        public const string Hours = "h";
    }

    /// <summary>
    /// Standard tag names for consistent metadata across the application
    /// </summary>
    public static class StandardTags
    {
        /// <summary>
        /// Tag name for identifying the type of industrial device
        /// </summary>
        public const string DeviceType = "device_type";

        /// <summary>
        /// Tag name for identifying the production line or manufacturing line
        /// </summary>
        public const string ProductionLine = "production_line";

        /// <summary>
        /// Tag name for identifying the work center or manufacturing cell
        /// </summary>
        public const string WorkCenter = "work_center";

        /// <summary>
        /// Tag name for identifying the work shift (day, night, etc.)
        /// </summary>
        public const string Shift = "shift";

        /// <summary>
        /// Tag name for identifying the type of product being manufactured
        /// </summary>
        public const string ProductType = "product_type";

        /// <summary>
        /// Tag name for identifying the quality classification or grade
        /// </summary>
        public const string QualityType = "quality_type";

        /// <summary>
        /// Tag name for identifying the severity level of alerts or notifications
        /// </summary>
        public const string AlertLevel = "alert_level";

        /// <summary>
        /// Tag name for identifying the source system or device providing the data
        /// </summary>
        public const string DataSource = "data_source";

        /// <summary>
        /// Tag name for identifying the deployment environment (production, staging, etc.)
        /// </summary>
        public const string Environment = "environment";

        /// <summary>
        /// Tag name for identifying the physical location or facility
        /// </summary>
        public const string Location = "location";
    }
}