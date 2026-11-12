using SmartX.Shared.Enums;

namespace SmartX.Shared.Dtos;

/// <summary>
/// The whole mesh as the topology page draws it: nodes with positions and
/// states, links with weights, plus the BFS reachability report and the MST.
/// Pushed over SignalR whenever a node goes down or recovers.
/// </summary>
public sealed class TopologySnapshot
{
    public double SiteWidth { get; init; }

    public double SiteHeight { get; init; }

    public List<TopologyNode> Nodes { get; init; } = [];

    public List<TopologyEdge> Edges { get; init; } = [];

    public ReachabilityReport Reachability { get; init; } = new();

    public MstReport Mst { get; init; } = new();

    /// <summary>Registered devices with no position in the topology definition (not drawn).</summary>
    public List<string> Unplaced { get; init; } = [];

    public DateTime GeneratedUtc { get; init; }
}

public sealed class TopologyNode
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Null for the gateway.</summary>
    public SensorCategory? Category { get; init; }

    public double X { get; init; }

    public double Y { get; init; }

    public MeshNodeState State { get; init; }

    /// <summary>Fewest hops to the gateway over live links (BFS), or null when unreachable.</summary>
    public int? HopsToGateway { get; init; }

    /// <summary>The next node on this node's MST route, or null for the gateway and unreachable nodes.</summary>
    public string? MstParentId { get; init; }

    /// <summary>Total power cost of this node's MST route to the gateway, or null when unreachable.</summary>
    public double? RouteCost { get; init; }
}

public sealed class TopologyEdge
{
    public required string A { get; init; }

    public required string B { get; init; }

    /// <summary>Power cost of a transmission across this link (distance² / 100).</summary>
    public double Weight { get; init; }

    /// <summary>False when either end is down, so the link carries nothing.</summary>
    public bool IsLive { get; init; }

    /// <summary>True when the link is part of the gateway's minimum spanning tree.</summary>
    public bool InMst { get; init; }
}
