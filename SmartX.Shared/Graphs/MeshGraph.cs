namespace SmartX.Shared.Graphs;

/// <summary>
/// The physical mesh as an undirected, weighted graph stored as an
/// adjacency list: a <see cref="Dictionary{TKey,TValue}"/> from each vertex
/// (an ESP32 node's MAC address, or <see cref="GatewayId"/>) to the list of
/// links leaving it.
///
/// An adjacency list fits a mesh because each node only has a few
/// neighbours: memory is O(V + E) rather than the O(V²) of a matrix, and
/// listing a node's neighbours, which is what BFS and Prim's do all the time,
/// costs O(degree).
///
/// Rules:
/// <list type="bullet">
///   <item>Vertex ids are case-insensitive, like MAC lookups elsewhere in the gateway.</item>
///   <item>Adding an edge to a vertex that does not exist throws <see cref="KeyNotFoundException"/>, because it means the topology definition is wrong and should fail loudly.</item>
///   <item>A second edge between the same two vertices (in either direction) is ignored and <see cref="AddEdge"/> returns false. The first weight is kept, so there are never parallel links.</item>
///   <item>Self-loops and negative or non-finite weights are rejected.</item>
///   <item>Reading the neighbours of an unknown vertex returns an empty list rather than throwing.</item>
/// </list>
///
/// Not thread-safe on its own. The API builds a fresh graph for every
/// topology request and never changes it afterwards, so it is never written
/// while being read.
/// </summary>
public sealed class MeshGraph
{
    /// <summary>Vertex id of the central gateway every route ends at.</summary>
    public const string GatewayId = "GATEWAY";

    private readonly Dictionary<string, List<MeshEdge>> _adjacency = new(StringComparer.OrdinalIgnoreCase);

    public int VertexCount => _adjacency.Count;

    /// <summary>Number of undirected links (each stored twice internally, counted once).</summary>
    public int EdgeCount { get; private set; }

    public IReadOnlyCollection<string> Vertices => _adjacency.Keys;

    /// <summary>Adds a vertex. Returns false if it already exists.</summary>
    public bool AddVertex(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _adjacency.TryAdd(id, []);
    }

    public bool ContainsVertex(string id) => _adjacency.ContainsKey(id);

    /// <summary>
    /// Adds an undirected link between two existing vertices. Returns false
    /// (and changes nothing) if the link already exists.
    /// </summary>
    public bool AddEdge(string a, string b, double weight)
    {
        if (!_adjacency.TryGetValue(a, out var fromA))
        {
            throw new KeyNotFoundException($"Vertex '{a}' is not in the graph.");
        }

        if (!_adjacency.TryGetValue(b, out var fromB))
        {
            throw new KeyNotFoundException($"Vertex '{b}' is not in the graph.");
        }

        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("A node cannot link to itself.", nameof(b));
        }

        if (!double.IsFinite(weight) || weight < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(weight), weight, "Link weight must be a finite, non-negative power cost.");
        }

        if (fromA.Exists(e => string.Equals(e.To, b, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        fromA.Add(new MeshEdge(a, b, weight));
        fromB.Add(new MeshEdge(b, a, weight));
        EdgeCount++;
        return true;
    }

    /// <summary>The links leaving a vertex. Empty for an unknown vertex.</summary>
    public IReadOnlyList<MeshEdge> Neighbours(string id)
        => _adjacency.TryGetValue(id, out var edges) ? edges : [];

    public bool TryGetWeight(string a, string b, out double weight)
    {
        foreach (var edge in Neighbours(a))
        {
            if (string.Equals(edge.To, b, StringComparison.OrdinalIgnoreCase))
            {
                weight = edge.Weight;
                return true;
            }
        }

        weight = 0;
        return false;
    }

    /// <summary>Every undirected link exactly once.</summary>
    public IEnumerable<MeshEdge> Edges()
        => _adjacency.Values.SelectMany(edges => edges).Where(e => string.Compare(e.From, e.To, StringComparison.OrdinalIgnoreCase) < 0);
}
