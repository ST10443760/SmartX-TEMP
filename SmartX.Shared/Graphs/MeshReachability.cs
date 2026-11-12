namespace SmartX.Shared.Graphs;

/// <summary>Why a node cannot reach the gateway.</summary>
public enum UnreachableReason
{
    /// <summary>The node itself is disconnected (it is in the active alert set).</summary>
    Down,

    /// <summary>The node is up, but every path from it to the gateway passes through a down node.</summary>
    CutOff
}

public readonly record struct UnreachableNode(string Id, UnreachableReason Reason);

/// <summary>
/// For one down node: a live neighbour that can still reach the gateway, and
/// the path traffic takes from that neighbour. It answers "if X is down,
/// where does traffic hop instead?".
/// </summary>
public sealed record RerouteOption(string DownNode, string ViaNeighbour, IReadOnlyList<string> PathToGateway);

/// <summary>Result of a <see cref="MeshReachability.Analyse"/> run.</summary>
public sealed class ReachabilityResult(
    string source,
    IReadOnlyList<string> reachable,
    IReadOnlyDictionary<string, string?> parent,
    IReadOnlyDictionary<string, int> hops,
    IReadOnlyList<UnreachableNode> unreachable,
    IReadOnlyList<RerouteOption> reroutes)
{
    public string Source { get; } = source;

    /// <summary>Reachable nodes in the order BFS discovered them (gateway first, then by hop count).</summary>
    public IReadOnlyList<string> Reachable { get; } = reachable;

    public IReadOnlyList<UnreachableNode> Unreachable { get; } = unreachable;

    /// <summary>For every down node, each live neighbour that can still reach the gateway.</summary>
    public IReadOnlyList<RerouteOption> Reroutes { get; } = reroutes;

    public bool IsReachable(string id) => parent.ContainsKey(id);

    /// <summary>Fewest hops from the node to the gateway, or null when unreachable.</summary>
    public int? HopsTo(string id) => hops.TryGetValue(id, out var h) ? h : null;

    /// <summary>
    /// The fewest-hops path from a node back to the gateway, following the BFS
    /// parent pointers: [node, ..., gateway]. Empty when unreachable.
    /// </summary>
    public IReadOnlyList<string> PathToSource(string id)
    {
        if (!parent.ContainsKey(id))
        {
            return [];
        }

        var path = new List<string>();
        for (string? current = id; current is not null; current = parent[current])
        {
            path.Add(current);
        }

        return path;
    }
}

/// <summary>
/// Breadth-first search from the gateway over live links only, to find which
/// devices can currently get their packets home.
///
/// BFS uses a <see cref="Queue{T}"/>: start with the gateway, and repeatedly
/// take the oldest discovered node and add every neighbour not seen yet. A
/// node is never queued twice (the visited check), so it runs in O(V + E).
/// Because nodes come out in order of distance, the parent pointers also give
/// each node's fewest-hops path to the gateway.
///
/// Down nodes are skipped as if they had been removed from the graph, so a
/// node that is up but only reachable through a down node is never
/// discovered. It is reported as <see cref="UnreachableReason.CutOff"/>, not
/// as reachable. The set of down nodes comes from the caller (the API passes
/// the disconnected nodes from the Part 2 ActiveAlertSet), so this class
/// keeps no state and is easy to test.
///
/// Source: written from general knowledge of breadth-first search. The
/// queue-based form is the standard one, as in Cormen, Leiserson, Rivest and
/// Stein, Introduction to Algorithms (3rd ed.), section 22.2.
/// </summary>
public static class MeshReachability
{
    public static ReachabilityResult Analyse(MeshGraph graph, string source, IReadOnlySet<string> downNodes)
    {
        var parent = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var hops = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();

        if (graph.ContainsVertex(source) && !downNodes.Contains(source))
        {
            var queue = new Queue<string>();
            parent[source] = null;
            hops[source] = 0;
            queue.Enqueue(source);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                order.Add(current);

                foreach (var edge in graph.Neighbours(current))
                {
                    if (downNodes.Contains(edge.To) || parent.ContainsKey(edge.To))
                    {
                        continue;
                    }

                    parent[edge.To] = current;
                    hops[edge.To] = hops[current] + 1;
                    queue.Enqueue(edge.To);
                }
            }
        }

        var unreachable = graph.Vertices
            .Where(v => !parent.ContainsKey(v))
            .Select(v => new UnreachableNode(v, downNodes.Contains(v) ? UnreachableReason.Down : UnreachableReason.CutOff))
            .OrderBy(u => u.Reason)
            .ThenBy(u => u.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new ReachabilityResult(source, order, parent, hops, unreachable, []);

        // For each down node, the live neighbours that can still reach the
        // gateway are where its traffic (and traffic that used to relay
        // through it) hops instead. Closest-to-gateway first, then cheapest link.
        // The gateway itself is the destination, not a relay, so it is never
        // offered as a "via" neighbour.
        var reroutes = unreachable
            .Where(u => u.Reason == UnreachableReason.Down)
            .SelectMany(u => graph.Neighbours(u.Id)
                .Where(e => result.IsReachable(e.To) && !string.Equals(e.To, source, StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => hops[e.To])
                .ThenBy(e => e.Weight)
                .Select(e => new RerouteOption(u.Id, e.To, result.PathToSource(e.To))))
            .ToList();

        return new ReachabilityResult(source, order, parent, hops, unreachable, reroutes);
    }
}
