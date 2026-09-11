using SmartX.Shared.Enums;
using SmartX.Shared.Models;
using SmartX.Shared.Validation;

namespace SmartX.Tests;

public class DeploymentTreeValidatorTests
{
    private static DeploymentNode BuildSampleTree()
    {
        // Facility A
        //   Zone 1
        //     Sub-Zone B
        //       Room 4
        //         Node 12
        //   Zone 2
        // Facility C (unrelated branch, no children)
        var node12 = new DeploymentNode { Name = "Node 12", Type = DeploymentNodeType.Node };
        var room4 = new DeploymentNode { Name = "Room 4", Type = DeploymentNodeType.Room, Children = [node12] };
        var subZoneB = new DeploymentNode { Name = "Sub-Zone B", Type = DeploymentNodeType.SubZone, Children = [room4] };
        var zone1 = new DeploymentNode { Name = "Zone 1", Type = DeploymentNodeType.Zone, Children = [subZoneB] };
        var zone2 = new DeploymentNode { Name = "Zone 2", Type = DeploymentNodeType.Zone };
        var facilityA = new DeploymentNode { Name = "Facility A", Type = DeploymentNodeType.Facility, Children = [zone1, zone2] };
        var facilityC = new DeploymentNode { Name = "Facility C", Type = DeploymentNodeType.Facility };

        return new DeploymentNode
        {
            Name = "Root",
            Type = DeploymentNodeType.Facility,
            Children = [facilityA, facilityC]
        };
    }

    [Fact]
    public void IsValidPath_FullDepthChain_ReturnsTrue()
    {
        var root = BuildSampleTree();

        var valid = DeploymentTreeValidator.IsValidPath(root, ["Root", "Facility A", "Zone 1", "Sub-Zone B", "Room 4", "Node 12"]);

        Assert.True(valid);
    }

    [Fact]
    public void IsValidPath_ShallowChain_ReturnsTrue()
    {
        var root = BuildSampleTree();

        var valid = DeploymentTreeValidator.IsValidPath(root, ["Root", "Facility A", "Zone 2"]);

        Assert.True(valid);
    }

    [Fact]
    public void IsValidPath_BrokenChain_ReturnsFalse()
    {
        var root = BuildSampleTree();

        // Sub-Zone B does not sit under Facility C in this tree.
        var valid = DeploymentTreeValidator.IsValidPath(root, ["Root", "Facility C", "Zone 1", "Sub-Zone B"]);

        Assert.False(valid);
    }

    [Fact]
    public void IsValidPath_UnknownLeaf_ReturnsFalse()
    {
        var root = BuildSampleTree();

        var valid = DeploymentTreeValidator.IsValidPath(root, ["Root", "Facility A", "Zone 1", "Sub-Zone Z"]);

        Assert.False(valid);
    }

    [Fact]
    public void IsValidPath_EmptySegments_ReturnsTrue_BaseCase()
    {
        var root = BuildSampleTree();

        var valid = DeploymentTreeValidator.IsValidPath(root, []);

        Assert.True(valid);
    }

    [Fact]
    public void IsValidPath_RootNameMismatch_ReturnsFalse()
    {
        var root = BuildSampleTree();

        var valid = DeploymentTreeValidator.IsValidPath(root, ["Not Root"]);

        Assert.False(valid);
    }

    [Fact]
    public void FindDeepestMatch_PartialPath_ReturnsLastMatchedNode()
    {
        var root = BuildSampleTree();

        var match = DeploymentTreeValidator.FindDeepestMatch(root, ["Root", "Facility A", "Zone 1", "Sub-Zone X"]);

        Assert.NotNull(match);
        Assert.Equal("Zone 1", match!.Name);
    }

    [Fact]
    public void IsValidPath_UsingDeploymentPathOverload_Works()
    {
        var root = BuildSampleTree();
        var path = DeploymentPath.Of("Root", "Facility A", "Zone 1");

        Assert.True(DeploymentTreeValidator.IsValidPath(root, path));
    }
}
