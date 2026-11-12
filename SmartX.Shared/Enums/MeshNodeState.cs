namespace SmartX.Shared.Enums;

/// <summary>Where a node stands in the mesh right now, as decided by BFS from the gateway.</summary>
public enum MeshNodeState
{
    /// <summary>The central gateway every route ends at.</summary>
    Gateway = 0,

    /// <summary>Up and able to reach the gateway over live links.</summary>
    Online = 1,

    /// <summary>Disconnected: the node is in the Part 2 active alert set.</summary>
    Down = 2,

    /// <summary>Up, but every path to the gateway runs through a down node.</summary>
    CutOff = 3
}
