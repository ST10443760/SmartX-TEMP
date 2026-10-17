using System.Globalization;
using SmartX.Shared.Dtos;
using SmartX.Shared.Enums;

namespace SmartX.Shared.Insights;

/// <summary>One thing the engine observed: a search, a manual command, or a device condition.</summary>
public sealed record ActivityEntry(
    ActivityKind Kind,
    string Label,
    DateTime TimestampUtc,
    OverrideCommandType? CommandType = null,
    string? TargetMac = null)
{
    /// <summary>Case-insensitive identity used as the dictionary key, e.g. "search:pump a".</summary>
    public string Key => $"{Kind}:{Label}".ToLowerInvariant();
}

/// <summary>
/// The predictive half of the Automated Action Engine. It learns "what tends
/// to follow what" from a bounded history instead of a hardcoded list.
///
/// How it works, in one paragraph: every search, manual command and device
/// condition is recorded with a timestamp in a bounded queue. When an
/// operator action (search or command) happens within <see cref="FollowWindow"/>
/// of the previous operator action, the pair is counted in a transition
/// table - a <c>Dictionary&lt;string, Dictionary&lt;string, int&gt;&gt;</c> that
/// reads "after X, the user did Y, N times" - together with the total gap in
/// seconds. Device conditions (e.g. "Moisture Sensor B below 20%") are
/// counted the same way against the next operator action, so the engine also
/// learns "after this condition, the operator usually does Y". To suggest,
/// it looks up the row for the latest action (or active condition), ranks the
/// followers by count, and turns count / row total into a confidence with an
/// average gap for the explanation. When an old entry falls out of the
/// bounded history, the transitions it created are subtracted again, so the
/// model always reflects only the remembered window.
///
/// Not thread-safe; the API wraps it in a lock.
/// </summary>
public sealed class SuggestionEngine
{
    private readonly Queue<RecordedEntry> _history = new();
    private readonly Dictionary<string, Dictionary<string, int>> _transitions = [];
    private readonly Dictionary<string, Dictionary<string, double>> _gapSeconds = [];
    private readonly Dictionary<string, ActivityEntry> _latestByKey = [];

    private ActivityEntry? _lastAction;
    private ActivityEntry? _lastCondition;

    public SuggestionEngine(int historyCapacity = 500, TimeSpan? followWindow = null, int minimumOccurrences = 2)
    {
        HistoryCapacity = historyCapacity > 0 ? historyCapacity : throw new ArgumentOutOfRangeException(nameof(historyCapacity));
        FollowWindow = followWindow ?? TimeSpan.FromMinutes(5);
        MinimumOccurrences = minimumOccurrences;
    }

    public int HistoryCapacity { get; }

    /// <summary>Two actions further apart than this are treated as unrelated.</summary>
    public TimeSpan FollowWindow { get; }

    /// <summary>A pattern must be seen at least this often before it is suggested.</summary>
    public int MinimumOccurrences { get; }

    public int HistorySize => _history.Count;

    public ActivityEntry? LastAction => _lastAction;

    /// <summary>Number of distinct "X then Y" pairs currently in the model.</summary>
    public int PatternCount => _transitions.Values.Sum(row => row.Count);

    public IReadOnlyList<ActivityEntry> History => _history.Select(r => r.Entry).ToList();

    public void Record(ActivityEntry entry)
    {
        var recorded = new RecordedEntry(entry);

        if (entry.Kind == ActivityKind.Condition)
        {
            _lastCondition = entry;
        }
        else
        {
            if (IsFollowUp(_lastAction, entry))
            {
                Count(recorded, _lastAction!, entry);
            }

            if (IsFollowUp(_lastCondition, entry))
            {
                Count(recorded, _lastCondition!, entry);

                // A condition explains only the first action taken after it.
                _lastCondition = null;
            }

            _lastAction = entry;
            _latestByKey[entry.Key] = entry;
        }

        _history.Enqueue(recorded);
        while (_history.Count > HistoryCapacity)
        {
            Forget(_history.Dequeue());
        }
    }

    /// <summary>Likely next actions after <paramref name="trigger"/>, best first.</summary>
    public List<SuggestedAction> SuggestAfter(ActivityEntry trigger, int max = 3)
    {
        if (!_transitions.TryGetValue(trigger.Key, out var row))
        {
            return [];
        }

        var total = row.Values.Sum();

        return row
            .Where(kv => kv.Value >= MinimumOccurrences && _latestByKey.ContainsKey(kv.Key))
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Take(max)
            .Select(kv =>
            {
                var next = _latestByKey[kv.Key];
                var averageGap = TimeSpan.FromSeconds(_gapSeconds[trigger.Key][kv.Key] / kv.Value);
                return new SuggestedAction
                {
                    Kind = next.Kind,
                    Label = next.Label,
                    CommandType = next.CommandType,
                    TargetMac = next.TargetMac,
                    Occurrences = kv.Value,
                    Confidence = (double)kv.Value / total,
                    Reason = Explain(trigger, next, kv.Value, total, averageGap)
                };
            })
            .ToList();
    }

    /// <summary>Likely next actions after the most recent operator action.</summary>
    public List<SuggestedAction> SuggestNext(int max = 3)
        => _lastAction is null ? [] : SuggestAfter(_lastAction, max);

    private bool IsFollowUp(ActivityEntry? previous, ActivityEntry next)
        => previous is not null
           && previous.Key != next.Key
           && next.TimestampUtc >= previous.TimestampUtc
           && next.TimestampUtc - previous.TimestampUtc <= FollowWindow;

    private void Count(RecordedEntry recorded, ActivityEntry from, ActivityEntry to)
    {
        if (!_transitions.TryGetValue(from.Key, out var row))
        {
            row = [];
            _transitions[from.Key] = row;
            _gapSeconds[from.Key] = [];
        }

        var gap = (to.TimestampUtc - from.TimestampUtc).TotalSeconds;
        row[to.Key] = row.GetValueOrDefault(to.Key) + 1;
        _gapSeconds[from.Key][to.Key] = _gapSeconds[from.Key].GetValueOrDefault(to.Key) + gap;

        recorded.Contributions.Add((from.Key, to.Key, gap));
    }

    /// <summary>Removes the transitions an evicted entry added, so the model matches the bounded history.</summary>
    private void Forget(RecordedEntry evicted)
    {
        foreach (var (fromKey, toKey, gap) in evicted.Contributions)
        {
            var row = _transitions[fromKey];
            row[toKey]--;
            _gapSeconds[fromKey][toKey] -= gap;

            if (row[toKey] <= 0)
            {
                row.Remove(toKey);
                _gapSeconds[fromKey].Remove(toKey);
            }

            if (row.Count == 0)
            {
                _transitions.Remove(fromKey);
                _gapSeconds.Remove(fromKey);
            }
        }
    }

    private static string Explain(ActivityEntry trigger, ActivityEntry next, int count, int total, TimeSpan averageGap)
    {
        var within = averageGap.TotalSeconds < 60
            ? "within a minute"
            : string.Create(CultureInfo.InvariantCulture, $"about {Math.Round(averageGap.TotalMinutes, 1)} min later");
        var what = next.Kind == ActivityKind.Search ? $"searched for {next.Label}" : $"ran \"{next.Label}\"";

        return trigger.Kind switch
        {
            ActivityKind.Condition => $"When {trigger.Label}, operators {what} {within} ({count} of {total} times).",
            ActivityKind.Search => $"After searching {trigger.Label}, operators {what} {within} ({count} of {total} times).",
            _ => $"After \"{trigger.Label}\", operators {what} {within} ({count} of {total} times)."
        };
    }

    private sealed class RecordedEntry(ActivityEntry entry)
    {
        public ActivityEntry Entry { get; } = entry;

        /// <summary>The (from, to, gap) transitions this entry created when it arrived.</summary>
        public List<(string From, string To, double Gap)> Contributions { get; } = [];
    }
}
