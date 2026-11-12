using SmartX.Shared.Graphs;

namespace SmartX.Tests;

public class MeshReachabilityTests
{
    private const string Gw = MeshGraph.GatewayId;

    private static readonly HashSet<string> NoneDown = [];

    // GW - A - B          A and C both reach the gateway directly.
    //  \       |          B can go via A or via C.
    //   C -----+          D hangs off B only (B is D's only way home).
    //          B - D
    private static MeshGraph Diamond()
    {
        var graph = new MeshGraph();
        foreach (var v in new[] { Gw, "A", "B", "C", "D" })
        {
            graph.AddVertex(v);
        }

        graph.AddEdge(Gw, "A", 1);
        graph.AddEdge(Gw, "C", 2);
        graph.AddEdge("A", "B", 1);
        graph.AddEdge("C", "B", 3);
        graph.AddEdge("B", "D", 1);
        return graph;
    }

    private static HashSet<string> Down(params string[] ids) => new(ids, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void EverythingUp_EveryNodeIsReachable_InBreadthFirstOrder()
    {
        var result = MeshReachability.Analyse(Diamond(), Gw, NoneDown);

        Assert.Equal(Gw, result.Reachable[0]);
        Assert.Equal(["A", "C"], result.Reachable.Skip(1).Take(2).Order());
        Assert.Equal(5, result.Reachable.Count);
        Assert.Empty(result.Unreachable);
        Assert.Equal(3, result.HopsTo("D"));
        Assert.Equal(["D", "B", "A", Gw], result.PathToSource("D"));
    }

    [Fact]
    public void ADownNode_IsExcludedAndReportedAsDown()
    {
        var result = MeshReachability.Analyse(Diamond(), Gw, Down("A"));

        Assert.False(result.IsReachable("A"));
        Assert.Contains(new UnreachableNode("A", UnreachableReason.Down), result.Unreachable);
        Assert.DoesNotContain("A", result.Reachable);
    }

    [Fact]
    public void ANodeOnlyReachableThroughADownNode_IsReportedAsCutOff()
    {
        // D's only link is to B; with B down, D is up but stranded.
        var result = MeshReachability.Analyse(Diamond(), Gw, Down("B"));

        Assert.False(result.IsReachable("D"));
        Assert.Contains(new UnreachableNode("D", UnreachableReason.CutOff), result.Unreachable);
        Assert.Contains(new UnreachableNode("B", UnreachableReason.Down), result.Unreachable);
        Assert.Empty(result.PathToSource("D"));
    }

    [Fact]
    public void WhenARelayGoesDown_TrafficHopsThroughTheAlternativeNeighbour()
    {
        // Normally B reaches the gateway via A. With A down it must use C.
        var before = MeshReachability.Analyse(Diamond(), Gw, NoneDown);
        var after = MeshReachability.Analyse(Diamond(), Gw, Down("A"));

        Assert.Equal(["B", "A", Gw], before.PathToSource("B"));
        Assert.Equal(["B", "C", Gw], after.PathToSource("B"));
        Assert.Equal(["D", "B", "C", Gw], after.PathToSource("D"));

        // The reroute report for A lists its live neighbours that still reach home.
        var options = after.Reroutes.Where(r => r.DownNode == "A").ToList();
        Assert.Equal(["B"], options.Select(o => o.ViaNeighbour));
        Assert.Equal(["B", "C", Gw], options[0].PathToGateway);
    }

    [Fact]
    public void ADownNodeWithNoLiveNeighbours_HasNoRerouteOption()
    {
        var result = MeshReachability.Analyse(Diamond(), Gw, Down("B", "D"));

        Assert.DoesNotContain(result.Reroutes, r => r.DownNode == "D");
        Assert.Contains(result.Reroutes, r => r.DownNode == "B");
    }

    [Fact]
    public void AnUnknownSource_LeavesEveryNodeUnreachable_WithoutThrowing()
    {
        var result = MeshReachability.Analyse(Diamond(), "NOT-A-NODE", NoneDown);

        Assert.Empty(result.Reachable);
        Assert.Equal(5, result.Unreachable.Count);
    }
}
