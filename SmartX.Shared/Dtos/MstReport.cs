namespace SmartX.Shared.Dtos;

/// <summary>The minimum spanning tree (Prim's) over live nodes, rooted at the gateway.</summary>
public sealed class MstReport
{
    /// <summary>Edges of the gateway's tree, in the order Prim's added them.</summary>
    public List<MstEdgeInfo> Edges { get; init; } = [];

    /// <summary>Sum of the gateway tree's link weights.</summary>
    public double TotalCost { get; init; }

    /// <summary>Number of trees in the spanning forest (1 when every live node reaches the gateway).</summary>
    public int TreeCount { get; init; }

    /// <summary>Live nodes outside the gateway's tree (in another tree of the forest).</summary>
    public List<string> UnreachableNames { get; init; } = [];

    /// <summary>Down nodes left out before Prim's ran.</summary>
    public List<string> ExcludedNames { get; init; } = [];
}

/// <summary>A tree edge from the parent (nearer the gateway) to the child.</summary>
public sealed record MstEdgeInfo(string ParentId, string ParentName, string ChildId, string ChildName, double Weight);
