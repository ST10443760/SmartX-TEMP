using SmartX.Client.Services;

namespace SmartX.Tests;

public class HistoryRangeInputTests
{
    [Fact]
    public void TryParse_AcceptsTheSecondsThatBlazorAddsToDatetimeLocalValues()
    {
        // Regression: Blazor reports "…T23:00:00" for an input showing "…T23:00".
        // A minutes-only exact parse rejected it and the input was reverted.
        Assert.True(HistoryRangeInput.TryParse("2026-11-12T23:00:00", out var withSeconds));
        Assert.True(HistoryRangeInput.TryParse("2026-11-12T23:00", out var withoutSeconds));

        Assert.Equal(new DateTime(2026, 11, 12, 23, 0, 0), withSeconds);
        Assert.Equal(withSeconds, withoutSeconds);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("2026-11-")]
    public void TryParse_RejectsEmptyOrHalfTypedValues(string? text)
    {
        Assert.False(HistoryRangeInput.TryParse(text, out _));
    }

    [Fact]
    public void Normalise_DropsSeconds_AndKeepsAnIncompleteValueAsIs()
    {
        Assert.Equal("2026-11-12T23:00", HistoryRangeInput.Normalise("2026-11-12T23:00:00"));
        Assert.Equal("", HistoryRangeInput.Normalise(""));
    }

    [Fact]
    public void Format_MatchesTheInputShape_AndIsEmptyForNoValue()
    {
        Assert.Equal("2026-11-12T14:05", HistoryRangeInput.Format(new DateTime(2026, 11, 12, 14, 5, 30)));
        Assert.Equal("", HistoryRangeInput.Format(null));
    }

    [Fact]
    public void TryCommit_ValidDrafts_GiveTheRange()
    {
        Assert.True(HistoryRangeInput.TryCommit("2026-11-12T14:00", "2026-11-12T16:00:00", out var from, out var to, out var error));

        Assert.Equal(new DateTime(2026, 11, 12, 14, 0, 0), from);
        Assert.Equal(new DateTime(2026, 11, 12, 16, 0, 0), to);
        Assert.Null(error);
    }

    [Fact]
    public void TryCommit_IncompleteOrReversedDrafts_AreRejectedWithAMessage()
    {
        Assert.False(HistoryRangeInput.TryCommit("", "2026-11-12T16:00", out _, out _, out var incomplete));
        Assert.False(HistoryRangeInput.TryCommit("2026-11-12T17:00", "2026-11-12T16:00", out _, out _, out var reversed));

        Assert.Contains("complete", incomplete);
        Assert.Contains("before", reversed);
    }
}
