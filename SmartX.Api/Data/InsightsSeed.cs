using SmartX.Shared.Enums;
using SmartX.Shared.Insights;

namespace SmartX.Api.Data;

/// <summary>
/// Simulated operator history so the action engine has patterns to suggest
/// from the moment the API starts. It is a set of short "episodes" spread over
/// the last few hours, each separated by more than the engine's 5-minute
/// follow window so episodes never bleed into each other. Every pattern
/// repeats several times (with the odd exception) so the confidence numbers
/// look like learnt behaviour rather than a hardcoded rule.
/// </summary>
public static class InsightsSeed
{
    private const string PumpA = "7C:9E:BD:33:22:06";
    private const string Relay1 = "7C:9E:BD:33:22:0A";

    public static IEnumerable<ActivityEntry> Build(DateTime nowUtc)
    {
        var episodes = new List<(int Offset, ActivityEntry Entry)[]>();
        var start = nowUtc.AddHours(-3);

        static ActivityEntry Search(string term) => new(ActivityKind.Search, term, default);
        static ActivityEntry Command(string label, OverrideCommandType type, string? mac) => new(ActivityKind.Command, label, default, type, mac);
        static ActivityEntry Condition(string label) => new(ActivityKind.Condition, label, default);

        // Moisture Sensor B dries out -> operator triggers Pump A, then stops it a few minutes later.
        for (var i = 0; i < 6; i++)
        {
            episodes.Add(
            [
                (0, Condition("Moisture Sensor B drops below 20%")),
                (35 + i * 4, Command("Trigger Pump A", OverrideCommandType.TriggerPump, PumpA)),
                (190 + i * 6, Command("Stop Pump A", OverrideCommandType.StopPump, PumpA))
            ]);
        }

        // ...once, the operator looked at the sensor first instead.
        episodes.Add([(0, Condition("Moisture Sensor B drops below 20%")), (50, Search("Moisture Sensor B"))]);

        // Searching Moisture Sensor B usually leads to checking Pump A.
        for (var i = 0; i < 5; i++)
        {
            episodes.Add([(0, Search("Moisture Sensor B")), (18 + i * 3, Search("Pump A"))]);
        }

        episodes.Add([(0, Search("Moisture Sensor B")), (25, Search("Valve 1"))]);

        // A busy power meter usually gets Relay 1 toggled to shed load.
        for (var i = 0; i < 4; i++)
        {
            episodes.Add([(0, Search("Power Meter A")), (40 + i * 5, Command("Toggle Relay 1", OverrideCommandType.ToggleRelay, Relay1))]);
        }

        // After closing every valve, operators check Valve 1 is really shut.
        for (var i = 0; i < 3; i++)
        {
            episodes.Add([(0, Command("Close all valves", OverrideCommandType.CloseAllValves, null)), (22 + i * 4, Search("Valve 1"))]);
        }

        // Interleave the episode types deterministically, 8 minutes apart, and
        // finish on a lone "Moisture Sensor B" search so the panel already has
        // a suggestion ("Pump A") the first time the dashboard opens.
        var ordered = episodes
            .Select((episode, index) => (episode, index))
            .OrderBy(e => (e.index * 7) % episodes.Count)
            .Select(e => e.episode)
            .Append([(0, Search("Moisture Sensor B"))]);

        var episodeStart = start;
        foreach (var episode in ordered)
        {
            foreach (var (offset, entry) in episode)
            {
                yield return entry with { TimestampUtc = episodeStart.AddSeconds(offset) };
            }

            episodeStart = episodeStart.AddMinutes(8);
        }
    }
}
