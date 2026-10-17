using SmartX.Shared.Enums;

namespace SmartX.Shared.Realtime;

/// <summary>
/// Every live registered device, in a <see cref="Dictionary{TKey,TValue}"/>
/// keyed by MAC address (case-insensitive), so each incoming packet finds its
/// device in O(1) instead of scanning a list. Unknown MACs simply miss - the
/// caller rejects or flags them; nothing here throws for an unknown key.
///
/// Thread safety: the ingest endpoint, the background worker and the
/// snapshot endpoint all touch it at once, so every member takes a private
/// lock. Entries are immutable records and are replaced, never mutated.
/// </summary>
public sealed class DeviceDirectory
{
    private readonly Dictionary<string, LiveDevice> _devices = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _gate = new();

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _devices.Count;
            }
        }
    }

    /// <summary>Adds a device; returns false (and leaves the existing entry) if the MAC is already known.</summary>
    public bool Register(string mac, string displayName, SensorCategory category, ActuatorKind actuatorKind = ActuatorKind.None)
    {
        lock (_gate)
        {
            return _devices.TryAdd(mac, new LiveDevice(mac, displayName, category, actuatorKind));
        }
    }

    public bool Contains(string mac)
    {
        lock (_gate)
        {
            return _devices.ContainsKey(mac);
        }
    }

    public bool TryGet(string mac, out LiveDevice? device)
    {
        lock (_gate)
        {
            return _devices.TryGetValue(mac, out device);
        }
    }

    /// <summary>Stores the latest reading against the device. Returns false for an unknown MAC.</summary>
    public bool RecordReading(string mac, double value, string unit, DateTime timestampUtc, AlertSeverity severity)
    {
        lock (_gate)
        {
            if (!_devices.TryGetValue(mac, out var device))
            {
                return false;
            }

            _devices[mac] = device with { LastValue = value, Unit = unit, LastSeenUtc = timestampUtc, Severity = severity };
            return true;
        }
    }

    public List<LiveDevice> Snapshot()
    {
        lock (_gate)
        {
            return [.. _devices.Values];
        }
    }
}
