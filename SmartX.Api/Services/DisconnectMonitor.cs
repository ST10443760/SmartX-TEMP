using SmartX.Shared.Enums;
using SmartX.Shared.Realtime;

namespace SmartX.Api.Services;

/// <summary>
/// Runs the <see cref="DisconnectDetector"/> once a second. A node that has
/// gone quiet is added to the active alert set (and an alert is pushed) the
/// first time only; every later scan that finds it still silent is counted
/// as a suppressed duplicate instead of a second alert.
/// </summary>
public sealed class DisconnectMonitor(
    DisconnectDetector detector,
    AlertBroadcaster alerts,
    GatewayCounters counters,
    ILogger<DisconnectMonitor> logger) : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(ScanInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var result = detector.Scan(DateTime.UtcNow);

                foreach (var mac in result.NewlyDisconnected)
                {
                    logger.LogWarning("Node {Mac} silent for over {Timeout}s - marked disconnected", mac, detector.Timeout.TotalSeconds);
                    await alerts.RaisedAsync(mac, AlertKind.Disconnected, $"has sent nothing for {detector.Timeout.TotalSeconds:0}s - marked disconnected", stoppingToken);
                }

                for (var i = 0; i < result.DuplicatesSuppressed; i++)
                {
                    counters.DuplicateSuppressed();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown.
        }
    }
}
