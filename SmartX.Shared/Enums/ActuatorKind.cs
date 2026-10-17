namespace SmartX.Shared.Enums;

/// <summary>
/// What a device in the <see cref="SensorCategory.Actuator"/> category
/// physically drives. Manual override commands target actuators by kind -
/// "close all valves" only touches valves, "trigger pump" only pumps.
/// </summary>
public enum ActuatorKind
{
    None = 0,
    Pump = 1,
    Valve = 2,
    Relay = 3
}
