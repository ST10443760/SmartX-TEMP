using SmartX.Shared.Enums;

namespace SmartX.Shared.Models;

/// <summary>
/// A single level in the physical deployment tree that Smart-X sensors are
/// registered against (Facility -> Zone -> Sub-Zone -> Room -> Node).
/// The gateway keeps one of these trees per site; sensor registrations are
/// validated by recursively walking it (see DeploymentTreeValidator).
/// </summary>
public sealed class DeploymentNode
{
    public required string Name { get; init; }

    public required DeploymentNodeType Type { get; init; }

    public List<DeploymentNode> Children { get; init; } = [];

    public DeploymentNode AddChild(DeploymentNode child)
    {
        Children.Add(child);
        return this;
    }
}
