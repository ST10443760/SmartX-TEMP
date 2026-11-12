using SmartX.Shared.Graphs;

namespace SmartX.Api.Data;

/// <summary>
/// THE mesh topology definition: where every node sits on the site plan and
/// which pairs of nodes can hear each other over the radio. Change the mesh
/// here and nowhere else.
///
/// Positions are in metres on a 100 m x 60 m site, with the gateway in the
/// middle. Only nodes close to the gateway link to it directly; the rest
/// have to hop through neighbours. Every seeded node has at least two links,
/// so a single failure never strands a node and there is always more than one
/// possible path. Two failures can, though: if Pump A and Moisture Sensor D
/// are both down, Moisture Sensor C is cut off.
///
/// Link weight (power cost): radio signal strength falls off roughly with the
/// square of distance in free space, so the transmit power needed to cross a
/// link grows with distance². The weight is distance² / 100, rounded to one
/// decimal place. A 10 m hop costs 1.0, a 20 m hop costs 4.0 and a 30 m hop
/// costs 9.0. One long hop therefore costs more than two short ones covering
/// the same ground, which is what makes the minimum spanning tree prefer
/// short relay hops.
///
/// The simulator's devices (5C:CF:7F:00:00:01 to 06) are placed here too, but
/// only join the graph once they have registered with the gateway.
/// </summary>
public static class MeshTopologySeed
{
    public const double SiteWidth = 100;
    public const double SiteHeight = 60;

    private const string MoistureA = "24:6F:28:A1:00:01";
    private const string MoistureB = "24:6F:28:A1:00:02";
    private const string MoistureC = "24:6F:28:A1:00:03";
    private const string MoistureD = "24:6F:28:A1:00:08";
    private const string PowerMeterA = "B4:E6:2D:C2:11:04";
    private const string PowerMeterB = "B4:E6:2D:C2:11:05";
    private const string PumpA = "7C:9E:BD:33:22:06";
    private const string Valve1 = "7C:9E:BD:33:22:07";
    private const string Valve2 = "7C:9E:BD:33:22:09";
    private const string Relay1 = "7C:9E:BD:33:22:0A";
    private const string Sim01 = "5C:CF:7F:00:00:01";
    private const string Sim02 = "5C:CF:7F:00:00:02";
    private const string Sim03 = "5C:CF:7F:00:00:03";
    private const string Sim04 = "5C:CF:7F:00:00:04";
    private const string Sim05 = "5C:CF:7F:00:00:05";
    private const string Sim06 = "5C:CF:7F:00:00:06";

    /// <summary>Site-plan position of every node in metres.</summary>
    public static readonly IReadOnlyDictionary<string, (double X, double Y)> Positions = new Dictionary<string, (double X, double Y)>(StringComparer.OrdinalIgnoreCase)
    {
        [MeshGraph.GatewayId] = (50, 30),
        [MoistureA] = (20, 12),
        [MoistureB] = (35, 8),
        [MoistureC] = (78, 10),
        [MoistureD] = (90, 22),
        [PowerMeterA] = (15, 45),
        [PowerMeterB] = (32, 22),
        [PumpA] = (62, 15),
        [Valve1] = (28, 55),
        [Valve2] = (68, 45),
        [Relay1] = (85, 50),
        [Sim01] = (6, 28),
        [Sim02] = (48, 52),
        [Sim03] = (95, 38),
        [Sim04] = (10, 6),
        [Sim05] = (55, 4),
        [Sim06] = (74, 57)
    };

    /// <summary>Pairs of nodes within radio range of each other (undirected).</summary>
    public static readonly IReadOnlyList<(string A, string B)> Links =
    [
        // Direct links to the gateway (the inner ring).
        (MeshGraph.GatewayId, PowerMeterB),
        (MeshGraph.GatewayId, PumpA),
        (MeshGraph.GatewayId, Valve2),
        (MeshGraph.GatewayId, Valve1),
        (MeshGraph.GatewayId, Sim02),

        // West side (Facility A, Zone 1).
        (PowerMeterB, MoistureA),
        (PowerMeterB, MoistureB),
        (PowerMeterB, PowerMeterA),
        (PowerMeterB, PumpA),
        (MoistureA, MoistureB),
        (MoistureA, PowerMeterA),
        (PowerMeterA, Valve1),
        (MoistureB, PumpA),

        // East side (Facility A, Zone 2 and the utility room).
        (PumpA, MoistureC),
        (MoistureC, MoistureD),
        (MoistureD, Valve2),
        (MoistureD, Relay1),
        (Relay1, Valve2),

        // Simulator devices (only present once registered).
        (Sim01, MoistureA),
        (Sim01, PowerMeterA),
        (Sim02, Valve1),
        (Sim02, Valve2),
        (Sim03, MoistureD),
        (Sim03, Relay1),
        (Sim04, MoistureA),
        (Sim04, Sim01),
        (Sim05, MoistureB),
        (Sim05, PumpA),
        (Sim06, Valve2),
        (Sim06, Relay1)
    ];

    /// <summary>Power cost of a link between two placed nodes: distance² / 100.</summary>
    public static double PowerCost(string a, string b)
    {
        var (ax, ay) = Positions[a];
        var (bx, by) = Positions[b];
        var distanceSquared = (ax - bx) * (ax - bx) + (ay - by) * (ay - by);
        return Math.Round(distanceSquared / 100, 1);
    }

    /// <summary>
    /// Builds the graph for the devices that are actually registered right
    /// now: the gateway, every registered device that has a position, and
    /// every link whose two ends are both present.
    /// </summary>
    public static MeshGraph BuildGraph(IEnumerable<string> registeredMacs)
    {
        var graph = new MeshGraph();
        graph.AddVertex(MeshGraph.GatewayId);

        foreach (var mac in registeredMacs)
        {
            if (Positions.ContainsKey(mac))
            {
                graph.AddVertex(mac);
            }
        }

        foreach (var (a, b) in Links)
        {
            if (graph.ContainsVertex(a) && graph.ContainsVertex(b))
            {
                graph.AddEdge(a, b, PowerCost(a, b));
            }
        }

        return graph;
    }
}
