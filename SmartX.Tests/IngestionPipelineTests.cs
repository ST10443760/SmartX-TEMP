using SmartX.Shared.Enums;
using SmartX.Shared.Realtime;

namespace SmartX.Tests;

public class IngestionPipelineTests
{
    private static QueuedReading Reading(string mac, double value, AlertSeverity severity)
        => new(mac, ReadingKind.Moisture, value, "%", DateTime.UtcNow, severity);

    [Fact]
    public void CriticalPacket_BypassesRoutinePacketsThatArrivedEarlier()
    {
        var pipeline = new IngestionPipeline();
        pipeline.Submit(Reading("routine-1", 40, AlertSeverity.Normal));
        pipeline.Submit(Reading("routine-2", 41, AlertSeverity.Normal));
        pipeline.Submit(Reading("routine-3", 42, AlertSeverity.Normal));
        var lane = pipeline.Submit(Reading("critical", 5, AlertSeverity.Critical));

        pipeline.TryTakeNext(out var first, out var firstLane);

        Assert.Equal(IngestionLane.Priority, lane);
        Assert.Equal("critical", first.MacAddress);
        Assert.Equal(IngestionLane.Priority, firstLane);
    }

    [Fact]
    public void Worker_DrainsWholePriorityQueueBeforeFifo_ThenFifoInOrder()
    {
        var pipeline = new IngestionPipeline();
        pipeline.SubmitBatch(
        [
            Reading("routine-1", 40, AlertSeverity.Normal),
            Reading("warning", 18, AlertSeverity.Warning),
            Reading("routine-2", 41, AlertSeverity.Normal),
            Reading("critical", 5, AlertSeverity.Critical)
        ]);

        var order = new List<string>();
        while (pipeline.TryTakeNext(out var next, out _))
        {
            order.Add(next.MacAddress);
        }

        Assert.Equal(["critical", "warning", "routine-1", "routine-2"], order);
    }

    [Fact]
    public void NormalReadings_GoThroughTheFifoLane()
    {
        var pipeline = new IngestionPipeline();

        var lane = pipeline.Submit(Reading("routine", 50, AlertSeverity.Normal));

        Assert.Equal(IngestionLane.Fifo, lane);
        Assert.Equal(1, pipeline.FifoDepth);
        Assert.Equal(0, pipeline.PriorityDepth);
    }

    [Theory]
    [InlineData(ReadingKind.Moisture, 45, AlertSeverity.Normal)]
    [InlineData(ReadingKind.Moisture, 18, AlertSeverity.Warning)]
    [InlineData(ReadingKind.Moisture, 6, AlertSeverity.Critical)]
    [InlineData(ReadingKind.Wattage, 400, AlertSeverity.Normal)]
    [InlineData(ReadingKind.Wattage, 4200, AlertSeverity.Critical)]
    [InlineData(ReadingKind.ValveState, 1, AlertSeverity.Normal)]
    public void SeverityClassifier_MapsReadingsToTheExpectedSeverity(ReadingKind kind, double value, AlertSeverity expected)
        => Assert.Equal(expected, SeverityClassifier.Classify(kind, value));
}
