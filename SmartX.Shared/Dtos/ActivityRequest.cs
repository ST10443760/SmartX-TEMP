namespace SmartX.Shared.Dtos;

/// <summary>Posted by the dashboard when the operator completes a search, so the engine can learn from it.</summary>
public sealed class ActivityRequest
{
    public required string SearchText { get; init; }
}
