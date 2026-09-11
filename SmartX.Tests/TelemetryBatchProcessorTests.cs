using SmartX.Shared.Models;
using SmartX.Shared.Processing;

namespace SmartX.Tests;

public class TelemetryBatchProcessorTests
{
    [Fact]
    public void FlattenBatches_JaggedInput_ProducesChronologicalFlatList()
    {
        // A jagged array: burst 0 has 2 readings, burst 1 has 4, burst 2 has 1.
        double[][] rawBatches =
        [
            [21.5, 21.7],
            [22.0, 22.4, 22.1, 21.9],
            [21.8]
        ];

        var start = new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero);
        var interval = TimeSpan.FromSeconds(5);

        List<TelemetryPacket<double>> flattened =
            TelemetryBatchProcessor.FlattenBatches(rawBatches, "Moisture-04", "%", start, interval);

        Assert.Equal(7, flattened.Count);
        Assert.Equal(21.5, flattened[0].Value);
        Assert.Equal(21.8, flattened[^1].Value);
        Assert.Equal(start, flattened[0].TimestampUtc);
        Assert.Equal(start + (interval * 6), flattened[^1].TimestampUtc);
        Assert.All(flattened, p => Assert.Equal("Moisture-04", p.SensorId));
    }

    [Fact]
    public void FlattenBatches_EmptyBursts_AreSkippedWithoutError()
    {
        double[][] rawBatches = [[], [1.0], []];

        var flattened = TelemetryBatchProcessor.FlattenBatches(
            rawBatches, "Power-02", "W", DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));

        Assert.Single(flattened);
    }

    [Fact]
    public void BuildLatestReadingsGrid_ReturnsRectangularArrayOfLastNReadings()
    {
        var history = new List<TelemetryPacket<double>>();
        for (var i = 0; i < 10; i++)
        {
            history.Add(new TelemetryPacket<double> { SensorId = "S1", Unit = "%", Value = i, TimestampUtc = DateTimeOffset.UtcNow });
        }

        var grid = TelemetryBatchProcessor.BuildLatestReadingsGrid([history], windowSize: 3);

        Assert.Equal(1, grid.GetLength(0));
        Assert.Equal(3, grid.GetLength(1));
        Assert.Equal(7, grid[0, 0]);
        Assert.Equal(8, grid[0, 1]);
        Assert.Equal(9, grid[0, 2]);
    }

    [Fact]
    public void BuildLatestReadingsGrid_ShortHistory_PadsWithNaN()
    {
        var history = new List<TelemetryPacket<double>>
        {
            new() { SensorId = "S1", Unit = "%", Value = 5, TimestampUtc = DateTimeOffset.UtcNow }
        };

        var grid = TelemetryBatchProcessor.BuildLatestReadingsGrid([history], windowSize: 3);

        Assert.True(double.IsNaN(grid[0, 0]));
        Assert.True(double.IsNaN(grid[0, 1]));
        Assert.Equal(5, grid[0, 2]); // most recent (and only) reading lands in the last column
    }
}
