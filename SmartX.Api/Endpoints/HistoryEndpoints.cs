using SmartX.Api.Services;
using SmartX.Shared.Dtos;
using SmartX.Shared.Realtime;
using SmartX.Shared.Trees;

namespace SmartX.Api.Endpoints;

/// <summary>Part 3 historical telemetry queries, answered by each device's AVL tree.</summary>
public static class HistoryEndpoints
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;

    public static void MapHistoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/history").WithTags("History");

        group.MapGet("/{device}", (string device, DateTimeOffset? from, DateTimeOffset? to, int? page, int? pageSize, DeviceDirectory directory, DeviceHistoryTrees<LiveReading> trees) =>
            {
                var target = DeviceLookup.Resolve(directory, device);
                if (target is null)
                {
                    return Results.NotFound($"Unknown device '{device}'.");
                }

                var span = trees.SpanFor(target.MacAddress);

                // No range given: the last hour of whatever history exists.
                var toUtc = to?.UtcDateTime ?? span?.Newest ?? DateTime.UtcNow;
                var fromUtc = from?.UtcDateTime ?? toUtc.AddHours(-1);

                if (fromUtc > toUtc)
                {
                    return Results.BadRequest("'from' must be before or equal to 'to'.");
                }

                var size = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
                var result = trees.Query(target.MacAddress, fromUtc, toUtc);
                var pageCount = Math.Max(1, (result.Items.Count + size - 1) / size);
                var current = Math.Clamp(page ?? 1, 1, pageCount);

                return Results.Ok(new HistoryQueryResult
                {
                    MacAddress = target.MacAddress,
                    DeviceName = target.DisplayName,
                    FromUtc = fromUtc,
                    ToUtc = toUtc,
                    // Already in timestamp order: the tree query is an in-order walk.
                    Items = result.Items.Skip((current - 1) * size).Take(size).Select(kv => kv.Value).ToList(),
                    Page = current,
                    PageSize = size,
                    MatchCount = result.Items.Count,
                    NodesVisited = result.NodesVisited,
                    TotalReadings = result.TotalReadings,
                    OldestUtc = span?.Oldest,
                    NewestUtc = span?.Newest
                });
            })
            .WithName("QueryDeviceHistory")
            .WithSummary("A device's readings between 'from' and 'to' (inclusive), paged, with how many AVL tree nodes the query visited.");
    }
}
