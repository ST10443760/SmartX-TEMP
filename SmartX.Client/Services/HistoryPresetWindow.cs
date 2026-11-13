namespace SmartX.Client.Services;

/// <summary>
/// The time window for the historical log's presets ("Last 15 min", "Last
/// hour", "Last 4 hours") and the default range used when a node is selected.
///
/// Normally a preset ends at the current minute. But the seeded history is
/// generated once when the API starts, and without the simulator nothing is
/// added afterwards. Once the app has been open longer than the preset, a
/// window ending "now" lies entirely after the newest stored reading and
/// returns nothing. In that case the window is anchored to the newest stored
/// reading instead, and the card says so. A custom range picked by hand is
/// never anchored; it is used exactly as picked.
/// </summary>
public static class HistoryPresetWindow
{
    /// <summary>The window ending at the current minute (inclusive of readings in that minute).</summary>
    public static (DateTime From, DateTime To) Relative(DateTime nowLocal, int minutes)
    {
        var to = Trim(nowLocal).AddMinutes(1);
        return (to.AddMinutes(-minutes), to);
    }

    /// <summary>
    /// True when the device's newest stored reading is older than the start of
    /// the window, so the window cannot contain any reading at all.
    /// </summary>
    public static bool ShouldAnchor(DateTime? newestLocal, DateTime windowFrom) => newestLocal is { } newest && newest < windowFrom;

    /// <summary>The same length of window, ending just after the newest stored reading.</summary>
    public static (DateTime From, DateTime To) AnchoredTo(DateTime newestLocal, int minutes)
    {
        var to = Trim(newestLocal).AddMinutes(1);
        return (to.AddMinutes(-minutes), to);
    }

    /// <summary>The note shown on the card when a preset was anchored.</summary>
    public static string Note(DateTime newestLocal, int minutes)
        => $"No live readings since {newestLocal:HH:mm}, showing the last {Describe(minutes)} of stored data.";

    public static string Describe(int minutes) => minutes switch
    {
        60 => "hour",
        _ when minutes % 60 == 0 => $"{minutes / 60} hours",
        _ => $"{minutes} minutes"
    };

    private static DateTime Trim(DateTime value) => new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, DateTimeKind.Unspecified);
}
