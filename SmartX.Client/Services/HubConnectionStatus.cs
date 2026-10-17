namespace SmartX.Client.Services;

/// <summary>State of the dashboard's SignalR link, shown as a badge in the top bar.</summary>
public enum HubConnectionStatus
{
    Connecting,
    Connected,
    Reconnecting,

    /// <summary>The socket is down; the dashboard falls back to polling the REST snapshots.</summary>
    Offline
}
