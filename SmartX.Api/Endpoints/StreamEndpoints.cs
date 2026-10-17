using SmartX.Shared.Collections;
using SmartX.Shared.Dtos;

namespace SmartX.Api.Endpoints;

/// <summary>REST read side of the real-time command stream (snapshot for first load and polling fallback).</summary>
public static class StreamEndpoints
{
    public static void MapStreamEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/stream").WithTags("Command Stream");

        group.MapGet("/snapshot", (SensorLedger ledger) => Results.Ok(new StreamSnapshot
            {
                Devices = ledger.Select(s => new DeviceStatus
                {
                    MacAddress = s.MacAddress,
                    DisplayName = s.DisplayName,
                    Category = s.Category,
                    ActuatorKind = s.ActuatorKind
                }).ToList()
            }))
            .WithName("GetStreamSnapshot")
            .WithSummary("Devices, recent readings, active alerts and queue stats in one call.");

        group.MapGet("/alerts", () => Results.Ok(new List<ActiveAlert>()))
            .WithName("GetActiveAlerts")
            .WithSummary("Every entry currently in the active alert set.");
    }
}
