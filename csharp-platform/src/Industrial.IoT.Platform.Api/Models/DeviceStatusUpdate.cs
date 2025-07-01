namespace Industrial.IoT.Platform.Api.Models;

public sealed class DeviceStatusUpdate
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public DateTime Timestamp { get; set; }
    public DateTime? LastDataTimestamp { get; set; }
    public string? LastError { get; set; }
    public int DataPointsCount { get; set; }
    public Dictionary<string, object> MetricsSummary { get; set; } = new();
}