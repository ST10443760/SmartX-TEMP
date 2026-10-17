using Microsoft.AspNetCore.SignalR;
using SmartX.Api.Hubs;
using SmartX.Shared.Dtos;
using SmartX.Shared.Enums;
using SmartX.Shared.Insights;
using SmartX.Shared.Realtime;

namespace SmartX.Api.Services;

/// <summary>
/// The API side of the Automated Action Engine. Feeds the
/// <see cref="SuggestionEngine"/> with every search, every manual command and
/// every device condition (moisture dropping below 20%, power climbing above
/// 2200W), builds the "Suggested Actions" snapshot by combining the engine's
/// predictions with the <see cref="DeviceHealthAnalyzer"/>'s per-device
/// checks, and pushes it to every dashboard over SignalR.
///
/// The engine is not thread-safe, so every call goes through one lock.
/// </summary>
public sealed class InsightsService(
    DeviceDirectory directory,
    ReadingHistoryStore<LiveReading> history,
    ActiveAlertSet alerts,
    IHubContext<TelemetryHub> hub)
{
    private readonly SuggestionEngine _engine = new(historyCapacity: 500, followWindow: TimeSpan.FromMinutes(5));
    private readonly Lock _gate = new();

    // Devices currently past a warning threshold, so a condition is recorded
    // once when it starts (edge-triggered), not on every reading.
    private readonly Dictionary<string, ActivityEntry> _activeConditions = new(StringComparer.OrdinalIgnoreCase);

    public void Seed(IEnumerable<ActivityEntry> entries)
    {
        lock (_gate)
        {
            foreach (var entry in entries.OrderBy(e => e.TimestampUtc))
            {
                _engine.Record(entry);
            }
        }
    }

    /// <summary>Records a completed search. A term that names exactly one device is normalised to its display name.</summary>
    public Task RecordSearchAsync(string searchText)
    {
        var term = searchText.Trim();
        var matches = directory.Snapshot()
            .Where(d => d.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase) || d.MacAddress.Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var label = matches.Count == 1 ? matches[0].DisplayName : term;

        lock (_gate)
        {
            _engine.Record(new ActivityEntry(ActivityKind.Search, label, DateTime.UtcNow));
        }

        return PublishAsync();
    }

    public Task RecordCommandAsync(OverrideCommandRequest request)
    {
        var name = request.TargetMac is not null && directory.TryGet(request.TargetMac, out var device) ? device!.DisplayName : "";
        var label = CommandLabel(request.CommandType, name);

        lock (_gate)
        {
            _engine.Record(new ActivityEntry(ActivityKind.Command, label, DateTime.UtcNow, request.CommandType, request.TargetMac));
        }

        return PublishAsync();
    }

    /// <summary>Called by the worker for every processed reading; records a condition when a device first crosses a threshold.</summary>
    public void ObserveReading(LiveReading reading)
    {
        var condition = reading.Kind switch
        {
            ReadingKind.Moisture when reading.Value < SeverityClassifier.MoistureWarningBelow => $"{reading.DeviceName} drops below {SeverityClassifier.MoistureWarningBelow}%",
            ReadingKind.Wattage when reading.Value > SeverityClassifier.WattsWarningAbove => $"{reading.DeviceName} draws over {SeverityClassifier.WattsWarningAbove}W",
            _ => null
        };

        lock (_gate)
        {
            if (condition is null)
            {
                _activeConditions.Remove(reading.MacAddress);
                return;
            }

            if (_activeConditions.ContainsKey(reading.MacAddress))
            {
                return;
            }

            var entry = new ActivityEntry(ActivityKind.Condition, condition, DateTime.UtcNow);
            _activeConditions[reading.MacAddress] = entry;
            _engine.Record(entry);
        }
    }

    public InsightsSnapshot BuildSnapshot()
    {
        List<SuggestedAction> next;
        List<SuggestedAction> fromConditions;
        string? basedOn;
        int historySize, patterns;

        lock (_gate)
        {
            next = _engine.SuggestNext();
            basedOn = _engine.LastAction is { } last ? (last.Kind == ActivityKind.Search ? $"search \"{last.Label}\"" : $"command \"{last.Label}\"") : null;
            fromConditions = _activeConditions.Values
                .SelectMany(condition => _engine.SuggestAfter(condition, max: 1))
                .ToList();
            historySize = _engine.HistorySize;
            patterns = _engine.PatternCount;
        }

        var disconnected = alerts.DisconnectedNodes();
        var problems = directory.Snapshot()
            .Select(d => DeviceHealthAnalyzer.Analyze(
                d.MacAddress,
                d.DisplayName,
                d.Category,
                history.Get(d.MacAddress, DeviceHealthAnalyzer.TrendWindow).Select(kv => kv.Value.Value).ToList(),
                alerts.Contains(d.MacAddress, AlertKind.CriticalReading),
                disconnected.Contains(d.MacAddress)))
            .OfType<DeviceInsight>()
            .OrderByDescending(i => i.Severity)
            .ThenBy(i => i.Kind)
            .ToList();

        return new InsightsSnapshot
        {
            BasedOn = basedOn,
            NextActions = next,
            ConditionActions = fromConditions,
            ProblemDevices = problems,
            HistorySize = historySize,
            PatternsLearned = patterns
        };
    }

    public Task PublishAsync(CancellationToken ct = default)
        => hub.Clients.All.SendAsync(HubEvents.Insights, BuildSnapshot(), ct);

    public static string CommandLabel(OverrideCommandType type, string deviceName) => type switch
    {
        OverrideCommandType.TriggerPump => $"Trigger {deviceName}",
        OverrideCommandType.StopPump => $"Stop {deviceName}",
        OverrideCommandType.ToggleRelay => $"Toggle {deviceName}",
        OverrideCommandType.OpenValve => $"Open {deviceName}",
        OverrideCommandType.CloseAllValves => "Close all valves",
        _ => type.ToString()
    };
}
