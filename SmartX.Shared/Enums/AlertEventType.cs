namespace SmartX.Shared.Enums;

/// <summary>What happened to an alert: newly raised, cleared, or ignored because it was already active.</summary>
public enum AlertEventType
{
    Raised = 0,
    Recovered = 1,
    DuplicateSuppressed = 2
}
