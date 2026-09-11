namespace SmartX.Shared.Models;

/// <summary>
/// A soil-moisture percentage reading from a hydroponics node. Overloads -
/// so the anomaly engine can compute how far a reading has drifted between
/// two polls in one expression: <c>var drop = previous - current;</c>
/// Comparison operators drive the "moisture dropped below threshold, trigger
/// the pump" rule directly against two MoistureReading values.
/// </summary>
public readonly struct MoistureReading : IEquatable<MoistureReading>, IComparable<MoistureReading>
{
    public string SensorId { get; }
    public double Percent { get; }
    public DateTimeOffset TimestampUtc { get; }

    public MoistureReading(string sensorId, double percent, DateTimeOffset? timestampUtc = null)
    {
        SensorId = sensorId;
        Percent = percent;
        TimestampUtc = timestampUtc ?? DateTimeOffset.UtcNow;
    }

    /// <summary>Signed drift between two readings from the same node over time.</summary>
    public static double operator -(MoistureReading later, MoistureReading earlier)
        => later.Percent - earlier.Percent;

    public static bool operator >(MoistureReading a, MoistureReading b) => a.Percent > b.Percent;
    public static bool operator <(MoistureReading a, MoistureReading b) => a.Percent < b.Percent;
    public static bool operator >=(MoistureReading a, MoistureReading b) => a.Percent >= b.Percent;
    public static bool operator <=(MoistureReading a, MoistureReading b) => a.Percent <= b.Percent;

    public static bool operator ==(MoistureReading a, MoistureReading b) => a.Equals(b);
    public static bool operator !=(MoistureReading a, MoistureReading b) => !a.Equals(b);

    public bool Equals(MoistureReading other)
        => SensorId == other.SensorId && Percent.Equals(other.Percent);

    public override bool Equals(object? obj) => obj is MoistureReading other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(SensorId, Percent);

    public int CompareTo(MoistureReading other) => Percent.CompareTo(other.Percent);

    public override string ToString() => $"{SensorId}: {Percent:0.#}%";
}
