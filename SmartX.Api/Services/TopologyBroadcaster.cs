using Microsoft.AspNetCore.SignalR;
using SmartX.Api.Hubs;
using SmartX.Shared.Realtime;

namespace SmartX.Api.Services;

/// <summary>
/// Pushes a freshly computed topology (BFS + MST) to every dashboard. Called
/// whenever the shape of the live mesh changes: a node is flagged
/// disconnected, a node recovers, or a new device registers. The topology
/// page therefore re-routes without a refresh.
/// </summary>
public sealed class TopologyBroadcaster(MeshTopologyService topology, IHubContext<TelemetryHub> hub)
{
    public Task PublishAsync(CancellationToken ct = default)
        => hub.Clients.All.SendAsync(HubEvents.Topology, topology.Snapshot(), ct);
}
