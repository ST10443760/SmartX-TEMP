using SmartX.Shared.Dtos;
using SmartX.Shared.Enums;

namespace SmartX.Simulator;

/// <summary>
/// One fake ESP32 node. Each kind produces its native value type, like real
/// hardware would: a float for soil moisture, an int for wattage, and a bool
/// for an actuator's valve/relay/pump state.
/// </summary>
public sealed class SimulatedDevice(string mac, string name, SensorCategory category)
{
    private readonly Random _rng = new(HashCode.Combine(mac));
    private float _moisture = 35 + Random.Shared.Next(0, 35);
    private int _watts = 300 + Random.Shared.Next(0, 500);

    public string Mac { get; } = mac;

    public string Name { get; } = name;

    public SensorCategory Category { get; } = category;

    /// <summary>A disconnected device simply stops sending - exactly what the gateway's detector watches for.</summary>
    public bool IsDisconnected { get; set; }

    /// <summary>
    /// Builds this round's packet. Soil slowly dries out (so moisture sensors
    /// eventually cross 20% and the insights engine reacts) and recovers while
    /// any pump is running; power meters wander around a normal load.
    /// </summary>
    public TelemetryIngestRequest NextReading(bool anyPumpOn, IReadOnlyDictionary<string, bool> actuatorStates, bool spike)
    {
        switch (Category)
        {
            case SensorCategory.Environmental:
                if (spike)
                {
                    return new TelemetryIngestRequest { MacAddress = Mac, Moisture = 6.5f };
                }

                _moisture += anyPumpOn ? 2.5f + (float)_rng.NextDouble() : -(0.4f + (float)_rng.NextDouble() * 0.8f);
                if (_moisture < 15)
                {
                    _moisture = 50 + _rng.Next(0, 10); // someone watered it by hand
                }

                _moisture = Math.Clamp(_moisture, 0, 85);
                return new TelemetryIngestRequest { MacAddress = Mac, Moisture = MathF.Round(_moisture, 1) };

            case SensorCategory.PowerConsumption:
                if (spike)
                {
                    return new TelemetryIngestRequest { MacAddress = Mac, Watts = 4300 + _rng.Next(0, 400) };
                }

                _watts = Math.Clamp(_watts + _rng.Next(-60, 61), 150, 1400);
                return new TelemetryIngestRequest { MacAddress = Mac, Watts = _watts };

            default:
                // Actuators report the state the gateway commanded, like a real
                // device acknowledging an override.
                return new TelemetryIngestRequest { MacAddress = Mac, ValveOpen = actuatorStates.GetValueOrDefault(Mac) };
        }
    }

    public override string ToString() => $"{Name} ({Mac})";
}
