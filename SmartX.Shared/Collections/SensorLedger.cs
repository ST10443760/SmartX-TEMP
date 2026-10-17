using System.Collections;
using SmartX.Shared.Enums;
using SmartX.Shared.Models;

namespace SmartX.Shared.Collections;

/// <summary>
/// Custom collection for the gateway's registered-sensor pipeline. Backed by
/// a generic <see cref="List{T}"/> internally (per the ingestion pipeline
/// requirement - raw registrations arrive one at a time and are appended,
/// never as a rectangular block), but wrapped in a domain-specific type so
/// callers work in terms of "register a sensor" / "find by MAC" /
/// "sensors in this category" rather than generic list indices.
///
/// Since Part 2 the ledger is touched concurrently (the simulator registers
/// devices while the background worker and endpoints read), so every member
/// takes a private lock and enumeration walks a snapshot copy.
/// </summary>
public sealed class SensorLedger : IReadOnlyCollection<SensorRecord>
{
    private readonly List<SensorRecord> _sensors = [];
    private readonly Lock _gate = new();

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _sensors.Count;
            }
        }
    }

    public SensorRecord Register(SensorRecord sensor)
    {
        ArgumentNullException.ThrowIfNull(sensor);

        lock (_gate)
        {
            if (_sensors.Any(s => string.Equals(s.MacAddress, sensor.MacAddress, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"A sensor with MAC address '{sensor.MacAddress}' is already registered.");
            }

            _sensors.Add(sensor);
            return sensor;
        }
    }

    public SensorRecord? FindByMacAddress(string macAddress)
    {
        lock (_gate)
        {
            return _sensors.FirstOrDefault(s => string.Equals(s.MacAddress, macAddress, StringComparison.OrdinalIgnoreCase));
        }
    }

    public IEnumerable<SensorRecord> InCategory(SensorCategory category)
        => Snapshot().Where(s => s.Category == category);

    public bool TryAttachFile(string macAddress, SensorAttachment attachment)
    {
        lock (_gate)
        {
            var sensor = _sensors.FirstOrDefault(s => string.Equals(s.MacAddress, macAddress, StringComparison.OrdinalIgnoreCase));
            if (sensor is null)
            {
                return false;
            }

            sensor.Attachments.Add(attachment);
            return true;
        }
    }

    public IEnumerator<SensorRecord> GetEnumerator() => Snapshot().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private List<SensorRecord> Snapshot()
    {
        lock (_gate)
        {
            return [.. _sensors];
        }
    }
}
