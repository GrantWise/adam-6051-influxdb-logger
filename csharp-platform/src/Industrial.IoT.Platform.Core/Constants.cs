// Industrial.IoT.Platform.Core - Application Constants
// Centralized constants following existing Industrial.Adam.Logger patterns

namespace Industrial.IoT.Platform.Core;

/// <summary>
/// Centralized constants for the Industrial IoT Platform
/// Follows the established pattern from Industrial.Adam.Logger.Constants
/// </summary>
public static class Constants
{
    #region Default Configuration Values

    /// <summary>
    /// Default polling interval in milliseconds for device data acquisition
    /// </summary>
    public const int DefaultPollIntervalMs = 5000;

    /// <summary>
    /// Default health check interval in milliseconds
    /// </summary>
    public const int DefaultHealthCheckIntervalMs = 30000;

    /// <summary>
    /// Default maximum number of devices to poll concurrently
    /// </summary>
    public const int DefaultMaxConcurrentDevices = 10;

    /// <summary>
    /// Default data buffer size for storing readings
    /// </summary>
    public const int DefaultDataBufferSize = 1000;

    /// <summary>
    /// Default batch size for processing data readings
    /// </summary>
    public const int DefaultBatchSize = 100;

    /// <summary>
    /// Default timeout for batch processing operations in milliseconds
    /// </summary>
    public const int DefaultBatchTimeoutMs = 5000;

    /// <summary>
    /// Default maximum consecutive failures before marking device as offline
    /// </summary>
    public const int DefaultMaxConsecutiveFailures = 5;

    /// <summary>
    /// Default device timeout in minutes before considering it unresponsive
    /// </summary>
    public const int DefaultDeviceTimeoutMinutes = 5;

    #endregion

    #region Protocol Discovery Constants

    /// <summary>
    /// Default confidence threshold for protocol discovery (0-100)
    /// </summary>
    public const double DefaultConfidenceThreshold = 85.0;

    /// <summary>
    /// Default maximum number of discovery iterations
    /// </summary>
    public const int DefaultMaxDiscoveryIterations = 10;

    /// <summary>
    /// Default timeout for capturing data frames during discovery (milliseconds)
    /// </summary>
    public const int DefaultFrameTimeoutMs = 2000;

    /// <summary>
    /// Default minimum number of data samples required for analysis
    /// </summary>
    public const int DefaultMinSamples = 3;

    /// <summary>
    /// Default window size for stability detection
    /// </summary>
    public const int DefaultStabilityWindow = 5;

    /// <summary>
    /// Default timeout for protocol discovery operations in seconds
    /// </summary>
    public const int DefaultDiscoveryTimeoutSeconds = 60;

    #endregion

    #region Transport and Communication Constants

    /// <summary>
    /// Default TCP port for ADAM-4571 devices
    /// </summary>
    public const int DefaultAdam4571Port = 4001;

    /// <summary>
    /// Default TCP connection timeout in milliseconds
    /// </summary>
    public const int DefaultTcpTimeoutMs = 5000;

    /// <summary>
    /// Default buffer size for TCP communication
    /// </summary>
    public const int DefaultTcpBufferSize = 1024;

    /// <summary>
    /// Default reconnection delay in milliseconds
    /// </summary>
    public const int DefaultReconnectDelayMs = 2000;

    #endregion

    #region Storage Constants

    /// <summary>
    /// Default batch size for storage operations
    /// </summary>
    public const int DefaultStorageBatchSize = 100;

    /// <summary>
    /// Default storage operation timeout in milliseconds
    /// </summary>
    public const int DefaultStorageTimeoutMs = 30000;

    /// <summary>
    /// Default data retention period in days
    /// </summary>
    public const int DefaultRetentionDays = 90;

    /// <summary>
    /// Default maximum retry attempts for storage operations
    /// </summary>
    public const int DefaultStorageRetryAttempts = 3;

    #endregion

    #region Performance and Monitoring Constants

    /// <summary>
    /// Default performance counter collection interval in milliseconds
    /// </summary>
    public const int DefaultPerformanceCounterIntervalMs = 60000;

    /// <summary>
    /// Default metrics collection window size
    /// </summary>
    public const int DefaultMetricsWindowSize = 100;

    /// <summary>
    /// Default health check timeout in milliseconds
    /// </summary>
    public const int DefaultHealthCheckTimeoutMs = 10000;

    #endregion

    #region Validation Constants

    /// <summary>
    /// Minimum allowed polling interval in milliseconds
    /// </summary>
    public const int MinPollIntervalMs = 100;

    /// <summary>
    /// Maximum allowed polling interval in milliseconds
    /// </summary>
    public const int MaxPollIntervalMs = 300000; // 5 minutes

    /// <summary>
    /// Maximum allowed device timeout in minutes
    /// </summary>
    public const int MaxDeviceTimeoutMinutes = 60;

    /// <summary>
    /// Maximum allowed concurrent devices
    /// </summary>
    public const int MaxConcurrentDevices = 50;

    /// <summary>
    /// Maximum allowed data buffer size
    /// </summary>
    public const int MaxDataBufferSize = 100000;

    /// <summary>
    /// Maximum device ID length
    /// </summary>
    public const int MaxDeviceIdLength = 50;

    /// <summary>
    /// Minimum port number
    /// </summary>
    public const int MinPortNumber = 1;

    /// <summary>
    /// Maximum port number
    /// </summary>
    public const int MaxPortNumber = 65535;

    /// <summary>
    /// Default device communication timeout in milliseconds
    /// </summary>
    public const int DefaultDeviceTimeoutMs = 5000;

    /// <summary>
    /// Default maximum retry attempts
    /// </summary>
    public const int DefaultMaxRetries = 3;

    /// <summary>
    /// Default retry delay in milliseconds
    /// </summary>
    public const int DefaultRetryDelayMs = 1000;

    /// <summary>
    /// Default TCP receive buffer size
    /// </summary>
    public const int DefaultReceiveBufferSize = 8192;

    /// <summary>
    /// Default TCP send buffer size
    /// </summary>
    public const int DefaultSendBufferSize = 8192;

    /// <summary>
    /// Connection retry cooldown in seconds
    /// </summary>
    public const int ConnectionRetryCooldownSeconds = 5;

    /// <summary>
    /// Maximum protocol template name length
    /// </summary>
    public const int MaxProtocolTemplateNameLength = 100;

    /// <summary>
    /// Default stability threshold in milliseconds
    /// </summary>
    public const int DefaultStabilityThresholdMs = 1000;

    /// <summary>
    /// Default stability tolerance
    /// </summary>
    public const double DefaultStabilityTolerance = 0.1;

    #endregion

    #region String Constants

    /// <summary>
    /// Default encoding for text-based protocols
    /// </summary>
    public const string DefaultTextEncoding = "ASCII";

    /// <summary>
    /// Default delimiter for text-based protocols
    /// </summary>
    public const string DefaultTextDelimiter = "\r\n";

    /// <summary>
    /// Default storage type for time-series data
    /// </summary>
    public const string DefaultTimeSeriesStorage = "InfluxDB";

    /// <summary>
    /// Default storage type for transactional data
    /// </summary>
    public const string DefaultTransactionalStorage = "SQLServer";

    /// <summary>
    /// Default storage type for configuration data
    /// </summary>
    public const string DefaultConfigurationStorage = "SQLServer";

    #endregion

    #region Error Messages

    /// <summary>
    /// Error message for invalid device configuration
    /// </summary>
    public const string ErrorInvalidDeviceConfiguration = "Device configuration is invalid or incomplete";

    /// <summary>
    /// Error message for connection failure
    /// </summary>
    public const string ErrorConnectionFailure = "Failed to establish connection to device";

    /// <summary>
    /// Error message for protocol discovery failure
    /// </summary>
    public const string ErrorProtocolDiscoveryFailure = "Protocol discovery failed to achieve sufficient confidence";

    /// <summary>
    /// Error message for storage operation failure
    /// </summary>
    public const string ErrorStorageOperationFailure = "Storage operation failed";

    /// <summary>
    /// Error message for validation failure
    /// </summary>
    public const string ErrorValidationFailure = "Validation failed for configuration";

    #endregion
}