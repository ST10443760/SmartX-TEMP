using Microsoft.AspNetCore.SignalR;
using SmartX.Api.Hubs;
using SmartX.Shared.Collections;
using SmartX.Shared.Dtos;
using SmartX.Shared.Realtime;

namespace SmartX.Api.Services;

/// <summary>
/// The processing engine's consumer. Every tick it drains the
/// <see cref="IngestionPipeline"/> - the priority queue first, then the FIFO
/// queue - turns each packet into a <see cref="LiveReading"/> and pushes it
/// to every connected dashboard over SignalR.
///
/// The short pause between drains is deliberate: packets that arrive within
/// the same window pile up in the queues, which is what makes the priority
/// bypass visible (a critical packet that arrived last is processed first).
/// </summary>
public sealed class TelemetryProcessingWorker(
    IngestionPipeline pipeline,
    SensorLedger ledger,
    GatewayCounters counters,
    RecentReadingFeed recentFeed,
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
        var sensor = ledger.FindByMacAddress(queued.MacAddress);
        if (sensor is null)
        {
            counters.RejectedUnknown();
            logger.LogWarning("Dropped a queued packet from unknown MAC {Mac}", queued.MacAddress);
            return;
        }

        var reading = new LiveReading
        {
            MacAddress = sensor.MacAddress,
            DeviceName = sensor.DisplayName,
            Category = sensor.Category,
            Kind = queued.Kind,
            Value = queued.Value,
            Unit = queued.Unit,
            TimestampUtc = queued.TimestampUtc,
            Severity = queued.Severity,
            Lane = lane == IngestionLane.Priority ? "Priority" : "FIFO",
            ProcessedSequence = counters.NextProcessedSequence(lane == IngestionLane.Priority)
        };

        recentFeed.Add(reading);
        await hub.Clients.All.SendAsync(HubEvents.Telemetry, reading, ct);
    }
}
