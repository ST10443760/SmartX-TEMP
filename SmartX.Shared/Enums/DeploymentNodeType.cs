namespace SmartX.Shared.Enums;

/// <summary>
/// Level within the Smart-X physical deployment hierarchy, e.g.
/// Facility A -> Zone 1 -> Sub-Zone B -> Node 4.
/// </summary>
public enum DeploymentNodeType
{
    Facility = 0,
    Zone = 1,
    SubZone = 2,
    Room = 3,
    Node = 4
}
