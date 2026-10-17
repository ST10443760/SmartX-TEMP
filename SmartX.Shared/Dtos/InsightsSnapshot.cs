namespace SmartX.Shared.Dtos;

/// <summary>Everything the "Suggested Actions" panel shows, pushed over SignalR whenever it changes.</summary>
public sealed class InsightsSnapshot
{
    /// <summary>The most recent operator action the next-action chips are based on.</summary>
    public string? BasedOn { get; init; }

    public List<SuggestedAction> NextActions { get; init; } = [];

    /// <summary>Actions suggested because a device condition is currently active (e.g. moisture low).</summary>
    public List<SuggestedAction> ConditionActions { get; init; } = [];

    public List<DeviceInsight> ProblemDevices { get; init; } = [];

    public int HistorySize { get; init; }

    public int PatternsLearned { get; init; }

    public DateTime GeneratedUtc { get; init; } = DateTime.UtcNow;
}
