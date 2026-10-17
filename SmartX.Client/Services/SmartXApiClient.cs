using System.Net.Http.Json;
using SmartX.Shared.Dtos;
using SmartX.Shared.Models;

namespace SmartX.Client.Services;

/// <summary>
/// Thin, typed wrapper around the SmartX gateway API. Every call goes over
/// real HTTP to the Minimal API project - nothing here talks to the backend
/// in-process - so the client and API can be deployed, scaled, and demoed
/// independently.
/// </summary>
public sealed class SmartXApiClient(HttpClient http)
{
    public async Task<DeploymentNode?> GetDeploymentTreeAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<DeploymentNode>("api/deployment-tree", ct);

    public async Task<List<SensorRecord>> GetSensorsAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<SensorRecord>>("api/sensors", ct) ?? [];

    public async Task<SensorRegistrationResult> RegisterSensorAsync(SensorRegistrationRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("api/sensors", request, ct);
        var result = await response.Content.ReadFromJsonAsync<SensorRegistrationResult>(ct);
        return result ?? SensorRegistrationResult.Fail("The server returned an unexpected empty response.");
    }

    public async Task<(bool Success, string? Error, SensorAttachment? Attachment)> UploadAttachmentAsync(
        string macAddress, string fileName, string contentType, Stream content, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        using var fileContent = new StreamContent(content);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        form.Add(fileContent, "file", fileName);

        var response = await http.PostAsync($"api/sensors/{Uri.EscapeDataString(macAddress)}/attachments", form, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return (false, error, null);
        }

        var attachment = await response.Content.ReadFromJsonAsync<SensorAttachment>(ct);
        return (true, null, attachment);
    }

    public async Task<List<TelemetryPulse>> GetLivePulsesAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<TelemetryPulse>>("api/telemetry/pulses", ct) ?? [];

    public async Task<List<TelemetryPacket<double>>> GetHistoryAsync(string sensorId, int take = 20, CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<TelemetryPacket<double>>>($"api/telemetry/history/{Uri.EscapeDataString(sensorId)}?take={take}", ct) ?? [];

    public async Task<StreamSnapshot?> GetStreamSnapshotAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<StreamSnapshot>("api/stream/snapshot", ct);

    public async Task<List<ActiveAlert>> GetActiveAlertsAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<ActiveAlert>>("api/stream/alerts", ct) ?? [];

    public async Task<List<LiveReading>> GetTimelineAsync(string mac, int take = 120, CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<LiveReading>>($"api/stream/history/{Uri.EscapeDataString(mac)}?take={take}", ct) ?? [];

    public async Task<CommandOutcome?> GetCommandsAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<CommandOutcome>("api/commands", ct);

    /// <summary>Executes an override. Failed commands come back as a 400 with an outcome body, which is returned as-is.</summary>
    public async Task<CommandOutcome> ExecuteCommandAsync(OverrideCommandRequest request, CancellationToken ct = default)
        => await ReadOutcomeAsync(await http.PostAsJsonAsync("api/commands", request, ct), ct);

    /// <summary>Undoes the last override. An empty stack comes back as a 409 with an outcome body explaining why.</summary>
    public async Task<CommandOutcome> UndoAsync(CancellationToken ct = default)
        => await ReadOutcomeAsync(await http.PostAsync("api/commands/undo", null, ct), ct);

    public async Task<string> SimulateCriticalAlertAsync(string? mac = null, CancellationToken ct = default)
        => await ReadMessageAsync(await http.PostAsync($"api/simulate/critical-alert{(mac is null ? "" : $"?mac={Uri.EscapeDataString(mac)}")}", null, ct), ct);

    public async Task<string> SimulateDisconnectAsync(string mac, CancellationToken ct = default)
        => await ReadMessageAsync(await http.PostAsync($"api/simulate/disconnect/{Uri.EscapeDataString(mac)}", null, ct), ct);

    private static async Task<CommandOutcome> ReadOutcomeAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<CommandOutcome>(ct)
                ?? new CommandOutcome { Success = false, Message = $"Empty response ({(int)response.StatusCode})." };
        }
        catch (System.Text.Json.JsonException)
        {
            return new CommandOutcome { Success = false, Message = $"Unexpected response ({(int)response.StatusCode})." };
        }
    }

    private static async Task<string> ReadMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object && doc.RootElement.TryGetProperty("message", out var message))
            {
                return message.GetString() ?? body;
            }

            return doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.String ? doc.RootElement.GetString() ?? body : body;
        }
        catch (System.Text.Json.JsonException)
        {
            return body;
        }
    }

    /// <summary>
    /// Builds the absolute download URL for an attachment, resolved against
    /// this same client's configured <see cref="HttpClient.BaseAddress"/> -
    /// the one place the API's base address is configured (see
    /// SmartX.Client/Program.cs) - rather than a second, independently
    /// hardcoded URL. Needed because this is rendered as a plain <c>&lt;a
    /// href&gt;</c> for the browser to follow directly, not an HttpClient call.
    /// </summary>
    public Uri BuildAttachmentDownloadUrl(string macAddress, Guid attachmentId)
        => new(http.BaseAddress!, $"api/sensors/{Uri.EscapeDataString(macAddress)}/attachments/{attachmentId}/download");
}
