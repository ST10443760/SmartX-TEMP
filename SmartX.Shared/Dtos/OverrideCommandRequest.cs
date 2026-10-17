using SmartX.Shared.Enums;

namespace SmartX.Shared.Dtos;

/// <summary>What the dashboard posts to issue a manual override.</summary>
public sealed class OverrideCommandRequest
{
    public required OverrideCommandType CommandType { get; init; }

    /// <summary>The actuator's MAC address; not needed for fleet-wide commands like CloseAllValves.</summary>
    public string? TargetMac { get; init; }
}
