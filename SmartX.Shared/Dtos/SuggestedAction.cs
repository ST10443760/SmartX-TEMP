using SmartX.Shared.Enums;

namespace SmartX.Shared.Dtos;

/// <summary>One "you will probably do this next" chip, with the evidence behind it.</summary>
public sealed class SuggestedAction
{
    public required ActivityKind Kind { get; init; }

    /// <summary>What the chip says, e.g. "Pump A" (search) or "Trigger Pump A" (command).</summary>
    public required string Label { get; init; }

    /// <summary>Plain-English reason, e.g. "After searching Moisture Sensor B you searched Pump A 5 of 6 times".</summary>
    public required string Reason { get; init; }

    /// <summary>Share of the times the trigger was followed by this action (0-1).</summary>
    public required double Confidence { get; init; }

    public required int Occurrences { get; init; }

    public OverrideCommandType? CommandType { get; init; }

    public string? TargetMac { get; init; }
}
