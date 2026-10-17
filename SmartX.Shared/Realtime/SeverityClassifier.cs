using SmartX.Shared.Enums;

namespace SmartX.Shared.Realtime;

/// <summary>
/// Decides how urgent a raw reading is, which decides which ingestion queue
/// it goes into. Thresholds are deliberately simple and public so they can
/// be quoted in the UI and the README.
/// </summary>
public static class SeverityClassifier
{
    public const double MoistureCriticalBelow = 12;
    public const double MoistureWarningBelow = 20;
    public const double MoistureWarningAbove = 90;

    public const double WattsWarningAbove = 2200;
    public const double WattsCriticalAbove = 3000;

    public static AlertSeverity Classify(ReadingKind kind, double value) => kind switch
    {
        ReadingKind.Moisture when value < MoistureCriticalBelow => AlertSeverity.Critical,
        ReadingKind.Moisture when value < MoistureWarningBelow || value > MoistureWarningAbove => AlertSeverity.Warning,
        ReadingKind.Wattage when value > WattsCriticalAbove => AlertSeverity.Critical,
        ReadingKind.Wattage when value > WattsWarningAbove => AlertSeverity.Warning,
        _ => AlertSeverity.Normal
    };
}
