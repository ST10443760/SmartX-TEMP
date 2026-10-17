namespace SmartX.Shared.Dtos;

/// <summary>
/// Everything the command stream dashboard needs in one REST call - used on
/// first load, after a SignalR reconnect, and by the polling fallback while
/// the socket is down.
/// </summary>
public sealed class StreamSnapshot
{
    public List<DeviceStatus> Devices { get; init; } = [];

    /// <summary>Newest first.</summary>
    public List<LiveReading> RecentReadings { get; init; } = [];

    public List<ActiveAlert> ActiveAlerts { get; init; } = [];

    /// <summary>Recent alert transitions, newest first.</summary>
    public List<AlertEvent> RecentAlertEvents { get; init; } = [];

    public QueueStats Stats { get; init; } = new();
}
