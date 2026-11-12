using SmartX.Api.Data;
using SmartX.Shared.Dtos;
using SmartX.Shared.Enums;
using SmartX.Shared.Graphs;
using SmartX.Shared.Realtime;

namespace SmartX.Api.Services;

/// <summary>
/// Turns the live gateway state into the Part 3 mesh view. On every call it:
/// <list type="number">
///   <item>builds the <see cref="MeshGraph"/> for the devices registered right now (<see cref="MeshTopologySeed"/>),</item>
///   <item>reads which nodes are down from the Part 2 <see cref="ActiveAlertSet"/>, the single source of truth for disconnections,</item>
///   <item>runs BFS (<see cref="MeshReachability"/>) and Prim's (<see cref="PrimMinimumSpanningTree"/>) over the live part,</item>
///   <item>maps the results to DTOs, or looks up one device's route (<see cref="MeshRouter"/>).</item>
/// </list>
/// The algorithms live in SmartX.Shared and this class only wires them up.
/// Nothing is cached: with about 17 nodes and 30 links a full recompute
/// takes microseconds, and a fresh graph per call means no shared mutable
/// graph that needs locking. A disconnect or recovery is reflected
/// immediately, which is what makes routes "self-heal".
/// </summary>
public sealed class MeshTopologyService(DeviceDirectory directory, ActiveAlertSet alerts)
{
    public const string GatewayName = "Gateway";

    public TopologySnapshot Snapshot()
    {
        var a = Analyse();

        var nodes = a.Graph.Vertices
            .Select(id =>
            {
                var (x, y) = MeshTopologySeed.Positions[id];
                var route = MeshRouter.RouteToRoot(a.Mst, id);
                a.Mst.TryGetParentEdge(id, out var parentEdge);
                return new TopologyNode
                {
                    Id = id,
                    Name = a.Name(id),
                    Category = a.Devices.TryGetValue(id, out var device) ? device.Category : null,
                    X = x,
                    Y = y,
                    State = a.State(id),
                    HopsToGateway = a.Bfs.HopsTo(id),
                    MstParentId = a.Mst.InRootTree(id) && !IsGateway(id) ? parentEdge.From : null,
                    RouteCost = route.Reachable ? route.TotalCost : null
                };
            })
            .OrderBy(n => n.State == MeshNodeState.Gateway ? 0 : 1)
            .ThenBy(n => n.Name)
            .ToList();

        var mstLinks = a.Mst.RootTreeEdges.Select(e => Key(e.From, e.To)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new TopologySnapshot
        {
            SiteWidth = MeshTopologySeed.SiteWidth,
            SiteHeight = MeshTopologySeed.SiteHeight,
            Nodes = nodes,
            Edges = a.Graph.Edges()
                .Select(e => new TopologyEdge
                {
                    A = e.From,
                    B = e.To,
                    Weight = e.Weight,
                    IsLive = !a.Down.Contains(e.From) && !a.Down.Contains(e.To),
                    InMst = mstLinks.Contains(Key(e.From, e.To))
                })
                .ToList(),
            Reachability = BuildReachability(a),
            Mst = BuildMst(a),
            Unplaced = a.Devices.Values
                .Where(d => !a.Graph.ContainsVertex(d.MacAddress))
                .Select(d => d.DisplayName)
                .Order()
                .ToList(),
            GeneratedUtc = DateTime.UtcNow
        };
    }

    public ReachabilityReport Reachability() => BuildReachability(Analyse());

    public MstReport MinimumSpanningTree() => BuildMst(Analyse());

    /// <summary>A device's MST route to the gateway, by MAC or display name. Null for an unknown device.</summary>
    public MeshRouteResult? Route(string macOrName)
    {
        var device = DeviceLookup.Resolve(directory, macOrName);
        if (device is null)
        {
            return null;
        }

        var a = Analyse();
        var mac = device.MacAddress;

        if (!a.Graph.ContainsVertex(mac))
        {
            return new MeshRouteResult
            {
                MacAddress = mac,
                Name = device.DisplayName,
                State = MeshNodeState.CutOff,
                Message = $"{device.DisplayName} has no position in the mesh topology definition, so it has no route."
            };
        }

        var state = a.State(mac);
        var route = MeshRouter.RouteToRoot(a.Mst, mac);

        return new MeshRouteResult
        {
            MacAddress = mac,
            Name = device.DisplayName,
            State = state,
            Reachable = route.Reachable,
            Hops = route.Hops.Select(h => new RouteHopInfo(h.From, a.Name(h.From), h.To, a.Name(h.To), h.Cost)).ToList(),
            TotalCost = route.TotalCost,
            Message = state switch
            {
                MeshNodeState.Down => $"{device.DisplayName} is disconnected, so it has no route. Its neighbours route around it.",
                MeshNodeState.CutOff => $"{device.DisplayName} is up, but every path to the gateway runs through a disconnected node.",
                // Invariant so the decimal point does not depend on the server's locale.
                _ => FormattableString.Invariant($"{route.Hops.Count} hop(s) to the gateway, total power cost {route.TotalCost:0.0}.")
            }
        };
    }

    private Analysis Analyse()
    {
        var devices = directory.Snapshot().ToDictionary(d => d.MacAddress, StringComparer.OrdinalIgnoreCase);
        var graph = MeshTopologySeed.BuildGraph(devices.Keys);
        var down = alerts.DisconnectedNodes();
        down.IntersectWith(graph.Vertices);

        return new Analysis(
            graph,
            down,
            MeshReachability.Analyse(graph, MeshGraph.GatewayId, down),
            PrimMinimumSpanningTree.Compute(graph, MeshGraph.GatewayId, down),
            devices);
    }

    private static ReachabilityReport BuildReachability(Analysis a) => new()
    {
        Reachable = a.Bfs.Reachable
            .Where(id => !IsGateway(id))
            .Select(id => new ReachableNodeInfo(id, a.Name(id), a.Bfs.HopsTo(id) ?? 0))
            .ToList(),
        Unreachable = a.Bfs.Unreachable
            .Select(u => new UnreachableNodeInfo(u.Id, a.Name(u.Id), u.Reason.ToString()))
            .ToList(),
        Reroutes = a.Bfs.Reroutes
            .Select(r => new RerouteInfo(r.DownNode, a.Name(r.DownNode), r.ViaNeighbour, a.Name(r.ViaNeighbour), r.PathToGateway.Select(a.Name).ToList()))
            .ToList()
    };

    private static MstReport BuildMst(Analysis a) => new()
    {
        Edges = a.Mst.RootTreeEdges
            .Select(e => new MstEdgeInfo(e.From, a.Name(e.From), e.To, a.Name(e.To), e.Weight))
            .ToList(),
        TotalCost = a.Mst.TotalCost,
        TreeCount = a.Mst.TreeCount,
        UnreachableNames = a.Mst.Unreachable.Select(a.Name).Order().ToList(),
        ExcludedNames = a.Mst.Excluded.Select(a.Name).Order().ToList()
    };

    private static bool IsGateway(string id) => string.Equals(id, MeshGraph.GatewayId, StringComparison.OrdinalIgnoreCase);

    private static string Key(string a, string b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase) < 0 ? $"{a}|{b}" : $"{b}|{a}";

    private sealed record Analysis(
        MeshGraph Graph,
        HashSet<string> Down,
        ReachabilityResult Bfs,
        SpanningTreeResult Mst,
        Dictionary<string, LiveDevice> Devices)
    {
        public string Name(string id) => IsGateway(id)
            ? GatewayName
            : Devices.TryGetValue(id, out var device) ? device.DisplayName : id;

        public MeshNodeState State(string id) => IsGateway(id)
            ? MeshNodeState.Gateway
            : Down.Contains(id)
                ? MeshNodeState.Down
                : Bfs.IsReachable(id) ? MeshNodeState.Online : MeshNodeState.CutOff;
    }
}
