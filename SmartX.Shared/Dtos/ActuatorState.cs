using SmartX.Shared.Enums;

namespace SmartX.Shared.Dtos;

/// <summary>The commanded on/off (open/closed) state of one actuator.</summary>
public sealed class ActuatorState
{
    public required string MacAddress { get; init; }

    public required string DisplayName { get; init; }

    public required ActuatorKind Kind { get; init; }

    public required bool IsOn { get; init; }
}
