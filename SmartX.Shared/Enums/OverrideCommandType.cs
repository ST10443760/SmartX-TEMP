namespace SmartX.Shared.Enums;

/// <summary>Manual override commands an operator can issue from the command stream.</summary>
public enum OverrideCommandType
{
    TriggerPump = 0,
    StopPump = 1,
    ToggleRelay = 2,
    OpenValve = 3,
    CloseAllValves = 4
}
