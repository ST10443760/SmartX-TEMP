using Microsoft.AspNetCore.SignalR;

namespace SmartX.Api.Hubs;

/// <summary>
/// The single real-time channel dashboards connect to. The gateway only ever
/// pushes from the server side (via IHubContext in the background worker and
/// command endpoints), so the hub itself has no client-callable methods - the
/// browser just listens for the events named in SmartX.Shared.Realtime.HubEvents.
/// </summary>
public sealed class TelemetryHub(ILogger<TelemetryHub> logger) : Hub
{
    public override Task OnConnectedAsync()
    {
        logger.LogInformation("Dashboard connected to telemetry hub: {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation("Dashboard disconnected from telemetry hub: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
