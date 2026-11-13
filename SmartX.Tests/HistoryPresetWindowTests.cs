using SmartX.Client.Services;

namespace SmartX.Tests;

public class HistoryPresetWindowTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 1, 20, 42);

    [Fact]
    public void Relative_EndsAfterTheCurrentMinute_AndHasThePresetLength()
    {
        var (from, to) = HistoryPresetWindow.Relative(Now, 15);

        Assert.Equal(new DateTime(2026, 11, 13, 1, 21, 0), to);
        Assert.Equal(TimeSpan.FromMinutes(15), to - from);
    }

    [Fact]
    public void ShouldAnchor_WhenTheNewestReadingIsBeforeTheWindow()
    {
        // Seed ended at 00:31; at 01:20 "Last 15 min" starts at 01:06.
        var (from, _) = HistoryPresetWindow.Relative(Now, 15);

        Assert.True(HistoryPresetWindow.ShouldAnchor(new DateTime(2026, 11, 13, 0, 31, 3), from));
    }

    [Fact]
    public void ShouldNotAnchor_WhenLiveReadingsReachIntoTheWindow()
    {
        var (from, _) = HistoryPresetWindow.Relative(Now, 15);

        Assert.False(HistoryPresetWindow.ShouldAnchor(new DateTime(2026, 11, 13, 1, 20, 40), from));
        Assert.False(HistoryPresetWindow.ShouldAnchor(from, from)); // a reading exactly at the start counts
    }

    [Fact]
    public void ShouldNotAnchor_WhenTheDeviceHasNoHistory()
    {
        Assert.False(HistoryPresetWindow.ShouldAnchor(null, Now));
    }

    [Fact]
    public void AnchoredTo_KeepsThePresetLength_AndIncludesTheNewestReading()
    {
        var newest = new DateTime(2026, 11, 13, 0, 31, 3);

        var (from, to) = HistoryPresetWindow.AnchoredTo(newest, 15);

        Assert.Equal(new DateTime(2026, 11, 13, 0, 32, 0), to);
        Assert.Equal(new DateTime(2026, 11, 13, 0, 17, 0), from);
        Assert.InRange(newest, from, to);
    }

    [Theory]
    [InlineData(15, "No live readings since 00:31, showing the last 15 minutes of stored data.")]
    [InlineData(60, "No live readings since 00:31, showing the last hour of stored data.")]
    [InlineData(240, "No live readings since 00:31, showing the last 4 hours of stored data.")]
    public void Note_SaysWhenLiveDataStoppedAndWhatIsShown(int minutes, string expected)
    {
        Assert.Equal(expected, HistoryPresetWindow.Note(new DateTime(2026, 11, 13, 0, 31, 3), minutes));
    }
}
