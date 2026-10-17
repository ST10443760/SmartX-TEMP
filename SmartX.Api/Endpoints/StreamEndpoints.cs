using SmartX.Api.Services;
using SmartX.Shared.Dtos;
using SmartX.Shared.Enums;
using SmartX.Shared.Realtime;

namespace SmartX.Api.Endpoints;

/// <summary>REST read side of the real-time command stream (first load, timeline chart, polling fallback).</summary>
public static class StreamEndpoints
{
    public static void MapStreamEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/stream").WithTags("Command Stream");

        group.MapGet("/snapshot", (DeviceDirectory directory, ActiveAlertSet alerts, ReadingHistoryStore<LiveReading> history, RecentReadingFeed recent, GatewayCounters counters, AlertBroadcaster broadcaster) =>
            {
                var disconnected = alerts.DisconnectedNodes();

                return Results.Ok(new StreamSnapshot
                {
                    Devices = directory.Snapshot()
                        .OrderBy(d => d.Category).ThenBy(d => d.DisplayName)
                        .Select(d => new DeviceStatus
                        {
                            MacAddress = d.MacAddress,
                            DisplayName = d.DisplayName,
                            Category = d.Category,
                            ActuatorKind = d.ActuatorKind,
                            LastValue = d.LastValue,
                            Unit = d.Unit,
                            LastSeenUtc = d.LastSeenUtc,
                            Severity = d.Severity,
                            IsDisconnected = disconnected.Contains(d.MacAddress),
                            HasActiveAlert = alerts.HasAny(d.MacAddress),
                            HistoryCount = history.CountFor(d.MacAddress)
                        })
                        .ToList(),
                    RecentReadings = recent.Snapshot(),
                    ActiveAlerts = ToActiveAlerts(alerts, directory),
                    RecentAlertEvents = broadcaster.Recent(),
                    Stats = counters.Snapshot()
                });
            })
            .WithName("GetStreamSnapshot")
            .WithSummary("Devices, recent readings, active alerts and queue stats in one call.");

        group.MapGet("/alerts", (ActiveAlertSet alerts, DeviceDirectory directory) => Results.Ok(ToActiveAlerts(alerts, directory)))
            .WithName("GetActiveAlerts")
            .WithSummary("Every entry currently in the active alert set (HashSet of unique error states).");

        group.MapGet("/history/{mac}", (string mac, int? take, DeviceDirectory directory, ReadingHistoryStore<LiveReading> history) =>
            {
                if (!directory.Contains(mac))
                {
                    return Results.NotFound($"Unknown device '{mac}'.");
                }

                // Already in timestamp order - it comes straight out of the SortedDictionary.
                return Results.Ok(history.Get(mac, Math.Clamp(take ?? 120, 1, history.CapacityPerDevice)).Select(kv => kv.Value));
            })
            .WithName("GetDeviceTimeline")
            .WithSummary("One device's processed readings in timestamp order, for the timeline chart.");
    }

    private static List<ActiveAlert> ToActiveAlerts(ActiveAlertSet alerts, DeviceDirectory directory)
        => alerts.Snapshot()
            .Select(entry => new ActiveAlert
            {
                MacAddress = entry.Key.MacAddress,
                DeviceName = directory.TryGet(entry.Key.MacAddress, out var device) ? device!.DisplayName : entry.Key.MacAddress,
                Kind = entry.Key.Kind,
                SinceUtc = entry.SinceUtc
            })
            .ToList();
}
