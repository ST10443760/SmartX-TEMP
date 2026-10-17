using SmartX.Api.Services;
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

        group.MapPost("/critical-alert", (string? mac, DeviceDirectory directory, IngestionPipeline pipeline) =>
            {
                var devices = directory.Snapshot();
                var target = string.IsNullOrWhiteSpace(mac)
                    ? devices.FirstOrDefault(d => d.Category == SensorCategory.Environmental)
                    : devices.FirstOrDefault(d => string.Equals(d.MacAddress, mac, StringComparison.OrdinalIgnoreCase));

                if (target is null || target.Category == SensorCategory.Actuator)
                {
                    return Results.BadRequest("Pick a registered moisture sensor or power meter to spike.");
                }

                var now = DateTime.UtcNow;
                var rng = Random.Shared;

                // A backlog of routine packets queued FIRST, then the critical
                // one LAST, all in one atomic batch. The worker still processes
                // the critical packet before any of the backlog.
                var backlog = devices
                    .Where(d => d.Category != SensorCategory.Actuator && d.MacAddress != target.MacAddress)
                    .OrderBy(_ => rng.Next())
                    .Take(BacklogSize)
                    .Select(d => d.Category == SensorCategory.Environmental
                        ? new QueuedReading(d.MacAddress, ReadingKind.Moisture, Math.Round(35 + rng.NextDouble() * 20, 1), "%", now, AlertSeverity.Normal)
                        : new QueuedReading(d.MacAddress, ReadingKind.Wattage, rng.Next(300, 900), "W", now, AlertSeverity.Normal))
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

        group.MapPost("/disconnect/{mac}", async (string mac, DeviceDirectory directory, DisconnectDetector detector, AlertBroadcaster alerts) =>
            {
                if (!directory.TryGet(mac, out var device) || device is null)
                {
                    return Results.NotFound($"Unknown device '{mac}'.");
                }

                var isNew = detector.ForceDisconnect(device.MacAddress, DateTime.UtcNow);
                if (isNew)
                {
                    await alerts.RaisedAsync(device.MacAddress, AlertKind.Disconnected, "was marked disconnected (simulated)");
                }
                else
                {
                    await alerts.DuplicateSuppressedAsync(device.MacAddress, AlertKind.Disconnected);
                }

                return Results.Ok(new
                {
                    newAlert = isNew,
                    message = isNew
                        ? $"{device.DisplayName} added to the disconnected set - alert raised."
                        : $"{device.DisplayName} is already in the disconnected set - duplicate ignored, no second alert."
                });
            })
            .WithName("SimulateDisconnect")
            .WithSummary("Marks a node disconnected. Calling it twice shows the HashSet ignoring the duplicate.");
    }
}
