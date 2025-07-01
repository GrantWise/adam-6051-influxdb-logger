using Microsoft.AspNetCore.SignalR;
using Industrial.IoT.Platform.Core.Interfaces;
using Industrial.IoT.Platform.Api.Models;
using System.Reactive.Linq;

namespace Industrial.IoT.Platform.Api.Hubs;

public sealed class DeviceDataHub : Hub
{
    private readonly IEnumerable<IDeviceProvider> _deviceProviders;
    private readonly ILogger<DeviceDataHub> _logger;

    public DeviceDataHub(
        IEnumerable<IDeviceProvider> deviceProviders,
        ILogger<DeviceDataHub> logger)
    {
        _deviceProviders = deviceProviders;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client {ConnectionId} connected to DeviceDataHub", Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, "AllDevices");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client {ConnectionId} disconnected from DeviceDataHub", Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "AllDevices");
        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinDeviceGroup(string deviceId)
    {
        var groupName = $"Device_{deviceId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client {ConnectionId} joined group {GroupName}", Context.ConnectionId, groupName);
    }

    public async Task LeaveDeviceGroup(string deviceId)
    {
        var groupName = $"Device_{deviceId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client {ConnectionId} left group {GroupName}", Context.ConnectionId, groupName);
    }

    public async Task JoinDiscoveryGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "Discovery");
        _logger.LogInformation("Client {ConnectionId} joined Discovery group", Context.ConnectionId);
    }

    public async Task LeaveDiscoveryGroup()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Discovery");
        _logger.LogInformation("Client {ConnectionId} left Discovery group", Context.ConnectionId);
    }

    public async Task GetDeviceStatus()
    {
        var deviceStatuses = new List<DeviceStatusUpdate>();
        
        foreach (var provider in _deviceProviders)
        {
            var status = new DeviceStatusUpdate
            {
                DeviceId = provider.DeviceId,
                DeviceType = provider.DeviceType,
                IsConnected = provider.IsRunning,
                Timestamp = DateTime.UtcNow,
                LastDataTimestamp = DateTime.UtcNow
            };
            deviceStatuses.Add(status);
        }

        await Clients.Caller.SendAsync("DeviceStatusUpdate", deviceStatuses);
    }
}