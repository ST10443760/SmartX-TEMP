using SmartX.Shared.Realtime;

namespace SmartX.Tests;

public class ReadingHistoryStoreTests
{
    private static readonly DateTime T0 = new(2026, 10, 17, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Get_ReturnsReadingsInTimestampOrder_EvenWhenAddedOutOfOrder()
    {
        var store = new ReadingHistoryStore<double>();
        store.Add("AA", T0.AddSeconds(30), 3);
        store.Add("AA", T0, 1);
        store.Add("AA", T0.AddSeconds(10), 2);

        var values = store.Get("AA").Select(kv => kv.Value);

        Assert.Equal([1d, 2d, 3d], values);
    }

    [Fact]
    public void Add_BeyondCapacity_DropsTheOldestReadings()
    {
        var store = new ReadingHistoryStore<int>(capacityPerDevice: 3);
        for (var i = 0; i < 5; i++)
        {
            store.Add("AA", T0.AddSeconds(i), i);
        }

        var kept = store.Get("AA");

        Assert.Equal(3, store.CountFor("AA"));
        Assert.Equal([2, 3, 4], kept.Select(kv => kv.Value));
    }

    [Fact]
    public void Add_LateButOldestReading_IsTheOneTrimmedWhenFull()
    {
        var store = new ReadingHistoryStore<int>(capacityPerDevice: 2);
        store.Add("AA", T0.AddSeconds(10), 10);
        store.Add("AA", T0.AddSeconds(20), 20);
        store.Add("AA", T0, 0);

        Assert.Equal([10, 20], store.Get("AA").Select(kv => kv.Value));
    }

    [Fact]
    public void Add_DuplicateTimestamp_KeepsBothReadings()
    {
        var store = new ReadingHistoryStore<int>();
        store.Add("AA", T0, 1);
        store.Add("AA", T0, 2);

        Assert.Equal(2, store.CountFor("AA"));
    }

    [Fact]
    public void Get_Take_ReturnsTheNewestReadingsOldestFirst()
    {
        var store = new ReadingHistoryStore<int>();
        for (var i = 0; i < 10; i++)
        {
            store.Add("AA", T0.AddMinutes(i), i);
        }

        Assert.Equal([7, 8, 9], store.Get("AA", take: 3).Select(kv => kv.Value));
    }

    [Fact]
    public void Histories_AreKeptSeparatePerDevice()
    {
        var store = new ReadingHistoryStore<int>();
        store.Add("AA", T0, 1);
        store.Add("BB", T0, 2);

        Assert.Single(store.Get("AA"));
        Assert.Empty(store.Get("CC"));
    }
}
