using SmartX.Shared.Enums;
using SmartX.Shared.Models;

namespace SmartX.Api.Data;

/// <summary>
/// Builds the mock Smart-X site hierarchy used by the recursive deployment
/// validator. Represents two facilities of differing depth so the sample
/// data actually exercises variable-depth recursion, not just a fixed
/// three-level chain.
/// </summary>
public static class DeploymentTreeSeed
{
    public static DeploymentNode BuildDefaultSite()
    {
        var hydroSubZoneB = new DeploymentNode
        {
            Name = "Sub-Zone B",
            Type = DeploymentNodeType.SubZone,
            Children =
            [
                new DeploymentNode { Name = "Node 12", Type = DeploymentNodeType.Node },
                new DeploymentNode { Name = "Node 13", Type = DeploymentNodeType.Node }
            ]
        };

        var hydroZone1 = new DeploymentNode
        {
            Name = "Zone 1",
            Type = DeploymentNodeType.Zone,
            Children = [hydroSubZoneB]
        };

        var hydroZone2 = new DeploymentNode
        {
            Name = "Zone 2",
            Type = DeploymentNodeType.Zone,
            Children =
            [
                new DeploymentNode
                {
                    Name = "Sub-Zone A",
                    Type = DeploymentNodeType.SubZone,
                    Children = [new DeploymentNode { Name = "Node 21", Type = DeploymentNodeType.Node }]
                }
            ]
        };

        var facilityA = new DeploymentNode
        {
            Name = "Facility A",
            Type = DeploymentNodeType.Facility,
            Children = [hydroZone1, hydroZone2]
        };

        var facilityBRoomLevel = new DeploymentNode
        {
            Name = "Utility Room",
            Type = DeploymentNodeType.Room,
            Children = [new DeploymentNode { Name = "Node 44", Type = DeploymentNodeType.Node }]
        };

        var facilityB = new DeploymentNode
        {
            // Shallower branch on purpose - proves the validator handles trees
            // of differing depth, not just a fixed Facility->Zone->SubZone chain.
            Name = "Facility B",
            Type = DeploymentNodeType.Facility,
            Children = [facilityBRoomLevel]
        };

        return new DeploymentNode
        {
            Name = "Smart-X Root",
            Type = DeploymentNodeType.Facility,
            Children = [facilityA, facilityB]
        };
    }
}
