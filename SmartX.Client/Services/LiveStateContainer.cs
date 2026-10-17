using SmartX.Shared.Dtos;
using SmartX.Shared.Enums;

namespace SmartX.Client.Services;

/// <summary>
/// Client-side state container for everything live on the dashboard,
/// registered as a singleton so it outlives any single page. Navigating from
/// the Command Stream to the Telemetry page and back keeps the feed, the
/// filters, the alert list and the command history exactly as they were,
/// because the pages only render this object - they never own the data.
///
/// Components subscribe to <see cref="Changed"/> in OnInitialized and
/// unsubscribe in Dispose; the SignalR connection service writes into it.
/// </summary>
public sealed class LiveStateContainer
{
    public const int MaxFeedItems = 60;
    public const int MaxAlertEvents = 40;
    public const int MaxTimelinePoints = 120;

    private readonly List<LiveReading> _feed = [];
    private readonly List<AlertEvent> _alertEvents = [];
    private readonly Dictionary<string, DeviceStatus> _devices = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Raised whenever anything in the container changes; subscribers re-render.</summary>
    public event Action? Changed;

    public HubConnectionStatus ConnectionStatus { get; private set; } = HubConnectionStatus.Connecting;

    public string? ConnectionDetail { get; private set; }

    /// <summary>Newest first, capped at <see cref="MaxFeedItems"/>.</summary>
    public IReadOnlyList<LiveReading> Feed => _feed;

    /// <summary>Newest first, capped at <see cref="MaxAlertEvents"/>.</summary>
    public IReadOnlyList<AlertEvent> AlertEvents => _alertEvents;

    public IReadOnlyList<ActiveAlert> ActiveAlerts { get; private set; } = [];

    public QueueStats Stats { get; private set; } = new();

    public IReadOnlyCollection<DeviceStatus> Devices => _devices.Values;

    public DateTime? LastMessageUtc { get; private set; }

    // ---- Persisted UI state (filters / selections survive navigation) ----

    public string SearchText { get; set; } = "";

    public SensorCategory? CategoryFilter { get; set; }

    public bool AlertsOnly { get; set; }

    public string? SelectedMac { get; set; }

    /// <summary>Which tab the Part 1 telemetry page was last on.</summary>
    public string TelemetryTab { get; set; } = "Register";

    /// <summary>Last Mesh Pulse snapshot, so the widget renders instantly when you navigate back.</summary>
    public IReadOnlyList<TelemetryPulse> LastPulses { get; set; } = [];

    public DateTimeOffset? LastPulseUtc { get; set; }

    // ---- Override commands (stack lives server-side; this mirrors it) ----

    public IReadOnlyList<ActuatorState> Actuators { get; private set; } = [];

    /// <summary>The server's override command stack, most recent first.</summary>
    public IReadOnlyList<CommandRecord> CommandHistory { get; private set; } = [];

    public string? LastCommandMessage { get; private set; }

    public bool LastCommandFailed { get; private set; }

    public void ApplyCommandOutcome(CommandOutcome outcome, bool fromThisClient = false)
    {
        Actuators = outcome.Actuators;
        CommandHistory = outcome.History;

        if (!string.IsNullOrEmpty(outcome.Message) && (fromThisClient || outcome.Success))
        {
            LastCommandMessage = outcome.Message;
            LastCommandFailed = !outcome.Success;
        }

        NotifyChanged();
    }

    // ---- Timeline for the selected device (kept so navigation does not reset it) ----

    public string? TimelineMac { get; private set; }

    public List<LiveReading> TimelinePoints { get; } = [];

    public void SetTimeline(string mac, IEnumerable<LiveReading> points)
    {
        TimelineMac = mac;
        TimelinePoints.Clear();
        TimelinePoints.AddRange(points);
        NotifyChanged();
    }

    // ---- Automated Action Engine ----

    public InsightsSnapshot Insights { get; private set; } = new();

    public void SetInsights(InsightsSnapshot insights)
    {
        Insights = insights;
        NotifyChanged();
    }

    // ---- Writers (called by the hub connection and snapshot sync) ----

    public void SetConnectionStatus(HubConnectionStatus status, string? detail = null)
    {
        ConnectionStatus = status;
        ConnectionDetail = detail;
        NotifyChanged();
    }

    public void ApplyReading(LiveReading reading)
    {
        _feed.Insert(0, reading);
        if (_feed.Count > MaxFeedItems)
        {
            _feed.RemoveRange(MaxFeedItems, _feed.Count - MaxFeedItems);
        }

        if (TimelineMac is not null && string.Equals(TimelineMac, reading.MacAddress, StringComparison.OrdinalIgnoreCase))
        {
            // Keep the chart live without refetching. Insert in timestamp
            // order, mirroring the server's SortedDictionary.
            var index = TimelinePoints.FindLastIndex(p => p.TimestampUtc <= reading.TimestampUtc) + 1;
            TimelinePoints.Insert(index, reading);
            if (TimelinePoints.Count > MaxTimelinePoints)
            {
                TimelinePoints.RemoveAt(0);
            }
        }

        if (_devices.TryGetValue(reading.MacAddress, out var device))
        {
            _devices[reading.MacAddress] = new DeviceStatus
            {
                MacAddress = device.MacAddress,
                DisplayName = device.DisplayName,
                Category = device.Category,
                ActuatorKind = device.ActuatorKind,
                LastValue = reading.Value,
                Unit = reading.Unit,
                LastSeenUtc = reading.TimestampUtc,
                Severity = reading.Severity,
                IsDisconnected = false,
                HasActiveAlert = device.HasActiveAlert,
                HistoryCount = device.HistoryCount + 1
            };
        }

        LastMessageUtc = DateTime.UtcNow;
        NotifyChanged();
    }

    public void ApplyAlert(AlertEvent alert)
    {
        _alertEvents.Insert(0, alert);
        if (_alertEvents.Count > MaxAlertEvents)
        {
            _alertEvents.RemoveRange(MaxAlertEvents, _alertEvents.Count - MaxAlertEvents);
        }

        LastMessageUtc = DateTime.UtcNow;
        NotifyChanged();
    }

    public void SetActiveAlerts(IReadOnlyList<ActiveAlert> alerts)
    {
        ActiveAlerts = alerts;
        NotifyChanged();
    }

    public void SetQueueStats(QueueStats stats)
    {
        Stats = stats;
        NotifyChanged();
    }

    public void SetDevices(IEnumerable<DeviceStatus> devices)
    {
        _devices.Clear();
        foreach (var device in devices)
        {
            _devices[device.MacAddress] = device;
        }

        NotifyChanged();
    }

    public void SetRecentFeed(IEnumerable<LiveReading> readingsNewestFirst)
    {
        _feed.Clear();
        _feed.AddRange(readingsNewestFirst.Take(MaxFeedItems));
        NotifyChanged();
    }

    public void ApplySnapshot(StreamSnapshot snapshot)
    {
        _devices.Clear();
        foreach (var device in snapshot.Devices)
        {
            _devices[device.MacAddress] = device;
        }

        if (snapshot.RecentReadings.Count > 0)
        {
            _feed.Clear();
            _feed.AddRange(snapshot.RecentReadings.Take(MaxFeedItems));
        }

        ActiveAlerts = snapshot.ActiveAlerts;

        if (snapshot.RecentAlertEvents.Count > 0)
        {
            _alertEvents.Clear();
            _alertEvents.AddRange(snapshot.RecentAlertEvents.Take(MaxAlertEvents));
        }

        Stats = snapshot.Stats;
        NotifyChanged();
    }

    public bool HasAlert(string mac) => ActiveAlerts.Any(a => string.Equals(a.MacAddress, mac, StringComparison.OrdinalIgnoreCase));

    public bool IsDisconnected(string mac) => ActiveAlerts.Any(a => a.Kind == AlertKind.Disconnected && string.Equals(a.MacAddress, mac, StringComparison.OrdinalIgnoreCase));

    public DeviceStatus? FindDevice(string mac) => _devices.GetValueOrDefault(mac);

    public void NotifyChanged() => Changed?.Invoke();
}
