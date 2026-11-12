using SmartX.Shared.Trees;

namespace SmartX.Tests;

public class AvlTreeTests
{
    private static readonly DateTime T0 = new(2026, 11, 12, 14, 0, 0, DateTimeKind.Utc);

    private static DateTime At(int seconds) => T0.AddSeconds(seconds);

    private static AvlTree<int> Build(params int[] seconds)
    {
        var tree = new AvlTree<int>();
        foreach (var s in seconds)
        {
            tree.Insert(At(s), s);
        }

        return tree;
    }

    [Fact]
    public void InOrder_ReturnsReadingsSortedByTimestamp_RegardlessOfInsertOrder()
    {
        var tree = Build(50, 10, 40, 20, 30, 0);

        Assert.Equal([0, 10, 20, 30, 40, 50], tree.InOrder().Select(kv => kv.Value));
        Assert.Equal(6, tree.Count);
        Assert.True(tree.IsBalanced());
    }

    [Fact]
    public void ManySequentialInserts_KeepTheHeightLogarithmic()
    {
        // Sorted input is the worst case for a plain BST (it becomes a linked
        // list of height n). Telemetry arrives in roughly this order.
        const int n = 10_000;
        var tree = new AvlTree<int>();
        for (var i = 0; i < n; i++)
        {
            tree.Insert(At(i), i);
        }

        // An AVL tree's height is below 1.44 * log2(n + 2).
        var bound = 1.44 * Math.Log2(n + 2);
        Assert.True(tree.Height <= bound, $"height {tree.Height} exceeds AVL bound {bound:0.0}");
        Assert.True(tree.IsBalanced());
        Assert.Equal(n, tree.Count);
    }

    [Fact]
    public void LeftLeftCase_IsFixedWithASingleRightRotation()
    {
        var tree = Build(30, 20, 10);

        Assert.Equal(At(20), tree.RootKey);
        Assert.Equal(1, tree.RightRotations);
        Assert.Equal(0, tree.LeftRotations + tree.LeftRightRotations + tree.RightLeftRotations);
        Assert.Equal(2, tree.Height);
    }

    [Fact]
    public void RightRightCase_IsFixedWithASingleLeftRotation()
    {
        var tree = Build(10, 20, 30);

        Assert.Equal(At(20), tree.RootKey);
        Assert.Equal(1, tree.LeftRotations);
        Assert.Equal(0, tree.RightRotations + tree.LeftRightRotations + tree.RightLeftRotations);
    }

    [Fact]
    public void LeftRightCase_IsFixedWithADoubleRotation()
    {
        var tree = Build(30, 10, 20);

        Assert.Equal(At(20), tree.RootKey);
        Assert.Equal(1, tree.LeftRightRotations);
        Assert.Equal(0, tree.RightRotations + tree.LeftRotations + tree.RightLeftRotations);
        Assert.Equal([10, 20, 30], tree.InOrder().Select(kv => kv.Value));
    }

    [Fact]
    public void RightLeftCase_IsFixedWithADoubleRotation()
    {
        var tree = Build(10, 30, 20);

        Assert.Equal(At(20), tree.RootKey);
        Assert.Equal(1, tree.RightLeftRotations);
        Assert.Equal(0, tree.RightRotations + tree.LeftRotations + tree.LeftRightRotations);
        Assert.Equal([10, 20, 30], tree.InOrder().Select(kv => kv.Value));
    }

    [Fact]
    public void RangeQuery_MatchesAPlainListFilter_OnRandomData()
    {
        var rng = new Random(7312);
        var tree = new AvlTree<int>();
        var list = new List<(DateTime Key, int Value)>();

        for (var i = 0; i < 3_000; i++)
        {
            var key = At(rng.Next(0, 20_000));
            tree.Insert(key, i);
            list.Add((key, i));
        }

        for (var q = 0; q < 50; q++)
        {
            var a = At(rng.Next(-500, 20_500));
            var b = At(rng.Next(-500, 20_500));
            var (from, to) = a <= b ? (a, b) : (b, a);

            var expected = list.Where(e => e.Key >= from && e.Key <= to).OrderBy(e => e.Key).ThenBy(e => e.Value).Select(e => e.Value);
            var actual = tree.RangeQuery(from, to).Items.Select(kv => kv.Value);

            Assert.Equal(expected, actual);
        }

        Assert.True(tree.IsBalanced());
    }

    [Fact]
    public void RangeQuery_IncludesBothBoundaries()
    {
        var tree = Build(0, 10, 20, 30, 40);

        var result = tree.RangeQuery(At(10), At(30));

        Assert.Equal([10, 20, 30], result.Items.Select(kv => kv.Value));
    }

    [Fact]
    public void RangeQuery_OnAnEmptyTree_ReturnsNothingAndVisitsNothing()
    {
        var tree = new AvlTree<int>();

        var result = tree.RangeQuery(At(0), At(100));

        Assert.Empty(result.Items);
        Assert.Equal(0, result.NodesVisited);
        Assert.Equal(0, result.TotalReadings);
        Assert.Equal(0, tree.Height);
        Assert.Null(tree.RootKey);
    }

    [Theory]
    [InlineData(-100, -1)] // entirely before the data
    [InlineData(41, 100)] // entirely after the data
    [InlineData(11, 19)] // a gap between two readings
    public void RangeQuery_WithNoMatchingReadings_ReturnsAnEmptyList(int from, int to)
    {
        var tree = Build(0, 10, 20, 30, 40);

        Assert.Empty(tree.RangeQuery(At(from), At(to)).Items);
    }

    [Fact]
    public void RangeQuery_WithFromAfterTo_ReturnsNothing()
    {
        var tree = Build(0, 10, 20);

        var result = tree.RangeQuery(At(20), At(0));

        Assert.Empty(result.Items);
        Assert.Equal(0, result.NodesVisited);
    }

    [Fact]
    public void DuplicateTimestamps_ShareOneNode_AndAreAllReturnedInInsertOrder()
    {
        var tree = new AvlTree<string>();
        tree.Insert(At(10), "first");
        tree.Insert(At(10), "second");
        tree.Insert(At(5), "earlier");

        Assert.Equal(3, tree.Count);
        Assert.Equal(2, tree.NodeCount);
        Assert.Equal(["first", "second"], tree.RangeQuery(At(10), At(10)).Items.Select(kv => kv.Value));
        Assert.Equal(["earlier", "first", "second"], tree.InOrder().Select(kv => kv.Value));
    }

    [Fact]
    public void NarrowRangeQuery_VisitsFarFewerNodesThanTheTreeHolds()
    {
        const int n = 10_000;
        var tree = new AvlTree<int>();
        for (var i = 0; i < n; i++)
        {
            tree.Insert(At(i * 5), i);
        }

        // 10 readings out of 10,000.
        var result = tree.RangeQuery(At(25_000), At(25_045));

        Assert.Equal(10, result.Items.Count);
        Assert.Equal(n, result.TotalReadings);
        Assert.Equal(result.NodesVisited, tree.LastQueryNodesVisited);

        // Roughly two root-to-leaf paths plus the matches; nowhere near n.
        Assert.True(result.NodesVisited < 60, $"visited {result.NodesVisited} nodes");
    }

    [Fact]
    public void RemoveOldest_DropsReadingsInTimestampOrder_AndStaysBalanced()
    {
        var tree = new AvlTree<int>();
        for (var i = 0; i < 1_000; i++)
        {
            tree.Insert(At(i), i);
        }

        for (var i = 0; i < 600; i++)
        {
            Assert.True(tree.RemoveOldest());
            Assert.True(tree.IsBalanced());
        }

        Assert.Equal(400, tree.Count);
        Assert.Equal(At(600), tree.MinKey);
        Assert.Equal(Enumerable.Range(600, 400), tree.InOrder().Select(kv => kv.Value));
    }

    [Fact]
    public void RemoveOldest_RemovesOneDuplicateAtATime_AndReturnsFalseWhenEmpty()
    {
        var tree = new AvlTree<string>();
        tree.Insert(At(0), "a");
        tree.Insert(At(0), "b");

        Assert.True(tree.RemoveOldest());
        Assert.Equal(["b"], tree.InOrder().Select(kv => kv.Value));
        Assert.Equal(1, tree.NodeCount);

        Assert.True(tree.RemoveOldest());
        Assert.False(tree.RemoveOldest());
        Assert.Equal(0, tree.Count);
        Assert.Equal(0, tree.NodeCount);
    }
}
