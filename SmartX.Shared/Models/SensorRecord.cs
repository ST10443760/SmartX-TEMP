using SmartX.Shared.Enums;

namespace SmartX.Shared.Models;

/// <summary>
/// A registered Smart-X sensor node as stored by the gateway: its identity,
/// where it sits in the deployment tree, what kind of device it is, and any
/// files (configs, photos, logs) attached to it.
/// </summary>
public sealed class SensorRecord
{
    public required Guid Id { get; init; }

    public required string MacAddress { get; init; }

    /// <summary>Human-friendly name operators search by, e.g. "Moisture Sensor B" or "Pump A".</summary>
    public string DisplayName { get; init; } = "";

    public required DeploymentPath DeploymentPath { get; init; }

    public required SensorCategory Category { get; init; }

    /// <summary>Only meaningful when <see cref="Category"/> is Actuator.</summary>
    public ActuatorKind ActuatorKind { get; init; } = ActuatorKind.None;

    public DateTimeOffset RegisteredAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public List<SensorAttachment> Attachments { get; init; } = [];
}
