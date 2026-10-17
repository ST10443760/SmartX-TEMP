using SmartX.Shared.Enums;

namespace SmartX.Shared.Realtime;

/// <summary>
/// Priority key for the critical alert queue: severity first (Critical beats
/// Warning), then an insertion counter as the tiebreaker so two alerts of
/// the same severity come out in the order they arrived.
/// </summary>
public readonly record struct AlertPriority(AlertSeverity Severity, long Sequence);

/// <summary>
/// <see cref="PriorityQueue{TElement,TPriority}"/> is a min-heap - it serves
/// the smallest priority first - so "smaller" here means "more urgent":
/// higher severity sorts first, and for equal severity the lower (earlier)
/// sequence number sorts first.
/// </summary>
public sealed class AlertPriorityComparer : IComparer<AlertPriority>
{
    public static readonly AlertPriorityComparer Instance = new();

    public int Compare(AlertPriority x, AlertPriority y)
    {
        var bySeverity = y.Severity.CompareTo(x.Severity);
        return bySeverity != 0 ? bySeverity : x.Sequence.CompareTo(y.Sequence);
    }
}
