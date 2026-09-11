using SmartX.Shared.Enums;

namespace SmartX.Shared.Models;

/// <summary>
/// Uniform envelope for a single telemetry reading coming off any Smart-X
/// sensor, whatever its native CLR type is - a float for soil moisture, an
/// int for power wattage, a bool for a valve/relay state.
///
/// Because <typeparamref name="T"/> is a generic type parameter (constrained
/// to <see langword="struct"/>) rather than <see cref="object"/>, the CLR
/// generates a specialised, value-type-backed instantiation of this class for
/// each T the gateway actually uses (TelemetryPacket&lt;float&gt;,
/// TelemetryPacket&lt;int&gt;, TelemetryPacket&lt;bool&gt;, ...). The payload is
/// never boxed to store or read it, which matters when a gateway is parsing
/// thousands of packets a second on constrained hardware.
/// </summary>
/// <typeparam name="T">The native value type of the sensor reading.</typeparam>
public sealed class TelemetryPacket<T> where T : struct
{
    public required string SensorId { get; init; }

    public required T Value { get; init; }

    /// <summary>Unit of measurement, e.g. "%", "W", "on/off".</summary>
    public required string Unit { get; init; }

    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    public TelemetryQuality Quality { get; init; } = TelemetryQuality.Normal;

    public override string ToString() => $"[{TimestampUtc:O}] {SensorId} = {Value}{Unit} ({Quality})";
}
