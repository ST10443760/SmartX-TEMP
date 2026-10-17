using System.Globalization;
using Microsoft.AspNetCore.SignalR;
using SmartX.Api.Hubs;
using SmartX.Shared.Dtos;
using SmartX.Shared.Enums;
using SmartX.Shared.Realtime;

namespace SmartX.Api.Services;

/// <summary>
/// The processing engine's consumer. Every tick it drains the
/// <see cref="IngestionPipeline"/> - the priority queue first, then the FIFO
/// queue - and for each packet:
/// <list type="number">
///   <item>finds the device in the MAC-keyed <see cref="DeviceDirectory"/> (O(1); unknown MACs are counted and dropped),</item>
///   <item>tells the <see cref="DisconnectDetector"/> the node is alive (raising "recovered" if it had been flagged),</item>
///   <item>adds the reading to the device's sorted <see cref="ReadingHistoryStore{T}"/>,</item>
///   <item>raises or clears a critical-reading entry in the <see cref="ActiveAlertSet"/>,</item>
///   <item>pushes the processed reading to every dashboard over SignalR.</item>
/// </list>
/// The short pause between drains is deliberate: packets that arrive within
/// the same window pile up in the queues, which is what makes the priority
/// bypass visible (a critical packet that arrived last is processed first).
/// </summary>
public sealed class TelemetryProcessingWorker(
    IngestionPipeline pipeline,
    DeviceDirectory directory,
    ReadingHistoryStore<LiveReading> history,
    ActiveAlertSet activeAlerts,
    DisconnectDetector disconnects,
    AlertBroadcaster alerts,
    GatewayCounters counters,
    RecentReadingFeed recentFeed,
    InsightsService insights,
    IHubContext<TelemetryHub> hub,
    ILogger<TelemetryProcessingWorker> logger) : BackgroundService
{
    private static readonly TimeSpan DrainInterval = TimeSpan.FromMilliseconds(250);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processedAny = false;

                while (pipeline.TryTakeNext(out var queued, out var lane))
                {
                    await ProcessAsync(queued, lane, stoppingToken);
                    processedAny = true;
                }

                if (processedAny)
                {
                    await hub.Clients.All.SendAsync(HubEvents.QueueStats, counters.Snapshot(), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // One bad packet must never kill the engine.
                logger.LogError(ex, "Telemetry worker failed while draining the queues");
            }

            try
            {
                await Task.Delay(DrainInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Expected on shutdown.
            }
        }
    }

    private async Task ProcessAsync(QueuedReading queued, IngestionLane lane, CancellationToken ct)
    {
        if (!directory.TryGet(queued.MacAddress, out var device) || device is null)
        {
            // The ingest endpoint already rejects unknown MACs; this guards
            // packets queued by other paths. Flag and drop - never crash.
            counters.RejectedUnknown();
            logger.LogWarning("Dropped a queued packet from unknown MAC {Mac}", queued.MacAddress);
            return;
        }

        var now = DateTime.UtcNow;

        if (disconnects.RecordSeen(device.MacAddress, now))
        {
            await alerts.RecoveredAsync(device.MacAddress, AlertKind.Disconnected, ct);
        }

        var reading = new LiveReading
        {
            MacAddress = device.MacAddress,
            DeviceName = device.DisplayName,
            Category = device.Category,
            Kind = queued.Kind,
            Value = queued.Value,
            Unit = queued.Unit,
            TimestampUtc = queued.TimestampUtc,
            Severity = queued.Severity,
            Lane = lane == IngestionLane.Priority ? "Priority" : "FIFO",
            ProcessedSequence = counters.NextProcessedSequence(lane == IngestionLane.Priority)
        };

        directory.RecordReading(device.MacAddress, reading.Value, reading.Unit, reading.TimestampUtc, reading.Severity);
        history.Add(device.MacAddress, reading.TimestampUtc, reading);
        recentFeed.Add(reading);
        insights.ObserveReading(reading);

        await hub.Clients.All.SendAsync(HubEvents.Telemetry, reading, ct);

        if (reading.Severity == AlertSeverity.Critical)
        {
            if (activeAlerts.TryRaise(device.MacAddress, AlertKind.CriticalReading, now))
            {
                await alerts.RaisedAsync(device.MacAddress, AlertKind.CriticalReading, string.Create(CultureInfo.InvariantCulture, $"reported a critical reading of {reading.Value:0.#}{reading.Unit}"), ct);
            }
            else
            {
                await alerts.DuplicateSuppressedAsync(device.MacAddress, AlertKind.CriticalReading, ct);
            }
        }
        else if (reading.Severity == AlertSeverity.Normal && activeAlerts.TryRecover(device.MacAddress, AlertKind.CriticalReading))
        {
            await alerts.RecoveredAsync(device.MacAddress, AlertKind.CriticalReading, ct);
        }
    }
}
