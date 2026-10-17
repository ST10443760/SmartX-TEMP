using SmartX.Shared.Enums;

namespace SmartX.Shared.Dtos;

/// <summary>One entry currently sitting in the gateway's active alert set.</summary>
public sealed class ActiveAlert
{
    public required string MacAddress { get; init; }

    public required string DeviceName { get; init; }

    public required AlertKind Kind { get; init; }

    public required DateTime SinceUtc { get; init; }
}
