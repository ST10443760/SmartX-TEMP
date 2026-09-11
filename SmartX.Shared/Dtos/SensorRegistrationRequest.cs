using SmartX.Shared.Enums;

namespace SmartX.Shared.Dtos;

/// <summary>Payload the client posts to register a new (simulated) sensor.</summary>
public sealed class SensorRegistrationRequest
{
    public required string MacAddress { get; init; }

    /// <summary>Deployment path from root to leaf, e.g. ["Facility A", "Zone 1", "Sub-Zone B"].</summary>
    public required List<string> DeploymentPathSegments { get; init; }

    public required SensorCategory Category { get; init; }
}
