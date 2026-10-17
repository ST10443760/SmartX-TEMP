using SmartX.Shared.Commands;
using SmartX.Shared.Enums;
using SmartX.Shared.Models;
using SmartX.Shared.Realtime;

namespace SmartX.Api.Services;

/// <summary>
/// Single place a newly registered sensor is plugged into the Part 2 live
/// structures, so the Part 1 registration endpoint and the startup seed
/// both go through the same path.
/// </summary>
public sealed class LiveDeviceRegistrar(DeviceDirectory directory, OverrideCommandProcessor commands)
{
    public void Track(SensorRecord sensor)
    {
        directory.Register(sensor.MacAddress, sensor.DisplayName, sensor.Category, sensor.ActuatorKind);

        if (sensor.Category == SensorCategory.Actuator)
        {
            commands.RegisterActuator(sensor.MacAddress, sensor.DisplayName, sensor.ActuatorKind);
        }
    }
}
