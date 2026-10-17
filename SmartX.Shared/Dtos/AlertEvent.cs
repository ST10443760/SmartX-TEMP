using SmartX.Shared.Enums;

namespace SmartX.Shared.Dtos;

/// <summary>An alert transition pushed to dashboards (raised, recovered, or a suppressed duplicate).</summary>
public sealed class AlertEvent
{
    public required string MacAddress { get; init; }

    public required string DeviceName { get; init; }

    public required AlertKind Kind { get; init; }

    public required AlertEventType EventType { get; init; }

    public required string Message { get; init; }

    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
}
