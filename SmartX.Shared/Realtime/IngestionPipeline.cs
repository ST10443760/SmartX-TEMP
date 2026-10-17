using SmartX.Shared.Enums;

namespace SmartX.Shared.Realtime;

/// <summary>Which queue a packet went through.</summary>
public enum IngestionLane
{
    Fifo = 0,
    Priority = 1
}

/// <summary>
/// The two-lane front door of the processing engine. Routine packets go into
/// the FIFO <see cref="PacketQueue{T}"/>; anything classified Warning or
/// Critical bypasses it and goes into the <see cref="CriticalAlertQueue{T}"/>.
/// <see cref="TryTakeNext"/> always drains the priority queue completely
/// before it touches the FIFO queue, so a critical spike that arrives behind
/// a hundred routine packets is still processed first.
/// </summary>
public sealed class IngestionPipeline
{
    private readonly PacketQueue<QueuedReading> _fifo = new();
    private readonly CriticalAlertQueue<QueuedReading> _priority = new();

    // Held only while submitting a batch or taking the next item, so a batch
    // lands in the queues atomically relative to the worker.
    private readonly Lock _gate = new();

    public int FifoDepth => _fifo.Count;

    public int PriorityDepth => _priority.Count;

    public IngestionLane Submit(QueuedReading reading)
    {
        lock (_gate)
        {
            return SubmitUnlocked(reading);
        }
    }

    /// <summary>Submits several readings with no chance of the worker running in between.</summary>
    public void SubmitBatch(IEnumerable<QueuedReading> readings)
    {
        lock (_gate)
        {
            foreach (var reading in readings)
            {
                SubmitUnlocked(reading);
            }
        }
    }

    public bool TryTakeNext(out QueuedReading reading, out IngestionLane lane)
    {
        lock (_gate)
        {
            if (_priority.TryDequeue(out reading, out _))
            {
                lane = IngestionLane.Priority;
                return true;
            }

            lane = IngestionLane.Fifo;
            return _fifo.TryDequeue(out reading);
        }
    }

    private IngestionLane SubmitUnlocked(QueuedReading reading)
    {
        if (reading.Severity >= AlertSeverity.Warning)
        {
            _priority.Enqueue(reading, reading.Severity);
            return IngestionLane.Priority;
        }

        _fifo.Enqueue(reading);
        return IngestionLane.Fifo;
    }
}
