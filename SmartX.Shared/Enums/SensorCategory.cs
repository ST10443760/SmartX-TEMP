namespace SmartX.Shared.Enums;

/// <summary>
/// The functional category of a registered Smart-X sensor node.
/// Drives which telemetry unit/value type is expected from the device.
/// </summary>
public enum SensorCategory
{
    Environmental = 0,
    PowerConsumption = 1,
    Actuator = 2
}
