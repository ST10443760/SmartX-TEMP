namespace SmartX.Shared.Enums;

/// <summary>Why a device is being highlighted on the dashboard before anyone searches for it.</summary>
public enum InsightKind
{
    Fault = 0,
    Disconnected = 1,
    OutOfRange = 2,
    SustainedTrend = 3
}
