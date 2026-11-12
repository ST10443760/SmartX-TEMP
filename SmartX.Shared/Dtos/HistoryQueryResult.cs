namespace SmartX.Shared.Dtos;

/// <summary>
/// One page of a device's historical telemetry between two times, answered by
/// the device's AVL tree. <see cref="NodesVisited"/> next to
/// <see cref="TotalReadings"/> shows how little of the tree the query had to
/// touch.
/// </summary>
public sealed class HistoryQueryResult
{
    public required string MacAddress { get; init; }

    public required string DeviceName { get; init; }

    public required DateTime FromUtc { get; init; }

    public required DateTime ToUtc { get; init; }

    /// <summary>This page of matching readings, oldest first.</summary>
    public List<LiveReading> Items { get; init; } = [];

    public int Page { get; init; } = 1;

    public int PageSize { get; init; }

    /// <summary>How many readings fall inside the range (across all pages).</summary>
    public int MatchCount { get; init; }

    /// <summary>Tree nodes the range query touched.</summary>
    public int NodesVisited { get; init; }

    /// <summary>Readings the device's tree holds in total.</summary>
    public int TotalReadings { get; init; }

    /// <summary>Oldest and newest reading held for the device, so the UI can suggest a sensible range.</summary>
    public DateTime? OldestUtc { get; init; }

    public DateTime? NewestUtc { get; init; }

    public int PageCount => PageSize <= 0 ? 0 : (MatchCount + PageSize - 1) / PageSize;
}
