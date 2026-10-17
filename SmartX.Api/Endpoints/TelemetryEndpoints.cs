using SmartX.Api.Services;
using SmartX.Shared.Collections;
using SmartX.Shared.Dtos;
using SmartX.Shared.Realtime;

namespace SmartX.Api.Endpoints;

public static class TelemetryEndpoints
{
    public static void MapTelemetryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/telemetry").WithTags("Telemetry");

        group.MapGet("/pulses", (TelemetryPulseGenerator generator) => Results.Ok(generator.Snapshot()))
            .WithName("GetTelemetryPulses")
            .WithSummary("Live, simulated per-sensor readings driving the dashboard's Mesh Pulse engagement widget.");

        group.MapGet("/history/{sensorId}", (string sensorId, int? take, TelemetryHistoryStore history) =>
                Results.Ok(history.GetHistory(sensorId, take ?? 50)))
            .WithName("GetTelemetryHistory")
            .WithSummary("Flattened historical readings for one sensor, originally captured as jagged raw batches.");

        group.MapPost("/ingest", (TelemetryIngestRequest request, SensorLedger ledger, IngestionPipeline pipeline, GatewayCounters counters) =>
            {
                if (string.IsNullOrWhiteSpace(request.MacAddress) || ledger.FindByMacAddress(request.MacAddress) is null)
                {
                    counters.RejectedUnknown();
                    return Results.NotFound($"Unknown device '{request.MacAddress}'. Register it via POST /api/sensors first.");
                }

                if (!TelemetryIngestMapper.TryMap(request, DateTime.UtcNow, out var reading, out var error))
                {
                    return Results.BadRequest(error);
                }

                // Enqueue and return straight away - the background worker
                // does the real processing, so a device is never kept waiting.
                var lane = pipeline.Submit(reading!);

                return Results.Accepted(value: new IngestAccepted
                {
                    Lane = lane.ToString(),
                    Severity = reading!.Severity,
                    QueueDepth = lane == IngestionLane.Priority ? pipeline.PriorityDepth : pipeline.FifoDepth
                });
            })
            .WithName("IngestTelemetry")
            .WithSummary("Accepts one telemetry packet (float moisture, int watts or bool valve state), queues it and returns 202.");
    }
}
