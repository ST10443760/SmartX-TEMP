namespace SmartX.Shared.Models;

/// <summary>
/// The path a single sensor's registration claims within the deployment
/// tree, ordered from the root down (e.g. ["Facility A", "Zone 1", "Sub-Zone B"]).
/// Kept as an ordered list rather than a fixed Facility/Zone/SubZone record so the
/// hierarchy can go arbitrarily deep without changing the model.
/// </summary>
public sealed class DeploymentPath
{
    public required IReadOnlyList<string> Segments { get; init; }

    public override string ToString() => string.Join(" -> ", Segments);

    public static DeploymentPath Of(params string[] segments) => new() { Segments = segments };
}
