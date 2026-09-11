namespace SmartX.Shared.Models;

/// <summary>
/// A wattage reading from a smart meter or plug. Overloads + and - so the
/// dashboard can aggregate or diff meters directly in code, e.g. combining
/// two sub-meters into the load for a shared circuit:
/// <c>var circuitLoad = meter1 + meter2;</c>
/// Comparison operators let the alerting engine ask "is this meter drawing
/// more than that one?" without unpacking the struct first.
/// </summary>
public readonly struct PowerReading : IEquatable<PowerReading>, IComparable<PowerReading>
{
    public string SensorId { get; }
    public double Watts { get; }
    public DateTimeOffset TimestampUtc { get; }

    public PowerReading(string sensorId, double watts, DateTimeOffset? timestampUtc = null)
    {
        SensorId = sensorId;
        Watts = watts;
        TimestampUtc = timestampUtc ?? DateTimeOffset.UtcNow;
    }

    /// <summary>Combined load of two meters, as if they fed the same circuit.</summary>
    public static PowerReading operator +(PowerReading a, PowerReading b)
        => new($"{a.SensorId}+{b.SensorId}", a.Watts + b.Watts, LaterOf(a, b));

    /// <summary>Load delta between two meters (e.g. before/after a device switched on).</summary>
    public static PowerReading operator -(PowerReading a, PowerReading b)
        => new($"{a.SensorId}-{b.SensorId}", a.Watts - b.Watts, LaterOf(a, b));

    public static bool operator >(PowerReading a, PowerReading b) => a.Watts > b.Watts;
    public static bool operator <(PowerReading a, PowerReading b) => a.Watts < b.Watts;
    public static bool operator >=(PowerReading a, PowerReading b) => a.Watts >= b.Watts;
    public static bool operator <=(PowerReading a, PowerReading b) => a.Watts <= b.Watts;

    public static bool operator ==(PowerReading a, PowerReading b) => a.Equals(b);
    public static bool operator !=(PowerReading a, PowerReading b) => !a.Equals(b);

    public bool Equals(PowerReading other)
        => SensorId == other.SensorId && Watts.Equals(other.Watts);

    public override bool Equals(object? obj) => obj is PowerReading other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(SensorId, Watts);

    public int CompareTo(PowerReading other) => Watts.CompareTo(other.Watts);

    public override string ToString() => $"{SensorId}: {Watts:0.##}W";

    private static DateTimeOffset LaterOf(PowerReading a, PowerReading b)
        => a.TimestampUtc > b.TimestampUtc ? a.TimestampUtc : b.TimestampUtc;
}
