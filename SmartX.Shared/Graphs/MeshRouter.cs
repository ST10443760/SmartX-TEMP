namespace SmartX.Shared.Graphs;

/// <summary>One hop of a route: a packet crosses the link From -> To at this power cost.</summary>
public readonly record struct RouteHop(string From, string To, double Cost);

/// <summary>A node's power-efficient route to the gateway.</summary>
public sealed record MeshRoute(string Source, bool Reachable, IReadOnlyList<RouteHop> Hops)
{
    public double TotalCost => Math.Round(Hops.Sum(h => h.Cost), 2);

    /// <summary>Every node on the route, from the source to the gateway.</summary>
    public IReadOnlyList<string> Path => Hops.Count == 0
        ? (Reachable ? [Source] : [])
        : [Hops[0].From, .. Hops.Select(h => h.To)];
}

/// <summary>
/// Looks up a node's route to the gateway inside a minimum spanning tree. A
/// tree has exactly one path between any two nodes, so the route is found
/// by following parent links from the node up to the root. That costs
/// O(depth), which is at most O(V).
/// </summary>
public static class MeshRouter
{
    public static MeshRoute RouteToRoot(SpanningTreeResult tree, string source)
    {
        if (string.Equals(source, tree.Root, StringComparison.OrdinalIgnoreCase))
        {
            return new MeshRoute(source, true, []);
        }

        if (!tree.InRootTree(source))
        {
            return new MeshRoute(source, false, []);
        }

        var hops = new List<RouteHop>();
        var current = source;

        // A tree path can never be longer than the number of tree edges; the
        // limit is a guard so bad input can never loop forever.
        for (var guard = 0; guard <= tree.Edges.Count && tree.TryGetParentEdge(current, out var edge); guard++)
        {
            hops.Add(new RouteHop(current, edge.From, edge.Weight));
            current = edge.From;
        }

        return new MeshRoute(source, true, hops);
    }
}
