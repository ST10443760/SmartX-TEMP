using SmartX.Shared.Commands;
using SmartX.Shared.Enums;
using SmartX.Shared.Models;

namespace SmartX.Api.Services;

/// <summary>
/// Single place a newly registered sensor is plugged into the Part 2 live
/// structures, so the Part 1 registration endpoint and the startup seed
/// both go through the same path.
/// </summary>
public sealed class LiveDeviceRegistrar(OverrideCommandProcessor commands)
{
    public void Track(SensorRecord sensor)
    {
        if (sensor.Category == SensorCategory.Actuator)
        {
            commands.RegisterActuator(sensor.MacAddress, sensor.DisplayName, sensor.ActuatorKind);
        }
    }
}
