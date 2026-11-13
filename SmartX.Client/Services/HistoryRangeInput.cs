using System.Globalization;

namespace SmartX.Client.Services;

/// <summary>
/// Parsing and formatting for the historical log's From/To datetime-local
/// inputs, kept out of the component so it can be unit tested.
///
/// Why this exists: Blazor normalises a datetime-local input's event value to
/// include seconds ("2026-11-12T23:00:00") even when the box only shows minutes
/// ("2026-11-12T23:00"). The old <c>@bind:format="yyyy-MM-ddTHH:mm"</c> parse
/// therefore failed on every value, so Blazor skipped the setter and reverted
/// the input. That is how picked values were "reset". Here both shapes are
/// accepted, and the component only commits a draft when Run query or a preset
/// is pressed.
/// </summary>
public static class HistoryRangeInput
{
    /// <summary>The shape the input displays and the drafts are kept in.</summary>
    public const string InputFormat = "yyyy-MM-ddTHH:mm";

    private static readonly string[] AcceptedFormats = ["yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ss.fff"];

    public static string Format(DateTime? value) => value?.ToString(InputFormat, CultureInfo.InvariantCulture) ?? "";

    public static bool TryParse(string? text, out DateTime value)
        => DateTime.TryParseExact(text?.Trim(), AcceptedFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);

    /// <summary>
    /// Brings an input event's value to the displayed shape (drops seconds). A
    /// half-typed or empty value (the browser reports "") is kept as it is.
    /// </summary>
    public static string Normalise(string? raw) => TryParse(raw, out var value) ? Format(value) : raw ?? "";

    /// <summary>
    /// Turns the two drafts into a range to commit. Returns false with a
    /// message, and leaves the committed range alone, if either draft is
    /// incomplete or From is after To. Nothing is silently replaced by a default.
    /// </summary>
    public static bool TryCommit(string? fromDraft, string? toDraft, out DateTime from, out DateTime to, out string? error)
    {
        to = default;
        if (!TryParse(fromDraft, out from) || !TryParse(toDraft, out to))
        {
            error = "Pick a complete date and time for both From and To.";
            return false;
        }

        if (from > to)
        {
            error = "'From' must be before 'To'.";
            return false;
        }

        error = null;
        return true;
    }
}
