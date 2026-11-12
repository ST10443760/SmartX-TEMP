namespace SmartX.Shared.Trees;

/// <summary>
/// One <see cref="AvlTree{TValue}"/> of historical telemetry per device, in a
/// <see cref="Dictionary{TKey,TValue}"/> keyed by MAC address
/// (case-insensitive), in the same style as the Part 2 stores. It is fed from
/// the same worker step that feeds <see cref="Realtime.ReadingHistoryStore{T}"/>.
/// That store still drives the Part 2 timeline chart (newest 200 points); this
/// one answers time-range questions over a much longer history.
///
/// Memory bound: each device keeps at most <see cref="CapacityPerDevice"/>
/// readings (10,000 by default, set by Gateway:TreeCapacityPerDevice). At one
/// reading every 2 seconds that is about 5.5 hours per device, which covers
/// the seeded 4-hour history plus a demo session. One processed reading is
/// roughly 200 bytes with its node, so 16 devices at the cap use about 32 MB.
/// When a device goes over the cap, its oldest reading is removed
/// (O(log n) remove-min), so the tree can never grow without limit.
///
/// Thread safety: the worker writes while the history endpoint reads, so one
/// private lock guards the dictionary and every tree in it, like the Part 2
/// structures. Each operation inside the lock is O(log n), or O(log n + k) for
/// a range query returning k readings.
/// </summary>
public sealed class DeviceHistoryTrees<TValue>(int capacityPerDevice = 10_000)
{
    private readonly Dictionary<string, AvlTree<TValue>> _byDevice = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _gate = new();

    public int CapacityPerDevice { get; } = capacityPerDevice > 0 ? capacityPerDevice : throw new ArgumentOutOfRangeException(nameof(capacityPerDevice));

    public void Add(string mac, DateTime timestampUtc, TValue reading)
    {
        lock (_gate)
        {
            if (!_byDevice.TryGetValue(mac, out var tree))
            {
                tree = new AvlTree<TValue>();
                _byDevice[mac] = tree;
            }

            tree.Insert(timestampUtc, reading);

            while (tree.Count > CapacityPerDevice)
            {
                tree.RemoveOldest();
            }
        }
    }

    public int CountFor(string mac)
    {
        lock (_gate)
        {
            return _byDevice.TryGetValue(mac, out var tree) ? tree.Count : 0;
        }
    }

    /// <summary>Oldest and newest timestamp held for a device, or null when it has no history.</summary>
    public (DateTime Oldest, DateTime Newest)? SpanFor(string mac)
    {
        lock (_gate)
        {
            if (!_byDevice.TryGetValue(mac, out var tree) || tree.Count == 0)
            {
                return null;
            }

            return (tree.MinKey!.Value, tree.MaxKey!.Value);
        }
    }

    /// <summary>
    /// Readings for one device between <paramref name="fromUtc"/> and
    /// <paramref name="toUtc"/> inclusive, with the nodes-visited count. An
    /// unknown device gives an empty result rather than an error.
    /// </summary>
    public RangeQueryResult<TValue> Query(string mac, DateTime fromUtc, DateTime toUtc)
    {
        lock (_gate)
        {
            return _byDevice.TryGetValue(mac, out var tree)
                ? tree.RangeQuery(fromUtc, toUtc)
                : new RangeQueryResult<TValue>([], 0, 0);
        }
    }
}
