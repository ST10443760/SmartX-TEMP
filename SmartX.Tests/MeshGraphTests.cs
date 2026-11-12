using SmartX.Shared.Graphs;

namespace SmartX.Tests;

public class MeshGraphTests
{
    private static MeshGraph WithVertices(params string[] ids)
    {
        var graph = new MeshGraph();
        foreach (var id in ids)
        {
            graph.AddVertex(id);
        }

        return graph;
    }

    [Fact]
    public void AddVertex_AddsOnce_AndRejectsADuplicate()
    {
        var graph = new MeshGraph();

        Assert.True(graph.AddVertex("A"));
        Assert.False(graph.AddVertex("a"));
        Assert.Equal(1, graph.VertexCount);
        Assert.True(graph.ContainsVertex("A"));
    }

    [Fact]
    public void AddEdge_IsUndirected_AndAppearsInBothNeighbourLists()
    {
        var graph = WithVertices("A", "B", "C");

        Assert.True(graph.AddEdge("A", "B", 2.5));
        Assert.True(graph.AddEdge("A", "C", 4));

        Assert.Equal(["B", "C"], graph.Neighbours("A").Select(e => e.To));
        Assert.Equal(["A"], graph.Neighbours("B").Select(e => e.To));
        Assert.True(graph.TryGetWeight("B", "A", out var weight));
        Assert.Equal(2.5, weight);
        Assert.Equal(2, graph.EdgeCount);
        Assert.Equal(2, graph.Edges().Count());
    }

    [Fact]
    public void AddEdge_Duplicate_InEitherDirection_IsIgnoredAndKeepsTheFirstWeight()
    {
        var graph = WithVertices("A", "B");
        graph.AddEdge("A", "B", 3);

        Assert.False(graph.AddEdge("A", "B", 9));
        Assert.False(graph.AddEdge("b", "a", 1));

        Assert.Single(graph.Neighbours("A"));
        Assert.Single(graph.Neighbours("B"));
        Assert.True(graph.TryGetWeight("A", "B", out var weight));
        Assert.Equal(3, weight);
        Assert.Equal(1, graph.EdgeCount);
    }

    [Fact]
    public void AddEdge_ToAnUnknownVertex_Throws()
    {
        var graph = WithVertices("A");

        Assert.Throws<KeyNotFoundException>(() => graph.AddEdge("A", "Z", 1));
        Assert.Throws<KeyNotFoundException>(() => graph.AddEdge("Z", "A", 1));
        Assert.Equal(0, graph.EdgeCount);
    }

    [Fact]
    public void AddEdge_SelfLoopOrInvalidWeight_IsRejected()
    {
        var graph = WithVertices("A", "B");

        Assert.Throws<ArgumentException>(() => graph.AddEdge("A", "A", 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => graph.AddEdge("A", "B", -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => graph.AddEdge("A", "B", double.NaN));
    }

    [Fact]
    public void Neighbours_OfAnUnknownVertex_IsEmpty()
    {
        var graph = WithVertices("A");

        Assert.Empty(graph.Neighbours("nobody"));
        Assert.False(graph.TryGetWeight("A", "nobody", out _));
    }
}
