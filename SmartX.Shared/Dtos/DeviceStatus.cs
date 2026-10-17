using SmartX.Shared.Enums;

namespace SmartX.Shared.Dtos;

/// <summary>A row in the command stream's device table: identity plus latest live state.</summary>
public sealed class DeviceStatus
{
    public required string MacAddress { get; init; }

    public required string DisplayName { get; init; }

    public required SensorCategory Category { get; init; }

    public ActuatorKind ActuatorKind { get; init; }

    public double? LastValue { get; init; }

    public string Unit { get; init; } = "";

    public DateTime? LastSeenUtc { get; init; }

    public AlertSeverity Severity { get; init; }

    public bool IsDisconnected { get; init; }

    public bool HasActiveAlert { get; init; }

    public int HistoryCount { get; init; }
}
