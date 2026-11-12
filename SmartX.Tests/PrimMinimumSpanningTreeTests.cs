using SmartX.Shared.Graphs;

namespace SmartX.Tests;

public class PrimMinimumSpanningTreeTests
{
    private const string Gw = MeshGraph.GatewayId;

    private static readonly HashSet<string> NoneDown = [];

    private static HashSet<string> Down(params string[] ids) => new(ids, StringComparer.OrdinalIgnoreCase);

    private static MeshGraph Graph(string[] vertices, params (string A, string B, double W)[] links)
    {
        var graph = new MeshGraph();
        foreach (var v in vertices)
        {
            graph.AddVertex(v);
        }

        foreach (var (a, b, w) in links)
        {
            graph.AddEdge(a, b, w);
        }

        return graph;
    }

    // Hand-checked example (five nodes, seven links):
    //
    //   GW --4-- A
    //   |  \     |
    //   1    3   2
    //   |      \ |
    //   B --5--  C --6-- D
    //    \______7_______/
    //
    // Prim's from GW: take GW-B (1), then GW-C (3), then C-A (2), then C-D (6).
    // GW-A (4), B-C (5) and B-D (7) would each close a cycle.
    // MST total = 1 + 3 + 2 + 6 = 12.
    private static MeshGraph HandChecked() => Graph(
        [Gw, "A", "B", "C", "D"],
        (Gw, "A", 4), (Gw, "B", 1), (Gw, "C", 3), ("A", "C", 2), ("B", "C", 5), ("C", "D", 6), ("B", "D", 7));

    [Fact]
    public void KnownGraph_GivesTheHandCheckedTotalAndEdges()
    {
        var mst = PrimMinimumSpanningTree.Compute(HandChecked(), Gw, NoneDown);

        Assert.Equal(12, mst.TotalCost);
        Assert.Equal(
            [(Gw, "B", 1d), (Gw, "C", 3d), ("C", "A", 2d), ("C", "D", 6d)],
            mst.Edges.Select(e => (e.From, e.To, e.Weight)));
    }

    [Fact]
    public void ConnectedGraph_HasExactlyNodesMinusOneEdges()
    {
        var graph = HandChecked();

        var mst = PrimMinimumSpanningTree.Compute(graph, Gw, NoneDown);

        Assert.Equal(graph.VertexCount - 1, mst.Edges.Count);
        Assert.Equal(1, mst.TreeCount);
        Assert.Empty(mst.Unreachable);
        Assert.Equal(graph.VertexCount, mst.Connected.Count);
    }

    [Fact]
    public void RandomConnectedGraphs_MatchABruteForceMinimum()
    {
        // Compare against trying every subset of V-1 edges on small graphs.
        var rng = new Random(23);
        for (var round = 0; round < 25; round++)
        {
            var vertices = new[] { Gw, "A", "B", "C", "D" };
            var links = new List<(string, string, double)>();
            for (var i = 1; i < vertices.Length; i++)
            {
                links.Add((vertices[rng.Next(i)], vertices[i], rng.Next(1, 20)));
            }

            for (var extra = 0; extra < 4; extra++)
            {
                var a = vertices[rng.Next(5)];
                var b = vertices[rng.Next(5)];
                if (a != b && !links.Exists(l => (l.Item1 == a && l.Item2 == b) || (l.Item1 == b && l.Item2 == a)))
                {
                    links.Add((a, b, rng.Next(1, 20)));
                }
            }

            var mst = PrimMinimumSpanningTree.Compute(Graph(vertices, [.. links]), Gw, NoneDown);

            Assert.Equal(BruteForceMinimum(vertices, links), mst.TotalCost);
        }
    }

    [Fact]
    public void DisconnectedGraph_GivesASpanningForest_AndListsTheIslandAsUnreachable()
    {
        // GW-A-B is one component; X-Y is an island with no link to the gateway.
        var graph = Graph([Gw, "A", "B", "X", "Y"], (Gw, "A", 2), ("A", "B", 3), ("X", "Y", 4));

        var mst = PrimMinimumSpanningTree.Compute(graph, Gw, NoneDown);

        Assert.Equal(2, mst.TreeCount);
        Assert.Equal(5, mst.TotalCost); // only the gateway's tree
        Assert.Equal(9, mst.ForestCost);
        Assert.Equal(["X", "Y"], mst.Unreachable.Order());
        Assert.Equal(graph.VertexCount - mst.TreeCount, mst.Edges.Count);
        Assert.False(mst.InRootTree("X"));
    }

    [Fact]
    public void WhenANodeGoesDown_TheTreeIsRecomputedAroundIt()
    {
        var graph = HandChecked();

        var before = PrimMinimumSpanningTree.Compute(graph, Gw, NoneDown);
        var after = PrimMinimumSpanningTree.Compute(graph, Gw, Down("C"));

        // With C down: GW-B (1), GW-A (4), B-D (7) = 12, and C is excluded.
        Assert.True(before.TryGetParentEdge("A", out var oldParent));
        Assert.Equal("C", oldParent.From);

        Assert.Equal(["C"], after.Excluded);
        Assert.Equal(3, after.Edges.Count);
        Assert.Equal(12, after.TotalCost);
        Assert.True(after.TryGetParentEdge("A", out var newParent));
        Assert.Equal(Gw, newParent.From);
        Assert.True(after.TryGetParentEdge("D", out var dParent));
        Assert.Equal("B", dParent.From);
        Assert.DoesNotContain(after.Edges, e => e.From == "C" || e.To == "C");
    }

    [Fact]
    public void ANodeCutOffByADownNode_EndsUpInItsOwnTree()
    {
        // GW - A - B : with A down, B is up but has no way home.
        var graph = Graph([Gw, "A", "B"], (Gw, "A", 1), ("A", "B", 1));

        var mst = PrimMinimumSpanningTree.Compute(graph, Gw, Down("A"));

        Assert.Equal(["B"], mst.Unreachable);
        Assert.Equal([Gw], mst.Connected);
        Assert.Equal(0, mst.TotalCost);
        Assert.Empty(mst.Edges);
    }

    [Fact]
    public void GatewayOnly_GivesAnEmptyTree()
    {
        var mst = PrimMinimumSpanningTree.Compute(Graph([Gw]), Gw, NoneDown);

        Assert.Empty(mst.Edges);
        Assert.Equal(0, mst.TotalCost);
        Assert.Equal([Gw], mst.Connected);
    }

    private static double BruteForceMinimum(string[] vertices, List<(string A, string B, double W)> links)
    {
        var best = double.MaxValue;
        var need = vertices.Length - 1;

        for (var mask = 0; mask < 1 << links.Count; mask++)
        {
            if (int.PopCount(mask) != need)
            {
                continue;
            }

            // Union-find style check that the chosen edges connect everything without a cycle.
            var group = vertices.ToDictionary(v => v, v => v);
            string Find(string v) => group[v] == v ? v : group[v] = Find(group[v]);

            var ok = true;
            double total = 0;
            for (var i = 0; i < links.Count && ok; i++)
            {
                if ((mask & (1 << i)) == 0)
                {
                    continue;
                }

                var (ra, rb) = (Find(links[i].A), Find(links[i].B));
                ok = ra != rb;
                group[ra] = rb;
                total += links[i].W;
            }

            if (ok)
            {
                best = Math.Min(best, total);
            }
        }

        return best;
    }
}
