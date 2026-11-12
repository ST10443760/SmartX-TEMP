namespace SmartX.Shared.Trees;

/// <summary>
/// What an <see cref="AvlTree{TValue}"/> range query found, plus the numbers
/// that show how efficient it was: how many tree nodes it had to touch
/// compared with how many readings the tree holds in total.
/// </summary>
public sealed record RangeQueryResult<TValue>(
    IReadOnlyList<KeyValuePair<DateTime, TValue>> Items,
    int NodesVisited,
    int TotalReadings);
