// Industrial.IoT.Platform.Api - Device Management Controller
// REST API endpoints for device lifecycle management and monitoring

using Microsoft.AspNetCore.Mvc;
using Industrial.IoT.Platform.Core.Interfaces;
using Industrial.IoT.Platform.Core.Models;
using Industrial.IoT.Platform.Api.Models;
using System.ComponentModel.DataAnnotations;

namespace Industrial.IoT.Platform.Api.Controllers;

/// <summary>
/// Device management and monitoring API endpoints
/// Provides CRUD operations and health monitoring for Industrial IoT devices
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class DevicesController : ControllerBase
{
    private readonly IEnumerable<IDeviceProvider> _deviceProviders;
    private readonly ILogger<DevicesController> _logger;

    /// <summary>
    /// Initialize device management controller
    /// </summary>
    public DevicesController(
        IEnumerable<IDeviceProvider> deviceProviders,
        ILogger<DevicesController> logger)
    {
        _deviceProviders = deviceProviders ?? throw new ArgumentNullException(nameof(deviceProviders));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get all registered devices with their current status
    /// </summary>
    /// <response code="200">Returns list of all devices</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<DeviceInfoResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<DeviceInfoResponse>>> GetDevices()
    {
        try
        {
            var devices = new List<DeviceInfoResponse>();
            
            foreach (var provider in _deviceProviders)
            {
                var deviceInfo = new DeviceInfoResponse
                {
                    DeviceId = provider.DeviceId,
                    DeviceType = provider.DeviceType,
                    IsConnected = provider.IsConnected,
                    LastSeen = provider.LastUpdateTime,
                    Health = await GetDeviceHealthSafe(provider),
                    Configuration = await GetDeviceConfigurationSafe(provider)
                };
                
                devices.Add(deviceInfo);
            }

            _logger.LogDebug("Retrieved {Count} devices", devices.Count);
            return Ok(devices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve devices");
            return StatusCode(500, new { Error = "Failed to retrieve devices", Details = ex.Message });
        }
    }

    /// <summary>
    /// Get specific device by ID
    /// </summary>
    /// <param name="deviceId">Unique device identifier</param>
    /// <response code="200">Returns device information</response>
    /// <response code="404">Device not found</response>
    [HttpGet("{deviceId}")]
    [ProducesResponseType(typeof(DeviceInfoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeviceInfoResponse>> GetDevice([Required] string deviceId)
    {
        try
        {
            var provider = _deviceProviders.FirstOrDefault(p => p.DeviceId == deviceId);
            if (provider == null)
            {
                _logger.LogWarning("Device {DeviceId} not found", deviceId);
                return NotFound(new { Error = $"Device '{deviceId}' not found" });
            }

            var deviceInfo = new DeviceInfoResponse
            {
                DeviceId = provider.DeviceId,
                DeviceType = provider.DeviceType,
                IsConnected = provider.IsConnected,
                LastSeen = provider.LastUpdateTime,
                Health = await GetDeviceHealthSafe(provider),
                Configuration = await GetDeviceConfigurationSafe(provider)
            };

            return Ok(deviceInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve device {DeviceId}", deviceId);
            return StatusCode(500, new { Error = "Failed to retrieve device", Details = ex.Message });
        }
    }

    /// <summary>
    /// Get device health status and diagnostics
    /// </summary>
    /// <param name="deviceId">Unique device identifier</param>
    /// <response code="200">Returns device health information</response>
    /// <response code="404">Device not found</response>
    [HttpGet("{deviceId}/health")]
    [ProducesResponseType(typeof(DeviceHealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeviceHealthResponse>> GetDeviceHealth([Required] string deviceId)
    {
        try
        {
            var provider = _deviceProviders.FirstOrDefault(p => p.DeviceId == deviceId);
            if (provider == null)
            {
                return NotFound(new { Error = $"Device '{deviceId}' not found" });
            }

            var health = await GetDeviceHealthSafe(provider);
            var response = new DeviceHealthResponse
            {
                DeviceId = deviceId,
                IsHealthy = health.IsHealthy,
                Status = health.Status.ToString(),
                LastChecked = health.LastChecked,
                Uptime = health.Uptime,
                ErrorCount = health.ErrorCount,
                LastError = health.LastError,
                Diagnostics = health.Diagnostics ?? new Dictionary<string, object>()
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get health for device {DeviceId}", deviceId);
            return StatusCode(500, new { Error = "Failed to get device health", Details = ex.Message });
        }
    }

    /// <summary>
    /// Get latest data readings from device
    /// </summary>
    /// <param name="deviceId">Unique device identifier</param>
    /// <param name="limit">Maximum number of readings to return (default: 10, max: 100)</param>
    /// <response code="200">Returns latest device readings</response>
    /// <response code="404">Device not found</response>
    [HttpGet("{deviceId}/data")]
    [ProducesResponseType(typeof(IEnumerable<DataReadingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<DataReadingResponse>>> GetDeviceData(
        [Required] string deviceId,
        [Range(1, 100)] int limit = 10)
    {
        try
        {
            var provider = _deviceProviders.FirstOrDefault(p => p.DeviceId == deviceId);
            if (provider == null)
            {
                return NotFound(new { Error = $"Device '{deviceId}' not found" });
            }

            // Get latest readings - implementation would depend on provider capabilities
            var readings = new List<DataReadingResponse>();
            
            // For now, return empty list - this would be expanded based on provider interface
            _logger.LogDebug("Retrieved {Count} readings for device {DeviceId}", readings.Count, deviceId);
            return Ok(readings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get data for device {DeviceId}", deviceId);
            return StatusCode(500, new { Error = "Failed to get device data", Details = ex.Message });
        }
    }

    /// <summary>
    /// Connect to a device
    /// </summary>
    /// <param name="deviceId">Unique device identifier</param>
    /// <response code="200">Device connected successfully</response>
    /// <response code="404">Device not found</response>
    /// <response code="409">Device already connected</response>
    [HttpPost("{deviceId}/connect")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> ConnectDevice([Required] string deviceId)
    {
        try
        {
            var provider = _deviceProviders.FirstOrDefault(p => p.DeviceId == deviceId);
            if (provider == null)
            {
                return NotFound(new { Error = $"Device '{deviceId}' not found" });
            }

            if (provider.IsConnected)
            {
                return Conflict(new { Error = $"Device '{deviceId}' is already connected" });
            }

            var connected = await provider.ConnectAsync();
            if (connected)
            {
                _logger.LogInformation("Device {DeviceId} connected successfully", deviceId);
                return Ok(new { Message = $"Device '{deviceId}' connected successfully" });
            }
            else
            {
                _logger.LogWarning("Failed to connect to device {DeviceId}", deviceId);
                return StatusCode(500, new { Error = $"Failed to connect to device '{deviceId}'" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to device {DeviceId}", deviceId);
            return StatusCode(500, new { Error = "Failed to connect device", Details = ex.Message });
        }
    }

    /// <summary>
    /// Disconnect from a device
    /// </summary>
    /// <param name="deviceId">Unique device identifier</param>
    /// <response code="200">Device disconnected successfully</response>
    /// <response code="404">Device not found</response>
    /// <response code="409">Device already disconnected</response>
    [HttpPost("{deviceId}/disconnect")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> DisconnectDevice([Required] string deviceId)
    {
        try
        {
            var provider = _deviceProviders.FirstOrDefault(p => p.DeviceId == deviceId);
            if (provider == null)
            {
                return NotFound(new { Error = $"Device '{deviceId}' not found" });
            }

            if (!provider.IsConnected)
            {
                return Conflict(new { Error = $"Device '{deviceId}' is already disconnected" });
            }

            await provider.DisconnectAsync();
            _logger.LogInformation("Device {DeviceId} disconnected successfully", deviceId);
            return Ok(new { Message = $"Device '{deviceId}' disconnected successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting from device {DeviceId}", deviceId);
            return StatusCode(500, new { Error = "Failed to disconnect device", Details = ex.Message });
        }
    }

    /// <summary>
    /// Test device connectivity
    /// </summary>
    /// <param name="deviceId">Unique device identifier</param>
    /// <response code="200">Returns connectivity test results</response>
    /// <response code="404">Device not found</response>
    [HttpPost("{deviceId}/test")]
    [ProducesResponseType(typeof(ConnectivityTestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConnectivityTestResponse>> TestDeviceConnectivity([Required] string deviceId)
    {
        try
        {
            var provider = _deviceProviders.FirstOrDefault(p => p.DeviceId == deviceId);
            if (provider == null)
            {
                return NotFound(new { Error = $"Device '{deviceId}' not found" });
            }

            var startTime = DateTimeOffset.UtcNow;
            var testPassed = await provider.TestConnectivityAsync(TimeSpan.FromSeconds(10));
            var duration = DateTimeOffset.UtcNow - startTime;

            var response = new ConnectivityTestResponse
            {
                DeviceId = deviceId,
                TestPassed = testPassed,
                TestDuration = duration,
                TestedAt = startTime,
                ErrorMessage = testPassed ? null : "Connectivity test failed"
            };

            _logger.LogDebug("Connectivity test for device {DeviceId}: {Result}", deviceId, testPassed ? "PASSED" : "FAILED");
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing connectivity for device {DeviceId}", deviceId);
            return StatusCode(500, new { Error = "Failed to test device connectivity", Details = ex.Message });
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Safely get device health, handling any exceptions
    /// </summary>
    private async Task<IDeviceHealth> GetDeviceHealthSafe(IDeviceProvider provider)
    {
        try
        {
            return await provider.GetHealthAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get health for device {DeviceId}", provider.DeviceId);
            
            // Return a default health object indicating an error
            return new DefaultDeviceHealth
            {
                DeviceId = provider.DeviceId,
                Timestamp = DateTimeOffset.UtcNow,
                Status = DeviceStatus.Error,
                IsConnected = false,
                LastError = ex.Message,
                ConsecutiveFailures = 1,
                TotalReads = 0,
                SuccessfulReads = 0
            };
        }
    }

    /// <summary>
    /// Safely get device configuration, handling any exceptions
    /// </summary>
    private async Task<IDeviceConfiguration?> GetDeviceConfigurationSafe(IDeviceProvider provider)
    {
        try
        {
            return await provider.GetConfigurationAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get configuration for device {DeviceId}", provider.DeviceId);
            return null;
        }
    }

    #endregion
}

/// <summary>
/// Default implementation of IDeviceHealth for error scenarios
/// </summary>
internal class DefaultDeviceHealth : IDeviceHealth
{
    public string DeviceId { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public DeviceStatus Status { get; set; }
    public bool IsConnected { get; set; }
    public TimeSpan? LastSuccessfulRead { get; set; }
    public int ConsecutiveFailures { get; set; }
    public double? CommunicationLatency { get; set; }
    public string? LastError { get; set; }
    public int TotalReads { get; set; }
    public int SuccessfulReads { get; set; }
}