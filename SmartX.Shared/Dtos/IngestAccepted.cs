using SmartX.Shared.Enums;

namespace SmartX.Shared.Dtos;

/// <summary>Body of the 202 Accepted response: which lane the packet went into and how deep it is.</summary>
public sealed class IngestAccepted
{
    public required string Lane { get; init; }

    public required AlertSeverity Severity { get; init; }

    public required int QueueDepth { get; init; }
}
