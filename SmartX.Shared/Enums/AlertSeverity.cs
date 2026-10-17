namespace SmartX.Shared.Enums;

/// <summary>
/// How urgent a processed reading or alert is. The numeric value doubles as
/// the priority rank in the critical alert queue - higher is served first.
/// </summary>
public enum AlertSeverity
{
    Normal = 0,
    Warning = 1,
    Critical = 2
}
