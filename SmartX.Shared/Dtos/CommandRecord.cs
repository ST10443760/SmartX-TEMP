using SmartX.Shared.Enums;

namespace SmartX.Shared.Dtos;

/// <summary>
/// One entry on the override command stack. It stores the state of every
/// actuator the command touched before and after it ran, which is exactly
/// enough to reverse it - undoing "close all valves" writes
/// <see cref="PreviousStates"/> back, so only the valves that were open
/// before are reopened.
/// </summary>
public sealed class CommandRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required OverrideCommandType CommandType { get; init; }

    public string? TargetMac { get; init; }

    public required string Description { get; init; }

    public required DateTime IssuedUtc { get; init; }

    /// <summary>MAC -> on/off before the command ran.</summary>
    public required Dictionary<string, bool> PreviousStates { get; init; }

    /// <summary>MAC -> on/off after the command ran.</summary>
    public required Dictionary<string, bool> NewStates { get; init; }
}
