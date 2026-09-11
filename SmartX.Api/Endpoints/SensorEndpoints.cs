using SmartX.Api.Services;
using SmartX.Shared.Collections;
using SmartX.Shared.Dtos;
using SmartX.Shared.Models;
using SmartX.Shared.Validation;

namespace SmartX.Api.Endpoints;

public static class SensorEndpoints
{
    public static void MapSensorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sensors").WithTags("Sensors");

        group.MapGet("/", (SensorLedger ledger) => Results.Ok(ledger.ToList()))
            .WithName("GetSensors")
            .WithSummary("Lists every registered sensor.");

        group.MapGet("/{mac}", (string mac, SensorLedger ledger) =>
            {
                var sensor = ledger.FindByMacAddress(mac);
                return sensor is null ? Results.NotFound() : Results.Ok(sensor);
            })
            .WithName("GetSensorByMac")
            .WithSummary("Fetches a single sensor by MAC address / unique identifier.");

        group.MapPost("/", (SensorRegistrationRequest request, SensorLedger ledger, DeploymentNode deploymentRoot) =>
            {
                if (string.IsNullOrWhiteSpace(request.MacAddress))
                {
                    return Results.BadRequest(SensorRegistrationResult.Fail("MAC address / unique identifier is required."));
                }

                if (request.DeploymentPathSegments is not { Count: > 0 })
                {
                    return Results.BadRequest(SensorRegistrationResult.Fail("A deployment location path is required."));
                }

                if (!DeploymentTreeValidator.IsValidPath(deploymentRoot, request.DeploymentPathSegments))
                {
                    var deepest = DeploymentTreeValidator.FindDeepestMatch(deploymentRoot, request.DeploymentPathSegments);
                    var hint = deepest is null
                        ? "The path does not start at a recognised facility."
                        : $"The path breaks after '{deepest.Name}' - no matching child found for the next segment.";

                    return Results.BadRequest(SensorRegistrationResult.Fail(
                        $"Deployment path '{string.Join(" -> ", request.DeploymentPathSegments)}' is not valid. {hint}"));
                }

                var sensor = new SensorRecord
                {
                    Id = Guid.NewGuid(),
                    MacAddress = request.MacAddress.Trim(),
                    Category = request.Category,
                    DeploymentPath = DeploymentPath.Of([.. request.DeploymentPathSegments])
                };

                try
                {
                    ledger.Register(sensor);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.Conflict(SensorRegistrationResult.Fail(ex.Message));
                }

                return Results.Created($"/api/sensors/{sensor.MacAddress}", SensorRegistrationResult.Ok(sensor.Id));
            })
            .WithName("RegisterSensor")
            .WithSummary("Registers a new (simulated) sensor node after validating its deployment path recursively.");

        group.MapPost("/{mac}/attachments", async (string mac, IFormFile file, SensorLedger ledger, AttachmentStorage storage, CancellationToken ct) =>
            {
                var sensor = ledger.FindByMacAddress(mac);
                if (sensor is null)
                {
                    return Results.NotFound($"No sensor registered with MAC address '{mac}'.");
                }

                var (isValid, error) = storage.Validate(file);
                if (!isValid)
                {
                    return Results.BadRequest(error);
                }

                var attachment = await storage.SaveAsync(sensor.MacAddress, file, ct);
                ledger.TryAttachFile(sensor.MacAddress, attachment);

                return Results.Created($"/api/sensors/{sensor.MacAddress}/attachments/{attachment.Id}", attachment);
            })
            .DisableAntiforgery()
            .WithName("UploadSensorAttachment")
            .WithSummary("Uploads a config file, deployment photo, or hardware log against a sensor profile.");

        group.MapGet("/{mac}/attachments/{attachmentId:guid}/download", (string mac, Guid attachmentId, SensorLedger ledger, AttachmentStorage storage) =>
            {
                var sensor = ledger.FindByMacAddress(mac);
                var attachment = sensor?.Attachments.FirstOrDefault(a => a.Id == attachmentId);
                if (sensor is null || attachment is null)
                {
                    return Results.NotFound();
                }

                var path = storage.ResolvePath(sensor.MacAddress, attachment.StoredFileName);
                if (!File.Exists(path))
                {
                    return Results.NotFound("The attachment metadata exists but the file is missing on disk.");
                }

                return Results.File(File.OpenRead(path), attachment.ContentType, attachment.FileName);
            })
            .WithName("DownloadSensorAttachment")
            .WithSummary("Downloads a previously uploaded sensor attachment.");
    }
}
