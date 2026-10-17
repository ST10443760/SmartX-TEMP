using SmartX.Shared.Enums;
using SmartX.Shared.Realtime;

namespace SmartX.Tests;

public class DisconnectDetectorTests
{
    private static readonly DateTime T0 = new(2026, 10, 17, 11, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public void Scan_NodeSilentLongerThanTimeout_IsFlaggedOnce()
    {
        var alerts = new ActiveAlertSet();
        var detector = new DisconnectDetector(alerts, Timeout);
        detector.RecordSeen("AA:01", T0);

        var result = detector.Scan(T0.AddSeconds(11));

        Assert.Equal(["AA:01"], result.NewlyDisconnected);
        Assert.True(alerts.Contains("AA:01", AlertKind.Disconnected));
    }

    [Fact]
    public void Scan_NodeStillSilent_DoesNotGenerateASecondAlert()
    {
        var alerts = new ActiveAlertSet();
        var detector = new DisconnectDetector(alerts, Timeout);
        detector.RecordSeen("AA:01", T0);
        detector.Scan(T0.AddSeconds(11));

        var second = detector.Scan(T0.AddSeconds(20));

        Assert.Empty(second.NewlyDisconnected);
        Assert.Equal(1, second.DuplicatesSuppressed);
        Assert.Equal(1, alerts.Count);
    }

    [Fact]
    public void Scan_NodeWithinTimeout_IsNotFlagged()
    {
        var detector = new DisconnectDetector(new ActiveAlertSet(), Timeout);
        detector.RecordSeen("AA:01", T0);

        Assert.Empty(detector.Scan(T0.AddSeconds(9)).NewlyDisconnected);
    }

    [Fact]
    public void RecordSeen_AfterDisconnect_RemovesTheNodeAndReportsRecovery()
    {
        var alerts = new ActiveAlertSet();
        var detector = new DisconnectDetector(alerts, Timeout);
        detector.RecordSeen("AA:01", T0);
        detector.Scan(T0.AddSeconds(15));

        var recovered = detector.RecordSeen("AA:01", T0.AddSeconds(16));

        Assert.True(recovered);
        Assert.Equal(0, alerts.Count);
        Assert.Empty(detector.Scan(T0.AddSeconds(17)).NewlyDisconnected);
    }

    [Fact]
    public void RecordSeen_HealthyNode_IsNotARecovery()
    {
        var detector = new DisconnectDetector(new ActiveAlertSet(), Timeout);

        Assert.False(detector.RecordSeen("AA:01", T0));
    }

    [Fact]
    public void ForceDisconnect_Twice_SecondCallIsADuplicate()
    {
        var alerts = new ActiveAlertSet();
        var detector = new DisconnectDetector(alerts, Timeout);

        Assert.True(detector.ForceDisconnect("AA:01", T0));
        Assert.False(detector.ForceDisconnect("AA:01", T0.AddSeconds(1)));
        Assert.Equal(1, alerts.Count);
    }

    [Fact]
    public void NodeThatNeverReported_IsNotWatched()
    {
        var detector = new DisconnectDetector(new ActiveAlertSet(), Timeout);

        Assert.Empty(detector.Scan(T0.AddHours(1)).NewlyDisconnected);
    }
}
