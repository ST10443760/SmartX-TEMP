using SmartX.Api.Services;
using SmartX.Shared.Dtos;

namespace SmartX.Api.Endpoints;

/// <summary>The Automated Action Engine: current suggestions and the search-recording hook.</summary>
public static class InsightsEndpoints
{
    public static void MapInsightsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/insights").WithTags("Insights");

        group.MapGet("/", (InsightsService insights) => Results.Ok(insights.BuildSnapshot()))
            .WithName("GetInsights")
            .WithSummary("Suggested next actions (learnt from history) plus devices flagged from the live data.");

        group.MapPost("/search", async (ActivityRequest request, InsightsService insights) =>
            {
                if (string.IsNullOrWhiteSpace(request.SearchText))
                {
                    return Results.BadRequest("searchText is required.");
                }

                await insights.RecordSearchAsync(request.SearchText);
                return Results.Ok(insights.BuildSnapshot());
            })
            .WithName("RecordSearch")
            .WithSummary("Records a completed dashboard search so the engine can learn what operators look for next.");
    }
}
