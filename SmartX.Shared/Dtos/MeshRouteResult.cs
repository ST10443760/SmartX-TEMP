using SmartX.Shared.Enums;

namespace SmartX.Shared.Dtos;

/// <summary>A device's power-efficient route to the gateway (its path in the MST), hop by hop.</summary>
public sealed class MeshRouteResult
{
    public required string MacAddress { get; init; }

    public required string Name { get; init; }

    public MeshNodeState State { get; init; }

    public bool Reachable { get; init; }

    /// <summary>Hops in order, from the device to the gateway.</summary>
    public List<RouteHopInfo> Hops { get; init; } = [];

    public double TotalCost { get; init; }

    /// <summary>Plain-language summary, e.g. why there is no route.</summary>
    public string Message { get; init; } = "";
}

public sealed record RouteHopInfo(string FromId, string FromName, string ToId, string ToName, double Cost);
