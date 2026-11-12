namespace SmartX.Shared.Graphs;

/// <summary>
/// Result of <see cref="PrimMinimumSpanningTree.Compute"/>. Each tree edge is
/// stored as From = parent (nearer the root) and To = child.
/// </summary>
public sealed class SpanningTreeResult(
    string root,
    IReadOnlyList<MeshEdge> edges,
    IReadOnlyDictionary<string, MeshEdge> edgeToParent,
    IReadOnlyDictionary<string, string> componentRoot,
    IReadOnlyList<string> excluded)
{
    public string Root { get; } = root;

    /// <summary>Every edge of the spanning forest, in the order Prim's added them.</summary>
    public IReadOnlyList<MeshEdge> Edges { get; } = edges;

    /// <summary>Edges of the tree that contains the root (the gateway).</summary>
    public IReadOnlyList<MeshEdge> RootTreeEdges => Edges.Where(e => InRootTree(e.To)).ToList();

    /// <summary>Total power cost of the root's tree: the cheapest way to connect every reachable node to the gateway.</summary>
    public double TotalCost => Math.Round(RootTreeEdges.Sum(e => e.Weight), 2);

    /// <summary>Total cost of the whole forest, including islands cut off from the gateway.</summary>
    public double ForestCost => Math.Round(Edges.Sum(e => e.Weight), 2);

    /// <summary>Live nodes in the root's tree, root included.</summary>
    public IReadOnlyList<string> Connected => componentRoot.Where(kv => string.Equals(kv.Value, Root, StringComparison.OrdinalIgnoreCase)).Select(kv => kv.Key).ToList();

    /// <summary>Live nodes that ended up in a different tree of the forest: up, but with no live path to the gateway.</summary>
    public IReadOnlyList<string> Unreachable => componentRoot.Where(kv => !string.Equals(kv.Value, Root, StringComparison.OrdinalIgnoreCase)).Select(kv => kv.Key).ToList();

    /// <summary>Down nodes that were left out before the algorithm ran.</summary>
    public IReadOnlyList<string> Excluded { get; } = excluded;

    /// <summary>Number of separate trees in the forest (1 when every live node can reach the gateway).</summary>
    public int TreeCount => componentRoot.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count();

    public bool InRootTree(string id) => componentRoot.TryGetValue(id, out var r) && string.Equals(r, Root, StringComparison.OrdinalIgnoreCase);

    /// <summary>The tree edge joining a node to its parent. False for a tree's root or a node not in the forest.</summary>
    public bool TryGetParentEdge(string id, out MeshEdge edge) => edgeToParent.TryGetValue(id, out edge);
}

/// <summary>
/// Prim's algorithm for the minimum spanning tree (MST) of the live mesh:
/// the set of links that connects every reachable node to the gateway with
/// the lowest total power cost. Each node's path to the gateway inside this
/// tree is its power-efficient route.
///
/// How it works: start with only the gateway in the tree. Push every link
/// leaving the tree onto a min-heap (<see cref="PriorityQueue{TElement,TPriority}"/>)
/// keyed by weight. Repeatedly pop the cheapest link; if it leads to a node
/// already in the tree it is skipped (it would make a cycle), otherwise that
/// node joins the tree through that link and its own links are pushed. This
/// is the "lazy" version of Prim's: stale links are left in the heap and
/// skipped when popped, instead of being decreased in place. It runs in
/// O(E log E), which for a mesh with a few links per node is O(E log V).
///
/// Down nodes are removed before it starts, so the tree only uses live
/// links. If some live nodes cannot be reached from the gateway, the heap
/// simply runs dry. Prim's is then started again from the first unvisited
/// node, as many times as needed, giving a spanning forest. Those extra
/// trees are reported as unreachable rather than causing an error or a loop.
/// Every node is added at most once, so the loop always ends.
///
/// Ties: equal weights are broken by insertion order (a running counter in
/// the priority), so the same input always gives the same tree.
///
/// Sources: written from general knowledge of Prim's algorithm, as presented
/// in Cormen, Leiserson, Rivest and Stein, Introduction to Algorithms (3rd
/// ed.), section 23.2. The heap is .NET's PriorityQueue&lt;TElement,TPriority&gt;
/// (Microsoft Learn, "PriorityQueue&lt;TElement,TPriority&gt; Class",
/// https://learn.microsoft.com/dotnet/api/system.collections.generic.priorityqueue-2).
/// </summary>
public static class PrimMinimumSpanningTree
{
    public static SpanningTreeResult Compute(MeshGraph graph, string root, IReadOnlySet<string> downNodes)
    {
        var edges = new List<MeshEdge>();
        var edgeToParent = new Dictionary<string, MeshEdge>(StringComparer.OrdinalIgnoreCase);
        var componentRoot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var excluded = graph.Vertices.Where(downNodes.Contains).Order(StringComparer.OrdinalIgnoreCase).ToList();

        // The gateway's tree first, then any islands, in a stable order.
        var starts = new List<string>();
        if (graph.ContainsVertex(root) && !downNodes.Contains(root))
        {
            starts.Add(root);
        }

        starts.AddRange(graph.Vertices
            .Where(v => !downNodes.Contains(v) && !string.Equals(v, root, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase));

        var heap = new PriorityQueue<MeshEdge, (double Weight, long Order)>();
        long counter = 0;

        foreach (var start in starts)
        {
            if (componentRoot.ContainsKey(start))
            {
                continue;
            }

            componentRoot[start] = start;
            PushLinks(start);

            while (heap.TryDequeue(out var edge, out _))
            {
                if (componentRoot.ContainsKey(edge.To))
                {
                    // Both ends already in the tree: this link would make a cycle.
                    continue;
                }

                componentRoot[edge.To] = start;
                edgeToParent[edge.To] = edge;
                edges.Add(edge);
                PushLinks(edge.To);
            }
        }

        return new SpanningTreeResult(root, edges, edgeToParent, componentRoot, excluded);

        void PushLinks(string node)
        {
            foreach (var edge in graph.Neighbours(node))
            {
                if (!downNodes.Contains(edge.To) && !componentRoot.ContainsKey(edge.To))
                {
                    heap.Enqueue(edge, (edge.Weight, counter++));
                }
            }
        }
    }
}
