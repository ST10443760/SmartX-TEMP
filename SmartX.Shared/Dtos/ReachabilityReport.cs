namespace SmartX.Shared.Dtos;

/// <summary>What BFS from the gateway found over live links.</summary>
public sealed class ReachabilityReport
{
    /// <summary>Reachable devices in BFS order (nearest first). The gateway itself is not listed.</summary>
    public List<ReachableNodeInfo> Reachable { get; init; } = [];

    /// <summary>Down nodes and nodes cut off by down nodes.</summary>
    public List<UnreachableNodeInfo> Unreachable { get; init; } = [];

    /// <summary>For each down node, the live neighbours its traffic hops through instead.</summary>
    public List<RerouteInfo> Reroutes { get; init; } = [];
}

public sealed record ReachableNodeInfo(string Id, string Name, int Hops);

/// <summary><see cref="Reason"/> is "Down" or "CutOff".</summary>
public sealed record UnreachableNodeInfo(string Id, string Name, string Reason);

public sealed record RerouteInfo(string DownId, string DownName, string ViaId, string ViaName, List<string> PathNames);
