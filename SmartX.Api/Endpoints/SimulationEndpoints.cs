using SmartX.Shared.Collections;
using SmartX.Shared.Enums;
using SmartX.Shared.Realtime;

namespace SmartX.Api.Endpoints;

/// <summary>
/// Demo controls so the Part 2 structures can be exercised without running
/// the console simulator - each one is also wired to a dashboard button.
/// </summary>
public static class SimulationEndpoints
{
    private const int BacklogSize = 6;

    public static void MapSimulationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/simulate").WithTags("Simulation");

        group.MapPost("/critical-alert", (string? mac, SensorLedger ledger, IngestionPipeline pipeline) =>
            {
                var target = string.IsNullOrWhiteSpace(mac)
                    ? ledger.FirstOrDefault(s => s.Category == SensorCategory.Environmental)
                    : ledger.FindByMacAddress(mac);

                if (target is null || target.Category == SensorCategory.Actuator)
                {
                    return Results.BadRequest("Pick a registered moisture sensor or power meter to spike.");
                }

                var now = DateTime.UtcNow;
                var rng = Random.Shared;

                // A backlog of routine packets queued FIRST, then the critical
                // one LAST, all in one atomic batch. The worker still processes
                // the critical packet before any of the backlog.
                var backlog = ledger
                    .Where(s => s.Category != SensorCategory.Actuator && s.MacAddress != target.MacAddress)
                    .OrderBy(_ => rng.Next())
                    .Take(BacklogSize)
                    .Select(s => s.Category == SensorCategory.Environmental
                        ? new QueuedReading(s.MacAddress, ReadingKind.Moisture, Math.Round(35 + rng.NextDouble() * 20, 1), "%", now, AlertSeverity.Normal)
                        : new QueuedReading(s.MacAddress, ReadingKind.Wattage, rng.Next(300, 900), "W", now, AlertSeverity.Normal))
                    .ToList();

                var critical = target.Category == SensorCategory.Environmental
                    ? new QueuedReading(target.MacAddress, ReadingKind.Moisture, 6.5, "%", now, AlertSeverity.Critical)
                    : new QueuedReading(target.MacAddress, ReadingKind.Wattage, 4350, "W", now, AlertSeverity.Critical);

                pipeline.SubmitBatch([.. backlog, critical]);

                return Results.Accepted(value: new
                {
                    message = $"Queued {backlog.Count} routine packets, then a critical packet for {target.DisplayName}. Watch it get processed first.",
                    target = target.MacAddress,
                    backlog = backlog.Count
                });
            })
            .WithName("SimulateCriticalAlert")
            .WithSummary("Queues a routine backlog followed by one critical packet to demonstrate the priority bypass.");
    }
}
