using Microsoft.AspNetCore.SignalR.Client;
using SmartX.Shared.Dtos;
using SmartX.Shared.Realtime;

namespace SmartX.Client.Services;

/// <summary>
/// Owns the dashboard's single SignalR connection to the gateway and writes
/// every pushed message into <see cref="LiveStateContainer"/>. Registered as a
/// singleton and started once from the main layout, so the connection (and
/// the data it has received) survives page navigation.
///
/// Graceful fallback: SignalR's automatic reconnect covers short drops. If
/// the connection closes for good, the service flips the badge to "Offline",
/// polls the REST snapshot every few seconds so the dashboard keeps updating,
/// and keeps trying to re-open the socket in the background.
/// </summary>
public sealed class TelemetryHubConnection(
    HttpClient http,
    LiveStateContainer state,
    SmartXApiClient api) : IAsyncDisposable
{
    private static readonly TimeSpan FallbackPollInterval = TimeSpan.FromSeconds(3);

    private HubConnection? _connection;
    private CancellationTokenSource? _fallbackCts;
    private bool _started;

    /// <summary>Raised after a (re)connect or fallback poll so pages can refresh anything they cache.</summary>
    public event Func<Task>? Resynced;

    public async Task EnsureStartedAsync()
    {
        if (_started)
        {
            return;
        }

        _started = true;

        // Same base address the typed API client uses (configured once in
        // Program.cs from ApiBaseUrl) - no second hardcoded host/port.
        var hubUrl = new Uri(http.BaseAddress!, HubEvents.HubRoute.TrimStart('/'));

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect([TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)])
            .Build();

        _connection.On<LiveReading>(HubEvents.Telemetry, state.ApplyReading);
        _connection.On<AlertEvent>(HubEvents.Alert, OnAlert);
        _connection.On<QueueStats>(HubEvents.QueueStats, state.SetQueueStats);
        _connection.On<CommandOutcome>(HubEvents.Command, outcome => state.ApplyCommandOutcome(outcome));

        _connection.Reconnecting += error =>
        {
            state.SetConnectionStatus(HubConnectionStatus.Reconnecting, error?.Message);
            return Task.CompletedTask;
        };

        _connection.Reconnected += async _ =>
        {
            state.SetConnectionStatus(HubConnectionStatus.Connected);
            await ResyncAsync();
        };

        _connection.Closed += _ =>
        {
            StartFallback();
            return Task.CompletedTask;
        };

        await TryConnectAsync();
        await ResyncAsync();
    }

    private async Task<bool> TryConnectAsync()
    {
        if (_connection is null)
        {
            return false;
        }

        try
        {
            state.SetConnectionStatus(HubConnectionStatus.Connecting);
            await _connection.StartAsync();
            state.SetConnectionStatus(HubConnectionStatus.Connected);
            StopFallback();
            return true;
        }
        catch (Exception ex)
        {
            StartFallback(ex.Message);
            return false;
        }
    }

    private void StartFallback(string? reason = null)
    {
        state.SetConnectionStatus(HubConnectionStatus.Offline, reason ?? "Live link lost - polling for updates");

        if (_fallbackCts is not null)
        {
            return;
        }

        _fallbackCts = new CancellationTokenSource();
        _ = FallbackLoopAsync(_fallbackCts.Token);
    }

    private void StopFallback()
    {
        _fallbackCts?.Cancel();
        _fallbackCts?.Dispose();
        _fallbackCts = null;
    }

    private async Task FallbackLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(FallbackPollInterval);
        var tick = 0;

        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                await ResyncAsync();

                // Every few polls, try to get the real-time socket back.
                if (++tick % 3 == 0 && _connection?.State == HubConnectionState.Disconnected)
                {
                    if (await TryConnectAsync())
                    {
                        await ResyncAsync();
                        return;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Fallback stopped because the socket came back.
        }
    }

    /// <summary>Pulls a full REST snapshot so nothing missed while disconnected stays missing.</summary>
    public async Task ResyncAsync()
    {
        try
        {
            var snapshot = await api.GetStreamSnapshotAsync();
            if (snapshot is not null)
            {
                state.ApplySnapshot(snapshot);
            }

            var commands = await api.GetCommandsAsync();
            if (commands is not null)
            {
                state.ApplyCommandOutcome(commands);
            }
        }
        catch
        {
            // API unreachable too - keep showing the last known state.
        }

        if (Resynced is not null)
        {
            await Resynced.Invoke();
        }
    }

    private void OnAlert(AlertEvent alert)
    {
        state.ApplyAlert(alert);
        _ = RefreshActiveAlertsAsync();
    }

    private async Task RefreshActiveAlertsAsync()
    {
        try
        {
            state.SetActiveAlerts(await api.GetActiveAlertsAsync());
        }
        catch
        {
            // Next snapshot will catch up.
        }
    }

    public async ValueTask DisposeAsync()
    {
        StopFallback();
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }
}
