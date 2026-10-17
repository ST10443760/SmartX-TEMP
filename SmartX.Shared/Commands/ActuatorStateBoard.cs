using SmartX.Shared.Dtos;
using SmartX.Shared.Enums;

namespace SmartX.Shared.Commands;

/// <summary>
/// The commanded state of every actuator, keyed by MAC address. Simulated
/// devices read their desired state from here and report it back in their
/// bool valve-state telemetry, like a real device obeying a command.
/// Not thread-safe on its own - see <see cref="OverrideCommandProcessor"/>.
/// </summary>
public sealed class ActuatorStateBoard
{
    private readonly Dictionary<string, (string Name, ActuatorKind Kind, bool IsOn)> _actuators = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string mac, string name, ActuatorKind kind, bool isOn = false)
        => _actuators.TryAdd(mac, (name, kind, isOn));

    public bool TryGet(string mac, out ActuatorKind kind, out bool isOn)
    {
        if (_actuators.TryGetValue(mac, out var entry))
        {
            kind = entry.Kind;
            isOn = entry.IsOn;
            return true;
        }

        kind = ActuatorKind.None;
        isOn = false;
        return false;
    }

    public string NameOf(string mac) => _actuators.TryGetValue(mac, out var entry) ? entry.Name : mac;

    public void Set(string mac, bool isOn)
    {
        if (_actuators.TryGetValue(mac, out var entry))
        {
            _actuators[mac] = entry with { IsOn = isOn };
        }
    }

    public IEnumerable<string> MacsOfKind(ActuatorKind kind)
        => _actuators.Where(a => a.Value.Kind == kind).Select(a => a.Key);

    public List<ActuatorState> Snapshot()
        => _actuators
            .Select(a => new ActuatorState { MacAddress = a.Key, DisplayName = a.Value.Name, Kind = a.Value.Kind, IsOn = a.Value.IsOn })
            .OrderBy(a => a.Kind).ThenBy(a => a.DisplayName)
            .ToList();
}
