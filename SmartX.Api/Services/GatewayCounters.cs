using SmartX.Shared.Dtos;
using SmartX.Shared.Realtime;

namespace SmartX.Api.Services;

/// <summary>
/// Running totals for the processing engine, shown on the dashboard so the
/// priority bypass and alert de-duplication are visible during a demo.
/// Interlocked increments - no lock needed for plain counters.
/// </summary>
public sealed class GatewayCounters(IngestionPipeline pipeline)
{
    private long _processed;
    private long _priorityProcessed;
    private long _rejectedUnknown;
    private long _duplicatesSuppressed;

    public long NextProcessedSequence(bool viaPriority)
    {
        if (viaPriority)
        {
            Interlocked.Increment(ref _priorityProcessed);
        }

        return Interlocked.Increment(ref _processed);
    }

    public void RejectedUnknown() => Interlocked.Increment(ref _rejectedUnknown);

    public void DuplicateSuppressed() => Interlocked.Increment(ref _duplicatesSuppressed);

    public QueueStats Snapshot() => new()
    {
        FifoDepth = pipeline.FifoDepth,
        PriorityDepth = pipeline.PriorityDepth,
        ProcessedTotal = Interlocked.Read(ref _processed),
        PriorityProcessed = Interlocked.Read(ref _priorityProcessed),
        RejectedUnknown = Interlocked.Read(ref _rejectedUnknown),
        DuplicateAlertsSuppressed = Interlocked.Read(ref _duplicatesSuppressed)
    };
}
