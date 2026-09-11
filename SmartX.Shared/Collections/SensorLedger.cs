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
/// </summary>
public sealed class SensorLedger : IReadOnlyCollection<SensorRecord>
{
    private readonly List<SensorRecord> _sensors = [];

    public int Count => _sensors.Count;

    public SensorRecord Register(SensorRecord sensor)
    {
        ArgumentNullException.ThrowIfNull(sensor);

        if (_sensors.Any(s => string.Equals(s.MacAddress, sensor.MacAddress, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"A sensor with MAC address '{sensor.MacAddress}' is already registered.");
        }

        _sensors.Add(sensor);
        return sensor;
    }

    public SensorRecord? FindByMacAddress(string macAddress)
        => _sensors.FirstOrDefault(s => string.Equals(s.MacAddress, macAddress, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<SensorRecord> InCategory(SensorCategory category)
        => _sensors.Where(s => s.Category == category);

    public bool TryAttachFile(string macAddress, SensorAttachment attachment)
    {
        var sensor = FindByMacAddress(macAddress);
        if (sensor is null)
        {
            return false;
        }

        sensor.Attachments.Add(attachment);
        return true;
    }

    public IEnumerator<SensorRecord> GetEnumerator() => _sensors.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
