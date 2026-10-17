using SmartX.Shared.Dtos;

namespace SmartX.Api.Services;

/// <summary>
/// The last N processed readings across all devices, so a dashboard that
/// (re)connects can show the live feed straight away instead of an empty list.
/// A bounded <see cref="Queue{T}"/>: enqueue at the back, drop from the front.
/// </summary>
public sealed class RecentReadingFeed
{
    public const int Capacity = 60;

    private readonly Queue<LiveReading> _recent = new();
    private readonly Lock _gate = new();

    public void Add(LiveReading reading)
    {
        lock (_gate)
        {
            _recent.Enqueue(reading);
            while (_recent.Count > Capacity)
            {
                _recent.Dequeue();
            }
        }
    }

    /// <summary>Newest first.</summary>
    public List<LiveReading> Snapshot()
    {
        lock (_gate)
        {
            return _recent.Reverse().ToList();
        }
    }
}
