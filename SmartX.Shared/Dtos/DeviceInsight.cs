using SmartX.Shared.Enums;

namespace SmartX.Shared.Dtos;

/// <summary>A device flagged proactively from the live data, with the reason.</summary>
public sealed class DeviceInsight
{
    public required string MacAddress { get; init; }

    public required string DeviceName { get; init; }

    public required InsightKind Kind { get; init; }

    public required AlertSeverity Severity { get; init; }

    public required string Reason { get; init; }
}
