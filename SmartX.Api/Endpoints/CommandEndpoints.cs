using Microsoft.AspNetCore.SignalR;
using SmartX.Api.Hubs;
using SmartX.Shared.Commands;
using SmartX.Shared.Dtos;
using SmartX.Shared.Realtime;

namespace SmartX.Api.Endpoints;

/// <summary>Manual override commands and Undo, backed by the override command stack.</summary>
public static class CommandEndpoints
{
    public static void MapCommandEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/commands").WithTags("Override Commands");

        group.MapGet("/", (OverrideCommandProcessor processor) => Results.Ok(processor.Snapshot()))
            .WithName("GetCommandHistory")
            .WithSummary("Actuator states plus the override command stack (most recent first).");

        group.MapGet("/actuators", (OverrideCommandProcessor processor) => Results.Ok(processor.Actuators()))
            .WithName("GetActuatorStates")
            .WithSummary("Commanded state of every pump, valve and relay - simulated devices report this back.");

        group.MapPost("/", async (OverrideCommandRequest request, OverrideCommandProcessor processor, IHubContext<TelemetryHub> hub) =>
            {
                var outcome = processor.Execute(request, DateTime.UtcNow);
                if (!outcome.Success)
                {
                    return Results.BadRequest(outcome);
                }

                await hub.Clients.All.SendAsync(HubEvents.Command, outcome);
                return Results.Ok(outcome);
            })
            .WithName("ExecuteOverrideCommand")
            .WithSummary("Executes a manual override (trigger pump, toggle relay, open valve, close all valves) and pushes it onto the undo stack.");

        group.MapPost("/undo", async (OverrideCommandProcessor processor, IHubContext<TelemetryHub> hub) =>
            {
                var outcome = processor.Undo();
                if (!outcome.Success)
                {
                    // Empty stack: a normal, expected situation - report it, don't crash.
                    return Results.Conflict(outcome);
                }

                await hub.Clients.All.SendAsync(HubEvents.Command, outcome);
                return Results.Ok(outcome);
            })
            .WithName("UndoLastCommand")
            .WithSummary("Pops the most recent override and applies its inverse. Returns 409 if there is nothing to undo.");
    }
}
