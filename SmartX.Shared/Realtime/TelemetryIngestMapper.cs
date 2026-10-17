using SmartX.Shared.Dtos;
using SmartX.Shared.Enums;
using SmartX.Shared.Models;

namespace SmartX.Shared.Realtime;

/// <summary>
/// Turns a raw ingest request into a <see cref="QueuedReading"/>. Each value
/// is first wrapped in the Part 1 generic <see cref="TelemetryPacket{T}"/>
/// with its native type (float, int or bool - no boxing), then normalised
/// to a double so both ingestion queues can hold a single element type.
/// </summary>
public static class TelemetryIngestMapper
{
    public static bool TryMap(TelemetryIngestRequest request, DateTime receivedUtc, out QueuedReading? reading, out string? error)
    {
        reading = null;
        error = null;

        var valuesSupplied = (request.Moisture.HasValue ? 1 : 0) + (request.Watts.HasValue ? 1 : 0) + (request.ValveOpen.HasValue ? 1 : 0);
        if (valuesSupplied != 1)
        {
            error = "Send exactly one of moisture (float), watts (int) or valveOpen (bool).";
            return false;
        }

        var timestamp = DateTime.SpecifyKind(request.TimestampUtc ?? receivedUtc, DateTimeKind.Utc);

        if (request.Moisture is { } moisture)
        {
            var packet = Packet(request.MacAddress, moisture, "%", timestamp);
            reading = ToQueued(packet.SensorId, ReadingKind.Moisture, packet.Value, packet.Unit, timestamp);
        }
        else if (request.Watts is { } watts)
        {
            var packet = Packet(request.MacAddress, watts, "W", timestamp);
            reading = ToQueued(packet.SensorId, ReadingKind.Wattage, packet.Value, packet.Unit, timestamp);
        }
        else
        {
            var packet = Packet(request.MacAddress, request.ValveOpen!.Value, "open", timestamp);
            reading = ToQueued(packet.SensorId, ReadingKind.ValveState, packet.Value ? 1 : 0, packet.Unit, timestamp);
        }

        return true;
    }

    private static TelemetryPacket<T> Packet<T>(string mac, T value, string unit, DateTime timestamp) where T : struct
        => new() { SensorId = mac.Trim(), Value = value, Unit = unit, TimestampUtc = timestamp };

    private static QueuedReading ToQueued(string mac, ReadingKind kind, double value, string unit, DateTime timestamp)
        => new(mac, kind, Math.Round(value, 2), unit, timestamp, SeverityClassifier.Classify(kind, value));
}
