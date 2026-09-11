namespace SmartX.Shared.Enums;

/// <summary>
/// Quality flag attached to an ingested telemetry packet, used by the
/// gateway dashboard to decide whether a reading needs operator attention.
/// </summary>
public enum TelemetryQuality
{
    Normal = 0,
    Warning = 1,
    Critical = 2,
    Disconnected = 3
}
