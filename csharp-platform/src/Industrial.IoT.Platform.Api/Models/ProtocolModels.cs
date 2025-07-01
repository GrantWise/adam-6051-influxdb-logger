namespace Industrial.IoT.Platform.Api.Models;

public sealed class DiscoveryRequest
{
    public string DeviceId { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public int? Port { get; set; }
    public string[]? ProtocolTypes { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public Dictionary<string, object> Options { get; set; } = new();
}

public sealed class ProtocolTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string ProtocolType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string[] Commands { get; set; } = Array.Empty<string>();
    public string[] ExpectedResponses { get; set; } = Array.Empty<string>();
    public Dictionary<string, object> Settings { get; set; } = new();
}