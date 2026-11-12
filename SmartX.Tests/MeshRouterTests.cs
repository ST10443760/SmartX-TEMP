using SmartX.Shared.Graphs;

namespace SmartX.Tests;

public class MeshRouterTests
{
    private const string Gw = MeshGraph.GatewayId;
    private const string MacA = "24:6F:28:A1:00:01";
    private const string MacB = "24:6F:28:A1:00:02";
    private const string MacC = "24:6F:28:A1:00:03";

    // GW --3-- A --1-- C
    //  \               |
    //   5---- B ---2---+
    // MST: GW-A (3), A-C (1), C-B (2). B's route: B -> C -> A -> GW, total 6.
    private static MeshGraph Graph()
    {
        var graph = new MeshGraph();
        foreach (var v in new[] { Gw, MacA, MacB, MacC })
        {
            graph.AddVertex(v);
        }

        graph.AddEdge(Gw, MacA, 3);
        graph.AddEdge(MacA, MacC, 1);
        graph.AddEdge(Gw, MacB, 5);
        graph.AddEdge(MacB, MacC, 2);
        return graph;
    }

    private static SpanningTreeResult Mst(params string[] down)
        => PrimMinimumSpanningTree.Compute(Graph(), Gw, new HashSet<string>(down, StringComparer.OrdinalIgnoreCase));

    [Fact]
    public void RouteByMac_ReturnsTheHopsInOrderWithEachCostAndTheTotal()
    {
        var route = MeshRouter.RouteToRoot(Mst(), MacB);

        Assert.True(route.Reachable);
        Assert.Equal(
            [new RouteHop(MacB, MacC, 2), new RouteHop(MacC, MacA, 1), new RouteHop(MacA, Gw, 3)],
            route.Hops);
        Assert.Equal(6, route.TotalCost);
        Assert.Equal([MacB, MacC, MacA, Gw], route.Path);
    }

    [Fact]
    public void RouteLookup_IsCaseInsensitiveOnTheMac()
    {
        var route = MeshRouter.RouteToRoot(Mst(), MacB.ToLowerInvariant());

        Assert.True(route.Reachable);
        Assert.Equal(3, route.Hops.Count);
    }

    [Fact]
    public void WhenARelayOnTheRouteGoesDown_TheRouteChanges()
    {
        // With C down, B can only use its own direct (more expensive) link.
        var route = MeshRouter.RouteToRoot(Mst(MacC), MacB);

        Assert.Equal([new RouteHop(MacB, Gw, 5)], route.Hops);
        Assert.Equal(5, route.TotalCost);
    }

    [Fact]
    public void RouteForADownOrUnknownNode_IsUnreachableWithNoHops()
    {
        Assert.False(MeshRouter.RouteToRoot(Mst(MacB), MacB).Reachable);
        Assert.False(MeshRouter.RouteToRoot(Mst(), "FF:FF:FF:FF:FF:FF").Reachable);
        Assert.Empty(MeshRouter.RouteToRoot(Mst(), "FF:FF:FF:FF:FF:FF").Path);
    }

    [Fact]
    public void RouteForTheGateway_IsEmptyButReachable()
    {
        var route = MeshRouter.RouteToRoot(Mst(), Gw);

        Assert.True(route.Reachable);
        Assert.Empty(route.Hops);
        Assert.Equal(0, route.TotalCost);
    }
}
