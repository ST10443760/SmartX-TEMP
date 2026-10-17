using System.Net;
using System.Net.Http.Json;
using SmartX.Shared.Dtos;
using SmartX.Shared.Enums;
using SmartX.Shared.Models;

namespace SmartX.Simulator;

/// <summary>
/// Registers a fleet through the same POST /api/sensors endpoint the Part 1
/// form uses, then POSTs one telemetry packet per device to
/// /api/telemetry/ingest every interval - real HTTP, exactly like hardware.
/// </summary>
public sealed class FleetSimulator(HttpClient http, SimulatorOptions options)
{
    private static readonly string[][] DeploymentPaths =
    [
        ["Smart-X Root", "Facility A", "Zone 1", "Sub-Zone B", "Node 12"],
        ["Smart-X Root", "Facility A", "Zone 1", "Sub-Zone B", "Node 13"],
        ["Smart-X Root", "Facility A", "Zone 2", "Sub-Zone A", "Node 21"],
        ["Smart-X Root", "Facility B", "Utility Room", "Node 44"]
    ];

    private readonly List<SimulatedDevice> _fleet = [];
    private readonly Lock _gate = new();
    private bool _spikeRequested;
    private string? _pendingDisconnect;
    private int _round;

    public const int DisconnectAfterRounds = 3;

    public IReadOnlyList<SimulatedDevice> Fleet => _fleet;

    public void RequestSpike()
    {
        lock (_gate)
        {
            _spikeRequested = true;
        }
    }

    public SimulatedDevice? DisconnectRandom()
    {
        var candidates = _fleet.Where(d => !d.IsDisconnected && d.Category != SensorCategory.Actuator).ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        var device = candidates[Random.Shared.Next(candidates.Count)];
        device.IsDisconnected = true;
        return device;
    }

    public SimulatedDevice? Disconnect(string macOrName)
    {
        var device = _fleet.FirstOrDefault(d => string.Equals(d.Mac, macOrName, StringComparison.OrdinalIgnoreCase)
                                             || string.Equals(d.Name, macOrName, StringComparison.OrdinalIgnoreCase));
        if (device is not null)
        {
            device.IsDisconnected = true;
        }

        return device;
    }

    public int ReconnectAll()
    {
        var count = _fleet.Count(d => d.IsDisconnected);
        _fleet.ForEach(d => d.IsDisconnected = false);
        return count;
    }

    public async Task RegisterFleetAsync(CancellationToken ct)
    {
        for (var i = 1; i <= options.DeviceCount; i++)
        {
            var (category, kind, label) = (i % 3) switch
            {
                1 => (SensorCategory.Environmental, (ActuatorKind?)null, "Sim Moisture"),
                2 => (SensorCategory.PowerConsumption, null, "Sim Power Meter"),
                _ => (SensorCategory.Actuator, ActuatorKind.Valve, "Sim Valve")
            };

            var request = new SensorRegistrationRequest
            {
                MacAddress = $"5C:CF:7F:00:00:{i:X2}",
                DisplayName = $"{label} {i:00}",
                Category = category,
                ActuatorKind = kind,
                DeploymentPathSegments = [.. DeploymentPaths[i % DeploymentPaths.Length]]
            };

            var response = await http.PostAsJsonAsync("api/sensors", request, ct);
            var outcome = response.StatusCode switch
            {
                HttpStatusCode.Created => "registered",
                HttpStatusCode.Conflict => "already registered",
                _ => $"FAILED ({(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)})"
            };
            Console.WriteLine($"  {request.DisplayName,-18} {request.MacAddress}  {outcome}");
        }

        var registered = await http.GetFromJsonAsync<List<SensorRecord>>("api/sensors", ct) ?? [];
        foreach (var sensor in registered)
        {
            var isOurs = sensor.MacAddress.StartsWith("5C:CF:7F:", StringComparison.OrdinalIgnoreCase);
            if (isOurs || options.IncludeSeeded)
            {
                _fleet.Add(new SimulatedDevice(sensor.MacAddress, sensor.DisplayName, sensor.Category));
            }
        }

        if (options.DisconnectTarget is { } target)
        {
            // Not silenced straight away: the gateway only watches nodes it has
            // heard from, so the device reports for a few rounds first.
            _pendingDisconnect = target;
            Console.WriteLine($"  '{target}' will go silent after round {DisconnectAfterRounds}");
        }
    }

    /// <summary>Sends one round of telemetry. Returns (sent, priority-lane count, failures).</summary>
    public async Task<(int Sent, int Priority, int Failed)> SendRoundAsync(CancellationToken ct)
    {
        bool spike;
        lock (_gate)
        {
            spike = _spikeRequested;
            _spikeRequested = false;
        }

        // Actuators mirror whatever state the gateway last commanded.
        if (++_round == DisconnectAfterRounds + 1 && _pendingDisconnect is { } pending)
        {
            var silenced = Disconnect(pending);
            Console.WriteLine(silenced is null ? $"  --disconnect: no device called '{pending}'" : $"  >> {silenced} stopped sending - expect ONE disconnect alert after the gateway timeout");
        }

        var actuators = await TryGetActuatorsAsync(ct);
        var states = actuators.ToDictionary(a => a.MacAddress, a => a.IsOn, StringComparer.OrdinalIgnoreCase);
        var anyPumpOn = actuators.Any(a => a.Kind == ActuatorKind.Pump && a.IsOn);

        var spikeTarget = spike
            ? _fleet.Where(d => !d.IsDisconnected && d.Category != SensorCategory.Actuator).OrderBy(_ => Random.Shared.Next()).FirstOrDefault()
            : null;

        var sends = _fleet
            .Where(d => !d.IsDisconnected)
            .Select(d => SendAsync(d.NextReading(anyPumpOn, states, d == spikeTarget), ct))
            .ToList();

        var results = await Task.WhenAll(sends);

        if (spikeTarget is not null)
        {
            Console.WriteLine($"  >> critical spike sent for {spikeTarget}");
        }

        return (results.Count(r => r.Ok), results.Count(r => r.Lane == "Priority"), results.Count(r => !r.Ok));
    }

    private async Task<(bool Ok, string? Lane)> SendAsync(TelemetryIngestRequest request, CancellationToken ct)
    {
        try
        {
            var response = await http.PostAsJsonAsync("api/telemetry/ingest", request, ct);
            if (response.StatusCode != HttpStatusCode.Accepted)
            {
                return (false, null);
            }

            var accepted = await response.Content.ReadFromJsonAsync<IngestAccepted>(ct);
            return (true, accepted?.Lane);
        }
        catch (HttpRequestException)
        {
            return (false, null);
        }
    }

    private async Task<List<ActuatorState>> TryGetActuatorsAsync(CancellationToken ct)
    {
        try
        {
            return await http.GetFromJsonAsync<List<ActuatorState>>("api/commands/actuators", ct) ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }
}
