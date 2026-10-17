namespace SmartX.Shared.Realtime;

/// <summary>
/// Each device's historical readings in a
/// <see cref="SortedDictionary{TKey,TValue}"/> keyed by timestamp, so the
/// timeline chart always gets them in time order even when packets arrive
/// out of order (a priority packet processed before older FIFO ones).
/// SortedDictionary is a red-black tree: insert, remove and min lookup are
/// O(log n) and in-order enumeration is O(n).
///
/// Memory is bounded: once a device holds more than <see cref="CapacityPerDevice"/>
/// readings the oldest (smallest key) is removed. Two readings with the
/// exact same timestamp are kept by nudging the later one forward one tick.
///
/// One lock guards the outer per-device dictionary and every inner tree.
/// </summary>
public sealed class ReadingHistoryStore<T>(int capacityPerDevice = 200)
{
    private readonly Dictionary<string, SortedDictionary<DateTime, T>> _byDevice = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _gate = new();

    public int CapacityPerDevice { get; } = capacityPerDevice > 0 ? capacityPerDevice : throw new ArgumentOutOfRangeException(nameof(capacityPerDevice));

    public void Add(string mac, DateTime timestampUtc, T reading)
    {
        lock (_gate)
        {
            if (!_byDevice.TryGetValue(mac, out var history))
            {
                history = new SortedDictionary<DateTime, T>();
                _byDevice[mac] = history;
            }

            var key = timestampUtc;
            while (history.ContainsKey(key))
            {
                key = key.AddTicks(1);
            }

            history[key] = reading;

            while (history.Count > CapacityPerDevice)
            {
                history.Remove(history.Keys.First());
            }
        }
    }

    public int CountFor(string mac)
    {
        lock (_gate)
        {
            return _byDevice.TryGetValue(mac, out var history) ? history.Count : 0;
        }
    }

    /// <summary>The newest <paramref name="take"/> readings for a device, oldest first.</summary>
    public List<KeyValuePair<DateTime, T>> Get(string mac, int take = int.MaxValue)
    {
        lock (_gate)
        {
            if (!_byDevice.TryGetValue(mac, out var history))
            {
                return [];
            }

            return history.Skip(Math.Max(0, history.Count - take)).ToList();
        }
    }
}
