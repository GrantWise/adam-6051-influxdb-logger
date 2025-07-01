using Microsoft.AspNetCore.SignalR;
using Industrial.IoT.Platform.Core.Interfaces;
using Industrial.IoT.Platform.Api.Hubs;
using Industrial.IoT.Platform.Api.Models;
using System.Reactive.Linq;
using System.Reactive.Disposables;

namespace Industrial.IoT.Platform.Api.Services;

public sealed class DataStreamService : IHostedService, IDisposable
{
    private readonly IHubContext<DeviceDataHub> _hubContext;
    private readonly IEnumerable<IDeviceProvider> _deviceProviders;
    private readonly ILogger<DataStreamService> _logger;
    private readonly CompositeDisposable _subscriptions = new();

    public DataStreamService(
        IHubContext<DeviceDataHub> hubContext,
        IEnumerable<IDeviceProvider> deviceProviders,
        ILogger<DataStreamService> logger)
    {
        _hubContext = hubContext;
        _deviceProviders = deviceProviders;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting DataStreamService");

        foreach (var provider in _deviceProviders)
        {
            SetupDeviceDataStream(provider);
            SetupDeviceStatusStream(provider);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping DataStreamService");
        _subscriptions.Dispose();
        return Task.CompletedTask;
    }

    private void SetupDeviceDataStream(IDeviceProvider provider)
    {
        try
        {
            if (provider.DataStream != null)
            {
                var subscription = provider.DataStream
                    .Where(reading => reading != null)
                    .Select(reading => new DataStreamMessage
                    {
                        DeviceId = provider.DeviceId,
                        DeviceType = provider.DeviceType,
                        Timestamp = reading.Timestamp,
                        Data = reading.Values.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                        Tags = reading.Tags.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString() ?? string.Empty),
                        Quality = reading.Quality?.ToString()
                    })
                    .Subscribe(
                        message => SendDataToClients(message),
                        error => _logger.LogError(error, "Error in data stream for device {DeviceId}", provider.DeviceId),
                        () => _logger.LogInformation("Data stream completed for device {DeviceId}", provider.DeviceId));

                _subscriptions.Add(subscription);
                _logger.LogInformation("Setup data stream for device {DeviceId}", provider.DeviceId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to setup data stream for device {DeviceId}", provider.DeviceId);
        }
    }

    private void SetupDeviceStatusStream(IDeviceProvider provider)
    {
        try
        {
            var statusStream = Observable.Interval(TimeSpan.FromSeconds(30))
                .Select(_ => new DeviceStatusUpdate
                {
                    DeviceId = provider.DeviceId,
                    DeviceType = provider.DeviceType,
                    IsConnected = provider.IsRunning,
                    Timestamp = DateTime.UtcNow,
                    LastDataTimestamp = DateTime.UtcNow
                });

            var subscription = statusStream.Subscribe(
                status => SendStatusToClients(status),
                error => _logger.LogError(error, "Error in status stream for device {DeviceId}", provider.DeviceId));

            _subscriptions.Add(subscription);
            _logger.LogInformation("Setup status stream for device {DeviceId}", provider.DeviceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to setup status stream for device {DeviceId}", provider.DeviceId);
        }
    }

    private async void SendDataToClients(DataStreamMessage message)
    {
        try
        {
            await _hubContext.Clients.Group("AllDevices").SendAsync("DataReceived", message);
            await _hubContext.Clients.Group($"Device_{message.DeviceId}").SendAsync("DeviceDataReceived", message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send data message to clients for device {DeviceId}", message.DeviceId);
        }
    }

    private async void SendStatusToClients(DeviceStatusUpdate status)
    {
        try
        {
            await _hubContext.Clients.Group("AllDevices").SendAsync("DeviceStatusUpdate", status);
            await _hubContext.Clients.Group($"Device_{status.DeviceId}").SendAsync("DeviceStatusUpdate", status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send status update to clients for device {DeviceId}", status.DeviceId);
        }
    }

    public void Dispose()
    {
        _subscriptions?.Dispose();
    }
}