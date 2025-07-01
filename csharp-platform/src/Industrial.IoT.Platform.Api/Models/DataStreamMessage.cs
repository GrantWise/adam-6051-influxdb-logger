namespace Industrial.IoT.Platform.Api.Models;

public sealed class DataStreamMessage
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
    public Dictionary<string, string> Tags { get; set; } = new();
    public string? Quality { get; set; }
}

public sealed class DiscoveryProgressMessage
{
    public string DeviceId { get; set; } = string.Empty;
    public string ProtocolName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Progress { get; set; }
    public string? Message { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object> Details { get; set; } = new();
}