namespace SmartX.Shared.Dtos;

/// <summary>
/// What a (simulated) device POSTs to the gateway. Exactly one value field is
/// set, and each uses the device's native type: a float for soil moisture,
/// an int for power wattage, a bool for a valve/relay/pump state.
/// </summary>
public sealed class TelemetryIngestRequest
{
    public required string MacAddress { get; init; }

    public float? Moisture { get; init; }

    public int? Watts { get; init; }

    public bool? ValveOpen { get; init; }

    /// <summary>Device-side timestamp; the gateway uses its receive time when omitted.</summary>
    public DateTime? TimestampUtc { get; init; }
}
