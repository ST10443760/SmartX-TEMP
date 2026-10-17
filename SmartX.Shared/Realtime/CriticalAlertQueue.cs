using SmartX.Shared.Enums;

namespace SmartX.Shared.Realtime;

/// <summary>
/// Priority queue for alert-grade packets (a severe power spike, a critical
/// moisture drop). Wraps <see cref="PriorityQueue{TElement,TPriority}"/>
/// with <see cref="AlertPriority"/> as the key: severity is the priority and
/// a monotonically increasing insertion counter breaks ties, so ordering is
/// deterministic (a plain heap gives no FIFO guarantee for equal keys).
///
/// Enqueue and dequeue are O(log n). A private lock makes it safe to share
/// between the ingest endpoint and the background worker.
/// </summary>
public sealed class CriticalAlertQueue<T>
{
    private readonly PriorityQueue<T, AlertPriority> _queue = new(AlertPriorityComparer.Instance);
    private readonly Lock _gate = new();
    private long _sequence;

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

    public void Enqueue(T item, AlertSeverity severity)
    {
        lock (_gate)
        {
            _queue.Enqueue(item, new AlertPriority(severity, _sequence++));
        }
    }

    public bool TryDequeue(out T item, out AlertSeverity severity)
    {
        lock (_gate)
        {
            if (_queue.TryDequeue(out item!, out var priority))
            {
                severity = priority.Severity;
                return true;
            }

            severity = AlertSeverity.Normal;
            return false;
        }
    }
}
