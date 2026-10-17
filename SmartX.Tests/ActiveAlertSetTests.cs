using SmartX.Shared.Enums;
using SmartX.Shared.Realtime;

namespace SmartX.Tests;

public class ActiveAlertSetTests
{
    private static readonly DateTime Now = new(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TryRaise_SameNodeTwice_OnlyTheFirstRaisesAnAlert()
    {
        var alerts = new ActiveAlertSet();

        var first = alerts.TryRaise("AA:01", AlertKind.Disconnected, Now);
        var second = alerts.TryRaise("aa:01", AlertKind.Disconnected, Now.AddSeconds(5));

        Assert.True(first);
        Assert.False(second);
        Assert.Equal(1, alerts.Count);
    }

    [Fact]
    public void DifferentKindsOnTheSameNode_AreSeparateErrorStates()
    {
        var alerts = new ActiveAlertSet();

        alerts.TryRaise("AA:01", AlertKind.Disconnected, Now);
        alerts.TryRaise("AA:01", AlertKind.CriticalReading, Now);

        Assert.Equal(2, alerts.Count);
    }

    [Fact]
    public void TryRecover_RemovesTheNodeAndReportsTheRecovery()
    {
        var alerts = new ActiveAlertSet();
        alerts.TryRaise("AA:01", AlertKind.Disconnected, Now);

        var recovered = alerts.TryRecover("AA:01", AlertKind.Disconnected);

        Assert.True(recovered);
        Assert.False(alerts.Contains("AA:01", AlertKind.Disconnected));
    }

    [Fact]
    public void TryRecover_NodeThatWasNeverAlerted_ReportsNothing()
    {
        var alerts = new ActiveAlertSet();

        Assert.False(alerts.TryRecover("AA:01", AlertKind.Disconnected));
    }

    [Fact]
    public void AfterRecovery_TheNodeCanAlertAgain()
    {
        var alerts = new ActiveAlertSet();
        alerts.TryRaise("AA:01", AlertKind.CriticalReading, Now);
        alerts.TryRecover("AA:01", AlertKind.CriticalReading);

        Assert.True(alerts.TryRaise("AA:01", AlertKind.CriticalReading, Now.AddMinutes(1)));
    }
}
