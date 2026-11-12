using SmartX.Client.Services;
using SmartX.Shared.Dtos;
using SmartX.Shared.Enums;

namespace SmartX.Tests;

public class LiveStateContainerTests
{
    private static LiveReading Reading(long sequence) => new()
    {
        MacAddress = "AA",
        DeviceName = "Moisture Sensor A",
        Category = SensorCategory.Environmental,
        Kind = ReadingKind.Moisture,
        Value = 40,
        Unit = "%",
        TimestampUtc = DateTime.UtcNow,
        ProcessedSequence = sequence
    };

    [Fact]
    public void ReadingInSnapshotAndPushedAgain_AppearsOnlyOnce()
    {
        // Regression: the duplicate made Blazor throw "more than one sibling
        // has the same key" and stopped the dashboard rendering.
        var state = new LiveStateContainer();
        state.ApplySnapshot(new StreamSnapshot { RecentReadings = [Reading(2), Reading(1)], Stats = new QueueStats { ProcessedTotal = 2 } });

        state.ApplyReading(Reading(2));
        state.ApplyReading(Reading(3));

        Assert.Equal([3L, 2L, 1L], state.Feed.Select(r => r.ProcessedSequence));
    }

    [Fact]
    public void Snapshot_FromARestartedGateway_ReplacesTheOldFeed()
    {
        var state = new LiveStateContainer();
        state.ApplyReading(Reading(500));

        state.ApplySnapshot(new StreamSnapshot { RecentReadings = [Reading(1)], Stats = new QueueStats { ProcessedTotal = 1 } });

        Assert.Equal([1L], state.Feed.Select(r => r.ProcessedSequence));
    }

    [Fact]
    public void Filters_SurviveBecauseTheyLiveInTheContainer()
    {
        var state = new LiveStateContainer { SearchText = "Pump", CategoryFilter = SensorCategory.Actuator, AlertsOnly = true };

        state.ApplySnapshot(new StreamSnapshot());

        Assert.Equal("Pump", state.SearchText);
        Assert.Equal(SensorCategory.Actuator, state.CategoryFilter);
        Assert.True(state.AlertsOnly);
    }

    [Fact]
    public void OlderTopologySnapshot_ArrivingLate_DoesNotOverwriteTheNewerOne()
    {
        var state = new LiveStateContainer();
        var t0 = new DateTime(2026, 11, 12, 15, 0, 0, DateTimeKind.Utc);
        var newer = new TopologySnapshot { GeneratedUtc = t0.AddSeconds(1), Mst = new MstReport { TotalCost = 38.8 } };
        var older = new TopologySnapshot { GeneratedUtc = t0, Mst = new MstReport { TotalCost = 37.2 } };

        Assert.True(state.SetTopology(newer));
        Assert.False(state.SetTopology(older));

        Assert.Equal(38.8, state.Topology!.Mst.TotalCost);
    }

    [Fact]
    public void TopologySelectionAndHistoryRange_AreKeptInTheSingleton()
    {
        var state = new LiveStateContainer();
        var from = new DateTime(2026, 11, 12, 14, 0, 0);

        state.TopologySelectedMac = "24:6F:28:A1:00:03";
        state.TopologySearch = "Moisture";
        state.HistoryFromLocal = from;
        state.HighlightMst = false;

        // Pages only read the container, so navigating away and back sees the same values.
        Assert.Equal("24:6F:28:A1:00:03", state.TopologySelectedMac);
        Assert.Equal("Moisture", state.TopologySearch);
        Assert.Equal(from, state.HistoryFromLocal);
        Assert.False(state.HighlightMst);
    }
}
