namespace SmartX.Shared.Enums;

/// <summary>What kind of event the action engine recorded.</summary>
public enum ActivityKind
{
    /// <summary>The operator searched for a device.</summary>
    Search = 0,

    /// <summary>The operator issued a manual override.</summary>
    Command = 1,

    /// <summary>A device crossed a threshold (e.g. moisture dropped below 20%). Never suggested, only learned from.</summary>
    Condition = 2
}
