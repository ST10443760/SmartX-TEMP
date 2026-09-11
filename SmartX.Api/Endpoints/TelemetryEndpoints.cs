using SmartX.Api.Services;

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
    }
}
