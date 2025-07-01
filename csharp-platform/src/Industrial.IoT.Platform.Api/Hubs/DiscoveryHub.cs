using Microsoft.AspNetCore.SignalR;
using Industrial.IoT.Platform.Api.Models;

namespace Industrial.IoT.Platform.Api.Hubs;

public sealed class DiscoveryHub : Hub
{
    private readonly ILogger<DiscoveryHub> _logger;

    public DiscoveryHub(ILogger<DiscoveryHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client {ConnectionId} connected to DiscoveryHub", Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, "Discovery");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client {ConnectionId} disconnected from DiscoveryHub", Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Discovery");
        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinDeviceDiscovery(string deviceId)
    {
        var groupName = $"Discovery_{deviceId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client {ConnectionId} joined discovery group {GroupName}", Context.ConnectionId, groupName);
    }

    public async Task LeaveDeviceDiscovery(string deviceId)
    {
        var groupName = $"Discovery_{deviceId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client {ConnectionId} left discovery group {GroupName}", Context.ConnectionId, groupName);
    }
}