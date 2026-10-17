namespace SmartX.Shared.Realtime;

/// <summary>
/// Standard first-in-first-out queue for routine telemetry packets, wrapping
/// <see cref="Queue{T}"/>. The ingest endpoint enqueues and returns 202
/// straight away; the background worker dequeues in arrival order.
///
/// Thread safety: the endpoint threads and the worker share one instance,
/// so every operation takes a private lock. Each critical section is O(1),
/// so contention stays negligible.
/// </summary>
public sealed class PacketQueue<T>
{
    private readonly Queue<T> _queue = new();
    private readonly Lock _gate = new();

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _queue.Count;
            }
        }
    }

    public void Enqueue(T item)
    {
        lock (_gate)
        {
            _queue.Enqueue(item);
        }
    }

    public bool TryDequeue(out T item)
    {
        lock (_gate)
        {
            return _queue.TryDequeue(out item!);
        }
    }
}
