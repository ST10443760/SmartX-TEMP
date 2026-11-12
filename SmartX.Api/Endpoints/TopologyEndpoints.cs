using SmartX.Api.Services;

namespace SmartX.Api.Endpoints;

/// <summary>
/// Part 3 network topology and mesh routing. These handlers only call
/// <see cref="MeshTopologyService"/>. The graph, BFS, Prim's and route lookup
/// live in their own classes in SmartX.Shared/Graphs.
/// </summary>
public static class TopologyEndpoints
{
    public static void MapTopologyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/topology").WithTags("Topology");

        group.MapGet("/", (MeshTopologyService topology) => Results.Ok(topology.Snapshot()))
            .WithName("GetTopology")
            .WithSummary("Every node (position and state) and link (weight, live, in MST), plus reachability and the MST.");

        group.MapGet("/reachability", (MeshTopologyService topology) => Results.Ok(topology.Reachability()))
            .WithName("GetReachability")
            .WithSummary("BFS from the gateway over live links: reachable, down and cut-off nodes, and where traffic reroutes.");

        group.MapGet("/mst", (MeshTopologyService topology) => Results.Ok(topology.MinimumSpanningTree()))
            .WithName("GetMinimumSpanningTree")
            .WithSummary("Prim's minimum spanning tree over live nodes, rooted at the gateway, with its total power cost.");

        group.MapGet("/route/{device}", (string device, MeshTopologyService topology) =>
                topology.Route(device) is { } route ? Results.Ok(route) : Results.NotFound($"Unknown device '{device}'."))
            .WithName("GetRoute")
            .WithSummary("A device's power-efficient route to the gateway (its MST path), by MAC or name, hop by hop.");
    }
}
