using SmartX.Shared.Enums;

namespace SmartX.Shared.Dtos;

/// <summary>
/// One node's worth of state for the dashboard's live "Mesh Pulse" engagement
/// widget - a simulated snapshot of a sensor's current reading and whether it
/// currently needs operator attention. The client polls a batch of these and
/// renders each as an animated pulse rather than a static progress bar.
/// </summary>
public sealed class TelemetryPulse
{
    public required string SensorId { get; init; }

    public required SensorCategory Category { get; init; }

    public required double Value { get; init; }

    public required string Unit { get; init; }

    public required TelemetryQuality Quality { get; init; }

    public required DateTimeOffset TimestampUtc { get; init; }

    /// <summary>0-100 anomaly confidence used to drive pulse intensity/colour client-side.</summary>
    public required int AnomalyScore { get; init; }
}
