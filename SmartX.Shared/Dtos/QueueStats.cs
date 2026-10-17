namespace SmartX.Shared.Dtos;

/// <summary>Live counters for the ingestion queues, pushed after every worker drain.</summary>
public sealed class QueueStats
{
    public int FifoDepth { get; init; }

    public int PriorityDepth { get; init; }

    public long ProcessedTotal { get; init; }

    public long PriorityProcessed { get; init; }

    public long RejectedUnknown { get; init; }

    public long DuplicateAlertsSuppressed { get; init; }
}
