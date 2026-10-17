using SmartX.Shared.Enums;

namespace SmartX.Shared.Dtos;

/// <summary>Payload the client posts to register a new (simulated) sensor.</summary>
public sealed class SensorRegistrationRequest
{
    public required string MacAddress { get; init; }

    /// <summary>Deployment path from root to leaf, e.g. ["Facility A", "Zone 1", "Sub-Zone B"].</summary>
    public required List<string> DeploymentPathSegments { get; init; }

    public required SensorCategory Category { get; init; }

    /// <summary>Optional friendly name; falls back to the MAC address when omitted.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Optional, actuators only - what the device drives (pump/valve/relay).</summary>
    public ActuatorKind? ActuatorKind { get; init; }
}
