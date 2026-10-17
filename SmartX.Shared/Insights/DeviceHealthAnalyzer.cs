using System.Globalization;
using SmartX.Shared.Dtos;
using SmartX.Shared.Enums;
using SmartX.Shared.Realtime;

namespace SmartX.Shared.Insights;

/// <summary>
/// The proactive half of the Automated Action Engine: looks at one device's
/// live state and recent sorted history and decides whether it deserves to
/// be highlighted before anyone searches for it. Checks run in order of
/// urgency and the first hit wins:
/// <list type="number">
///   <item>Fault - the device has a critical-reading entry in the active alert set.</item>
///   <item>Disconnected - the device is in the disconnected set.</item>
///   <item>Out of range - the latest reading is past a warning threshold.</item>
///   <item>Sustained trend - the last <see cref="TrendWindow"/> readings all move the same way and the total change is large (an early warning before a threshold is crossed).</item>
/// </list>
/// </summary>
public static class DeviceHealthAnalyzer
{
    public const int TrendWindow = 6;
    public const double MoistureTrendDrop = 8;     // percentage points
    public const double WattsTrendRiseRatio = 0.25; // +25%

    public static DeviceInsight? Analyze(
        string mac,
        string name,
        SensorCategory category,
        IReadOnlyList<double> recentValuesOldestFirst,
        bool hasCriticalAlert,
        bool isDisconnected)
    {
        if (hasCriticalAlert)
        {
            var last = recentValuesOldestFirst.Count > 0 ? Format(recentValuesOldestFirst[^1], category) : "a critical value";
            return Insight(mac, name, InsightKind.Fault, AlertSeverity.Critical, $"{name} is in a fault state - last reading {last}.");
        }

        if (isDisconnected)
        {
            return Insight(mac, name, InsightKind.Disconnected, AlertSeverity.Critical, $"{name} has stopped reporting and is marked disconnected.");
        }

        if (recentValuesOldestFirst.Count == 0 || category == SensorCategory.Actuator)
        {
            return null;
        }

        var latest = recentValuesOldestFirst[^1];
        var kind = category == SensorCategory.PowerConsumption ? ReadingKind.Wattage : ReadingKind.Moisture;
        if (SeverityClassifier.Classify(kind, latest) != AlertSeverity.Normal)
        {
            var bound = kind == ReadingKind.Wattage
                ? $"above {SeverityClassifier.WattsWarningAbove.ToString(CultureInfo.InvariantCulture)}W"
                : latest < SeverityClassifier.MoistureWarningBelow
                    ? $"below {SeverityClassifier.MoistureWarningBelow.ToString(CultureInfo.InvariantCulture)}%"
                    : $"above {SeverityClassifier.MoistureWarningAbove.ToString(CultureInfo.InvariantCulture)}%";
            return Insight(mac, name, InsightKind.OutOfRange, AlertSeverity.Warning, $"{name} is out of range at {Format(latest, category)} ({bound}).");
        }

        return DetectTrend(mac, name, category, recentValuesOldestFirst);
    }

    private static DeviceInsight? DetectTrend(string mac, string name, SensorCategory category, IReadOnlyList<double> values)
    {
        if (values.Count < TrendWindow)
        {
            return null;
        }

        var window = values.Skip(values.Count - TrendWindow).ToList();
        var falling = window.Zip(window.Skip(1)).All(p => p.Second < p.First);
        var rising = window.Zip(window.Skip(1)).All(p => p.Second > p.First);
        var change = window[^1] - window[0];

        if (category == SensorCategory.Environmental && falling && -change >= MoistureTrendDrop)
        {
            return Insight(mac, name, InsightKind.SustainedTrend, AlertSeverity.Warning,
                string.Create(CultureInfo.InvariantCulture, $"{name} has fallen {-change:0.#} points over its last {TrendWindow} readings - likely to drop below {SeverityClassifier.MoistureWarningBelow}% soon."));
        }

        if (category == SensorCategory.PowerConsumption && rising && window[0] > 0 && change / window[0] >= WattsTrendRiseRatio)
        {
            return Insight(mac, name, InsightKind.SustainedTrend, AlertSeverity.Warning,
                string.Create(CultureInfo.InvariantCulture, $"{name} load has climbed {change / window[0]:P0} over its last {TrendWindow} readings."));
        }

        return null;
    }

    private static DeviceInsight Insight(string mac, string name, InsightKind kind, AlertSeverity severity, string reason)
        => new() { MacAddress = mac, DeviceName = name, Kind = kind, Severity = severity, Reason = reason };

    private static string Format(double value, SensorCategory category) => category switch
    {
        SensorCategory.PowerConsumption => string.Create(CultureInfo.InvariantCulture, $"{value:0}W"),
        SensorCategory.Environmental => string.Create(CultureInfo.InvariantCulture, $"{value:0.#}%"),
        _ => value >= 0.5 ? "on" : "off"
    };
}
