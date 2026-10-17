using SmartX.Shared.Enums;

namespace SmartX.Shared.Realtime;

/// <summary>
/// A telemetry packet that has been accepted by the ingest endpoint and is
/// waiting in one of the two ingestion queues for the background worker.
/// Whatever the native type on the wire (float/int/bool) it is normalised to
/// a double here so both queues can hold one element type.
/// </summary>
public sealed record QueuedReading(
    string MacAddress,
    ReadingKind Kind,
    double Value,
    string Unit,
    DateTime TimestampUtc,
    AlertSeverity Severity);
