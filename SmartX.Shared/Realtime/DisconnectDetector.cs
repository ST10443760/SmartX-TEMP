using SmartX.Shared.Enums;

namespace SmartX.Shared.Realtime;

/// <summary>Result of one disconnect scan.</summary>
public sealed record DisconnectScanResult(IReadOnlyList<string> NewlyDisconnected, int DuplicatesSuppressed);

/// <summary>
/// Flags a node as disconnected if it has sent nothing for
/// <see cref="Timeout"/>. It remembers when each MAC was last seen and, on
/// every <see cref="Scan"/>, tries to add each silent node to the
/// <see cref="ActiveAlertSet"/>. Because that is a set, a node that is still
/// silent on the next scan is NOT re-alerted - it is counted as a suppressed
/// duplicate. When a packet arrives from a disconnected node,
/// <see cref="RecordSeen"/> removes it and reports the recovery.
///
/// Only nodes that have reported at least once are watched, so a registered
/// device that has never been switched on is not called "disconnected".
/// Time is passed in rather than read from the clock so it is unit-testable.
/// </summary>
public sealed class DisconnectDetector(ActiveAlertSet alerts, TimeSpan timeout)
{
    private readonly Dictionary<string, DateTime> _lastSeen = new(StringComparer.OrdinalIgnoreCase);

    // When each silent node was last alerted or counted as a duplicate. A
    // detector without the set would re-alert once per timeout period, so
    // that is how often a suppressed duplicate is counted.
    private readonly Dictionary<string, DateTime> _lastFlagged = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _gate = new();

    public TimeSpan Timeout { get; } = timeout > TimeSpan.Zero ? timeout : throw new ArgumentOutOfRangeException(nameof(timeout));

    /// <summary>Records a packet from <paramref name="mac"/>. Returns true if the node was disconnected and has now recovered.</summary>
    public bool RecordSeen(string mac, DateTime nowUtc)
    {
        lock (_gate)
        {
            _lastSeen[mac] = nowUtc;
            _lastFlagged.Remove(mac);
        }

        return alerts.TryRecover(mac, AlertKind.Disconnected);
    }

    public DisconnectScanResult Scan(DateTime nowUtc)
    {
        var newlyDisconnected = new List<string>();
        var duplicates = 0;

        List<KeyValuePair<string, DateTime>> watched;
        lock (_gate)
        {
            watched = [.. _lastSeen];
        }

        foreach (var (mac, lastSeen) in watched)
        {
            if (nowUtc - lastSeen < Timeout)
            {
                continue;
            }

            if (alerts.TryRaise(mac, AlertKind.Disconnected, nowUtc))
            {
                newlyDisconnected.Add(mac);
                MarkFlagged(mac, nowUtc);
            }
            else if (DuePeriod(mac, nowUtc))
            {
                duplicates++;
                MarkFlagged(mac, nowUtc);
            }
        }

        return new DisconnectScanResult(newlyDisconnected, duplicates);
    }

    /// <summary>
    /// Demo hook: marks a node disconnected right now. Returns true if that
    /// raised a new alert, false if the node was already in the set (duplicate).
    /// </summary>
    public bool ForceDisconnect(string mac, DateTime nowUtc)
    {
        lock (_gate)
        {
            // Back-date last-seen so the next scan agrees the node is silent.
            _lastSeen[mac] = nowUtc - Timeout;
        }

        var isNew = alerts.TryRaise(mac, AlertKind.Disconnected, nowUtc);
        MarkFlagged(mac, nowUtc);
        return isNew;
    }

    private bool DuePeriod(string mac, DateTime nowUtc)
    {
        lock (_gate)
        {
            return !_lastFlagged.TryGetValue(mac, out var flagged) || nowUtc - flagged >= Timeout;
        }
    }

    private void MarkFlagged(string mac, DateTime nowUtc)
    {
        lock (_gate)
        {
            _lastFlagged[mac] = nowUtc;
        }
    }

    public DateTime? LastSeen(string mac)
    {
        lock (_gate)
        {
            return _lastSeen.TryGetValue(mac, out var seen) ? seen : null;
        }
    }
}
