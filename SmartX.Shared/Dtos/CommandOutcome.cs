namespace SmartX.Shared.Dtos;

/// <summary>Result of executing or undoing an override, also pushed over SignalR.</summary>
public sealed class CommandOutcome
{
    public required bool Success { get; init; }

    public required string Message { get; init; }

    public bool IsUndo { get; init; }

    /// <summary>The command that ran (or was undone), if any.</summary>
    public CommandRecord? Record { get; init; }

    public List<ActuatorState> Actuators { get; init; } = [];

    /// <summary>Command stack, most recent first.</summary>
    public List<CommandRecord> History { get; init; } = [];
}
