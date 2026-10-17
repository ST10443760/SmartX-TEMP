using SmartX.Shared.Enums;

namespace SmartX.Shared.Dtos;

/// <summary>
/// One telemetry reading after the background worker has processed it -
/// what the dashboard's live feed and timeline chart render. Bool valve
/// states travel as 1/0 in <see cref="Value"/> so every reading can share a
/// single numeric timeline.
/// </summary>
public sealed class LiveReading
{
    public required string MacAddress { get; init; }

    public required string DeviceName { get; init; }

    public required SensorCategory Category { get; init; }

    public required ReadingKind Kind { get; init; }

    public required double Value { get; init; }

    public required string Unit { get; init; }

    public required DateTime TimestampUtc { get; init; }

    public AlertSeverity Severity { get; init; } = AlertSeverity.Normal;

    /// <summary>"Priority" if the reading jumped the queue, otherwise "FIFO".</summary>
    public string Lane { get; init; } = "FIFO";

    /// <summary>Order in which the worker processed this reading (1, 2, 3...).</summary>
    public long ProcessedSequence { get; init; }
}
