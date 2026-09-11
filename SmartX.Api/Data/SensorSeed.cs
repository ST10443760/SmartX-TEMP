using SmartX.Shared.Collections;
using SmartX.Shared.Enums;
using SmartX.Shared.Models;

namespace SmartX.Api.Data;

/// <summary>
/// Heavily seeds the in-memory sensor ledger with mock ESP32-style nodes so
/// the ingestion pipeline and dashboard have realistic data to work against
/// without needing physical hardware, per the assessment's simulation note.
/// </summary>
public static class SensorSeed
{
    public static void Populate(SensorLedger ledger)
    {
        Register(ledger, "24:6F:28:A1:00:01", SensorCategory.Environmental, "Smart-X Root", "Facility A", "Zone 1", "Sub-Zone B", "Node 12");
        Register(ledger, "24:6F:28:A1:00:02", SensorCategory.Environmental, "Smart-X Root", "Facility A", "Zone 1", "Sub-Zone B", "Node 13");
        Register(ledger, "24:6F:28:A1:00:03", SensorCategory.Environmental, "Smart-X Root", "Facility A", "Zone 2", "Sub-Zone A", "Node 21");
        Register(ledger, "B4:E6:2D:C2:11:04", SensorCategory.PowerConsumption, "Smart-X Root", "Facility B", "Utility Room", "Node 44");
        Register(ledger, "B4:E6:2D:C2:11:05", SensorCategory.PowerConsumption, "Smart-X Root", "Facility A", "Zone 1", "Sub-Zone B", "Node 12");
        Register(ledger, "7C:9E:BD:33:22:06", SensorCategory.Actuator, "Smart-X Root", "Facility A", "Zone 1", "Sub-Zone B", "Node 13");
        Register(ledger, "7C:9E:BD:33:22:07", SensorCategory.Actuator, "Smart-X Root", "Facility B", "Utility Room", "Node 44");
        Register(ledger, "24:6F:28:A1:00:08", SensorCategory.Environmental, "Smart-X Root", "Facility A", "Zone 2", "Sub-Zone A", "Node 21");
    }

    private static void Register(SensorLedger ledger, string mac, SensorCategory category, params string[] path)
    {
        ledger.Register(new SensorRecord
        {
            Id = Guid.NewGuid(),
            MacAddress = mac,
            Category = category,
            DeploymentPath = DeploymentPath.Of(path),
            RegisteredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-Random.Shared.Next(5, 4000))
        });
    }
}
