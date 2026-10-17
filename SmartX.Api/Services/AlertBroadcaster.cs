using Microsoft.AspNetCore.SignalR;
using SmartX.Api.Hubs;
using SmartX.Shared.Dtos;
using SmartX.Shared.Enums;
using SmartX.Shared.Realtime;

namespace SmartX.Api.Services;

/// <summary>
/// Builds a human-readable <see cref="AlertEvent"/> and pushes it to every
/// dashboard. Shared by the worker, the disconnect monitor and the demo
/// endpoints so alert wording is consistent everywhere.
/// </summary>
public sealed class AlertBroadcaster(DeviceDirectory directory, GatewayCounters counters, IHubContext<TelemetryHub> hub)
{
    private const int RecentCapacity = 40;

    // Bounded log so a dashboard that opens later still sees what happened.
    private readonly Queue<AlertEvent> _recent = new();
    private readonly Lock _gate = new();

    /// <summary>Newest first.</summary>
    public List<AlertEvent> Recent()
    {
        lock (_gate)
        {
            return _recent.Reverse().ToList();
        }
    }

    public Task RaisedAsync(string mac, AlertKind kind, string detail, CancellationToken ct = default)
        => SendAsync(mac, kind, AlertEventType.Raised, detail, ct);

    public Task RecoveredAsync(string mac, AlertKind kind, CancellationToken ct = default)
        => SendAsync(mac, kind, AlertEventType.Recovered, kind == AlertKind.Disconnected ? "is reporting again" : "is back within normal range", ct);

    public Task DuplicateSuppressedAsync(string mac, AlertKind kind, CancellationToken ct = default)
    {
        counters.DuplicateSuppressed();
        return SendAsync(mac, kind, AlertEventType.DuplicateSuppressed, "is already in the active alert set - no new alert raised", ct);
    }

    private Task SendAsync(string mac, AlertKind kind, AlertEventType type, string detail, CancellationToken ct)
    {
        var name = directory.TryGet(mac, out var device) ? device!.DisplayName : mac;
        var alert = new AlertEvent
        {
            MacAddress = device?.MacAddress ?? mac,
            DeviceName = name,
            Kind = kind,
            EventType = type,
            Message = $"{name} {detail}",
            TimestampUtc = DateTime.UtcNow
        };

        lock (_gate)
        {
            _recent.Enqueue(alert);
            while (_recent.Count > RecentCapacity)
            {
                _recent.Dequeue();
            }
        }

        return hub.Clients.All.SendAsync(HubEvents.Alert, alert, ct);
    }
}
