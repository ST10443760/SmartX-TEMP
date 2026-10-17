namespace SmartX.Shared.Realtime;

/// <summary>
/// The SignalR hub route and the names of every message the gateway pushes
/// to connected dashboards. Kept in the shared library so the API and the
/// Blazor client cannot drift apart on a magic string.
/// </summary>
public static class HubEvents
{
    public const string HubRoute = "/hubs/telemetry";

    public const string Telemetry = "telemetry";
    public const string Alert = "alert";
    public const string Command = "command";
    public const string QueueStats = "queueStats";
    public const string Insights = "insights";
}
