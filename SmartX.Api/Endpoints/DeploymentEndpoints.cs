using SmartX.Shared.Models;

namespace SmartX.Api.Endpoints;

public static class DeploymentEndpoints
{
    public static void MapDeploymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/deployment-tree").WithTags("Deployment Tree");

        group.MapGet("/", (DeploymentNode root) => Results.Ok(root))
            .WithName("GetDeploymentTree")
            .WithSummary("Returns the full facility/zone/sub-zone/node hierarchy sensors register against.");
    }
}
