using SmartX.Shared.Dtos;
using SmartX.Shared.Enums;
using SmartX.Shared.Realtime;
using SmartX.Shared.Trees;

namespace SmartX.Api.Data;

/// <summary>
/// Gives every seeded device four hours of simulated history in its AVL tree
/// (one reading every 5 seconds, 2,880 per device), so a range query such as
/// 14:00 to 16:00 returns a sensible subset the first time the page opens,
/// and the nodes-visited count is clearly smaller than the total.
///
/// Only the Part 3 trees are seeded. The Part 2 ReadingHistoryStore and
/// timeline still start empty and fill from live telemetry, as before.
/// A fixed random seed per device keeps the curves the same on every run.
/// </summary>
public static class HistorySeed
{
    public static readonly TimeSpan Span = TimeSpan.FromHours(4);
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    public static int Populate(DeviceHistoryTrees<LiveReading> trees, IEnumerable<LiveDevice> devices, DateTime nowUtc)
    {
        var start = nowUtc - Span;
        var steps = (int)(Span / Interval);
        var total = 0;

        foreach (var device in devices)
        {
            var rng = new Random(device.MacAddress.GetHashCode(StringComparison.OrdinalIgnoreCase) & 0x7fffffff);
            var phase = rng.NextDouble() * Math.PI * 2;

            for (var i = 0; i < steps; i++)
            {
                var timestamp = start + Interval * i;
                var hours = (timestamp - start).TotalHours;
                var (kind, value, unit) = device.Category switch
                {
                    // Soil slowly dries and is watered back up roughly every 80 minutes.
                    SensorCategory.Environmental => (ReadingKind.Moisture, Math.Clamp(42 + 18 * Math.Sin(hours * 4.7 + phase) + rng.NextDouble() * 4 - 2, 0, 100), "%"),
                    // Base load with peaks while pumps run, plus the odd spike.
                    SensorCategory.PowerConsumption => (ReadingKind.Wattage, 650 + 300 * Math.Sin(hours * 3.1 + phase) + rng.Next(-60, 60) + (rng.Next(400) == 0 ? 1900 : 0), "W"),
                    // Actuators switch roughly every 20 minutes.
                    _ => (ReadingKind.ValveState, Math.Sin(hours * 9.4 + phase) > 0.3 ? 1d : 0d, "open")
                };

                trees.Add(device.MacAddress, timestamp, new LiveReading
                {
                    MacAddress = device.MacAddress,
                    DeviceName = device.DisplayName,
                    Category = device.Category,
                    Kind = kind,
                    Value = Math.Round(value, 2),
                    Unit = unit,
                    TimestampUtc = timestamp,
                    Severity = SeverityClassifier.Classify(kind, value),
                    Lane = "Seed"
                });
                total++;
            }
        }

        return total;
    }
}
