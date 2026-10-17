using SmartX.Shared.Enums;

namespace SmartX.Shared.Realtime;

/// <summary>One unique error state: a node plus the kind of problem it has.</summary>
public readonly record struct AlertKey(string MacAddress, AlertKind Kind)
{
    public static AlertKey Of(string mac, AlertKind kind) => new(mac.ToUpperInvariant(), kind);
}

/// <summary>
/// The currently active unique error states (critical readings and
/// disconnected nodes) in a <see cref="HashSet{T}"/>. Set semantics are the
/// whole point: <see cref="HashSet{T}.Add"/> returns false for a node that is
/// already in the set, which is how a second identical alert is suppressed
/// instead of being raised again. When a node recovers it is removed, and
/// the true return from Remove is the signal to raise a "recovered" event.
///
/// Add/Remove/Contains are O(1). A private lock makes it safe to share
/// between the worker, the disconnect monitor and the endpoints.
/// </summary>
public sealed class ActiveAlertSet
{
    private readonly HashSet<AlertKey> _active = [];
    private readonly Dictionary<AlertKey, DateTime> _since = [];
    private readonly Lock _gate = new();

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _active.Count;
            }
        }
    }

    /// <summary>True if this is a NEW alert; false if the node already had this alert (duplicate).</summary>
    public bool TryRaise(string mac, AlertKind kind, DateTime nowUtc)
    {
        var key = AlertKey.Of(mac, kind);
        lock (_gate)
        {
            if (!_active.Add(key))
            {
                return false;
            }

            _since[key] = nowUtc;
            return true;
        }
    }

    /// <summary>True if the node WAS in the set and has now been cleared (raise "recovered").</summary>
    public bool TryRecover(string mac, AlertKind kind)
    {
        var key = AlertKey.Of(mac, kind);
        lock (_gate)
        {
            _since.Remove(key);
            return _active.Remove(key);
        }
    }

    public bool Contains(string mac, AlertKind kind)
    {
        lock (_gate)
        {
            return _active.Contains(AlertKey.Of(mac, kind));
        }
    }

    public bool HasAny(string mac)
    {
        lock (_gate)
        {
            return _active.Contains(AlertKey.Of(mac, AlertKind.CriticalReading))
                || _active.Contains(AlertKey.Of(mac, AlertKind.Disconnected));
        }
    }

    public List<(AlertKey Key, DateTime SinceUtc)> Snapshot()
    {
        lock (_gate)
        {
            return _active.Select(k => (k, _since.GetValueOrDefault(k))).OrderBy(e => e.Item2).ToList();
        }
    }

    public HashSet<string> DisconnectedNodes()
    {
        lock (_gate)
        {
            return _active.Where(k => k.Kind == AlertKind.Disconnected).Select(k => k.MacAddress).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
    }
}
