using SmartX.Shared.Collections;
using SmartX.Shared.Enums;
using SmartX.Shared.Models;

namespace SmartX.Api.Data;

/// <summary>
/// Heavily seeds the in-memory sensor ledger with mock ESP32-style nodes so
/// the ingestion pipeline and dashboard have realistic data to work against
/// without needing physical hardware, per the assessment's simulation note.
/// Each node has a friendly name so the Part 2 command stream can be searched
/// by name as well as MAC, and the actuators say what they drive.
/// </summary>
public static class SensorSeed
{
    public static void Populate(SensorLedger ledger)
    {
        Register(ledger, "24:6F:28:A1:00:01", "Moisture Sensor A", SensorCategory.Environmental, ActuatorKind.None, "Smart-X Root", "Facility A", "Zone 1", "Sub-Zone B", "Node 12");
        Register(ledger, "24:6F:28:A1:00:02", "Moisture Sensor B", SensorCategory.Environmental, ActuatorKind.None, "Smart-X Root", "Facility A", "Zone 1", "Sub-Zone B", "Node 13");
        Register(ledger, "24:6F:28:A1:00:03", "Moisture Sensor C", SensorCategory.Environmental, ActuatorKind.None, "Smart-X Root", "Facility A", "Zone 2", "Sub-Zone A", "Node 21");
        Register(ledger, "B4:E6:2D:C2:11:04", "Power Meter A", SensorCategory.PowerConsumption, ActuatorKind.None, "Smart-X Root", "Facility B", "Utility Room", "Node 44");
        Register(ledger, "B4:E6:2D:C2:11:05", "Power Meter B", SensorCategory.PowerConsumption, ActuatorKind.None, "Smart-X Root", "Facility A", "Zone 1", "Sub-Zone B", "Node 12");
        Register(ledger, "7C:9E:BD:33:22:06", "Pump A", SensorCategory.Actuator, ActuatorKind.Pump, "Smart-X Root", "Facility A", "Zone 1", "Sub-Zone B", "Node 13");
        Register(ledger, "7C:9E:BD:33:22:07", "Valve 1", SensorCategory.Actuator, ActuatorKind.Valve, "Smart-X Root", "Facility B", "Utility Room", "Node 44");
        Register(ledger, "24:6F:28:A1:00:08", "Moisture Sensor D", SensorCategory.Environmental, ActuatorKind.None, "Smart-X Root", "Facility A", "Zone 2", "Sub-Zone A", "Node 21");
        Register(ledger, "7C:9E:BD:33:22:09", "Valve 2", SensorCategory.Actuator, ActuatorKind.Valve, "Smart-X Root", "Facility A", "Zone 1", "Sub-Zone B", "Node 12");
        Register(ledger, "7C:9E:BD:33:22:0A", "Relay 1", SensorCategory.Actuator, ActuatorKind.Relay, "Smart-X Root", "Facility B", "Utility Room", "Node 44");
    }

    private static void Register(SensorLedger ledger, string mac, string name, SensorCategory category, ActuatorKind actuatorKind, params string[] path)
    {
        ledger.Register(new SensorRecord
        {
            Id = Guid.NewGuid(),
            MacAddress = mac,
            DisplayName = name,
            Category = category,
            ActuatorKind = actuatorKind,
            DeploymentPath = DeploymentPath.Of(path),
            RegisteredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-Random.Shared.Next(5, 4000))
        });
    }
}
