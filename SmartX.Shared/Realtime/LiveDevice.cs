using SmartX.Shared.Enums;

namespace SmartX.Shared.Realtime;

/// <summary>
/// Live view of one registered device inside the <see cref="DeviceDirectory"/>.
/// Immutable: an update replaces the whole entry under the directory's lock,
/// so a reader never sees a half-updated device.
/// </summary>
public sealed record LiveDevice(
    string MacAddress,
    string DisplayName,
    SensorCategory Category,
    ActuatorKind ActuatorKind,
    double? LastValue = null,
    string Unit = "",
    DateTime? LastSeenUtc = null,
    AlertSeverity Severity = AlertSeverity.Normal);
