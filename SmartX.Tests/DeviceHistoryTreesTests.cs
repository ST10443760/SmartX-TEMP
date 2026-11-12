using SmartX.Shared.Trees;

namespace SmartX.Tests;

public class DeviceHistoryTreesTests
{
    private static readonly DateTime T0 = new(2026, 11, 12, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Query_ReturnsOnlyThatDevicesReadingsInRange()
    {
        var trees = new DeviceHistoryTrees<int>();
        for (var i = 0; i < 10; i++)
        {
            trees.Add("AA", T0.AddMinutes(i), i);
            trees.Add("BB", T0.AddMinutes(i), 100 + i);
        }

        var result = trees.Query("aa", T0.AddMinutes(3), T0.AddMinutes(5));

        Assert.Equal([3, 4, 5], result.Items.Select(kv => kv.Value));
        Assert.Equal(10, result.TotalReadings);
    }

    [Fact]
    public void Query_ForAnUnknownDevice_IsEmpty()
    {
        var trees = new DeviceHistoryTrees<int>();

        var result = trees.Query("ZZ", T0, T0.AddHours(1));

        Assert.Empty(result.Items);
        Assert.Equal(0, trees.CountFor("ZZ"));
        Assert.Null(trees.SpanFor("ZZ"));
    }

    [Fact]
    public void Add_BeyondCapacity_DropsTheOldestReadings()
    {
        var trees = new DeviceHistoryTrees<int>(capacityPerDevice: 5);
        for (var i = 0; i < 8; i++)
        {
            trees.Add("AA", T0.AddSeconds(i), i);
        }

        Assert.Equal(5, trees.CountFor("AA"));
        Assert.Equal((T0.AddSeconds(3), T0.AddSeconds(7)), trees.SpanFor("AA"));
        Assert.Equal([3, 4, 5, 6, 7], trees.Query("AA", T0, T0.AddMinutes(1)).Items.Select(kv => kv.Value));
    }

    [Fact]
    public void ConcurrentWritersAndReaders_DoNotCorruptTheTrees()
    {
        var trees = new DeviceHistoryTrees<int>();

        Parallel.For(0, 4_000, i =>
        {
            trees.Add(i % 2 == 0 ? "AA" : "BB", T0.AddSeconds(i), i);
            _ = trees.Query("AA", T0, T0.AddSeconds(i));
        });

        Assert.Equal(2_000, trees.CountFor("AA"));
        Assert.Equal(2_000, trees.CountFor("BB"));
    }
}
