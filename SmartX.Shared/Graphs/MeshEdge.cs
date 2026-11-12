namespace SmartX.Shared.Graphs;

/// <summary>
/// One wireless link seen from <see cref="From"/>'s side. The graph is
/// undirected, so every link is stored twice in the adjacency list, once in
/// each direction, with the same weight.
///
/// <see cref="Weight"/> is the power cost of transmitting one packet across
/// the link (see MeshTopologySeed in the API for how it is derived from
/// distance). Lower is cheaper.
/// </summary>
public readonly record struct MeshEdge(string From, string To, double Weight);
