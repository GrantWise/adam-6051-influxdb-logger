using Microsoft.AspNetCore.Mvc;
using Industrial.IoT.Platform.Core.Interfaces;
using Industrial.IoT.Platform.Api.Models;

namespace Industrial.IoT.Platform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Tags("Data")]
public sealed class DataController : ControllerBase
{
    private readonly ILogger<DataController> _logger;

    public DataController(ILogger<DataController> logger)
    {
        _logger = logger;
    }

    [HttpGet("devices/{deviceId}/latest")]
    [ProducesResponseType(typeof(DataStreamMessage), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DataStreamMessage>> GetLatestData(string deviceId)
    {
        _logger.LogInformation("Getting latest data for device {DeviceId}", deviceId);
        
        var data = new DataStreamMessage
        {
            DeviceId = deviceId,
            DeviceType = "Unknown",
            Timestamp = DateTime.UtcNow,
            Data = new Dictionary<string, object> { { "status", "no data available" } },
            Tags = new Dictionary<string, string> { { "source", "api" } }
        };

        return Ok(data);
    }

    [HttpGet("devices/{deviceId}/history")]
    [ProducesResponseType(typeof(IEnumerable<DataStreamMessage>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<DataStreamMessage>>> GetHistoricalData(
        string deviceId,
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null,
        [FromQuery] int limit = 100)
    {
        _logger.LogInformation("Getting historical data for device {DeviceId}", deviceId);
        
        start ??= DateTime.UtcNow.AddHours(-1);
        end ??= DateTime.UtcNow;
        
        var data = new List<DataStreamMessage>();
        return Ok(data);
    }

    [HttpGet("devices/{deviceId}/aggregate")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<object>> GetAggregatedData(
        string deviceId,
        [FromQuery] string aggregation = "avg",
        [FromQuery] string interval = "1h",
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null)
    {
        _logger.LogInformation("Getting aggregated data for device {DeviceId}", deviceId);
        
        var result = new
        {
            DeviceId = deviceId,
            Aggregation = aggregation,
            Interval = interval,
            Start = start ?? DateTime.UtcNow.AddDays(-1),
            End = end ?? DateTime.UtcNow,
            Data = new object[] { }
        };

        return Ok(result);
    }

    [HttpGet("metrics")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> GetSystemMetrics()
    {
        _logger.LogInformation("Getting system metrics");
        
        var metrics = new
        {
            TotalDevices = 0,
            ActiveDevices = 0,
            DataPointsPerSecond = 0.0,
            StorageUsage = new
            {
                InfluxDb = "0 MB",
                SqlServer = "0 MB"
            },
            Uptime = TimeSpan.Zero,
            Timestamp = DateTime.UtcNow
        };

        return Ok(metrics);
    }
}