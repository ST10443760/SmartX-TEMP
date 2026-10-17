using SmartX.Shared.Enums;
using SmartX.Shared.Insights;

namespace SmartX.Tests;

public class SuggestionEngineTests
{
    private static readonly DateTime T0 = new(2026, 10, 17, 8, 0, 0, DateTimeKind.Utc);

    private static ActivityEntry Search(string term, DateTime at) => new(ActivityKind.Search, term, at);

    private static ActivityEntry Command(string label, DateTime at, OverrideCommandType type = OverrideCommandType.TriggerPump, string mac = "PUMP")
        => new(ActivityKind.Command, label, at, type, mac);

    private static ActivityEntry Condition(string label, DateTime at) => new(ActivityKind.Condition, label, at);

    [Fact]
    public void GivenAKnownHistory_RecommendsTheMostFrequentNextSearch()
    {
        var engine = new SuggestionEngine();
        var t = T0;
        // "Moisture Sensor B" is followed by "Pump A" three times and by "Valve 1" once.
        foreach (var next in new[] { "Pump A", "Pump A", "Valve 1", "Pump A" })
        {
            engine.Record(Search("Moisture Sensor B", t));
            engine.Record(Search(next, t.AddSeconds(20)));
            t = t.AddMinutes(30);
        }

        var suggestions = engine.SuggestAfter(Search("Moisture Sensor B", t));

        var top = Assert.Single(suggestions); // Valve 1 (seen once) is below the minimum
        Assert.Equal("Pump A", top.Label);
        Assert.Equal(ActivityKind.Search, top.Kind);
        Assert.Equal(3, top.Occurrences);
        Assert.Equal(0.75, top.Confidence, precision: 2);
        Assert.Contains("within a minute", top.Reason);
    }

    [Fact]
    public void Suggestions_AreCaseInsensitiveOnTheTrigger()
    {
        var engine = new SuggestionEngine(minimumOccurrences: 1);
        engine.Record(Search("Moisture Sensor B", T0));
        engine.Record(Search("Pump A", T0.AddSeconds(5)));

        Assert.Single(engine.SuggestAfter(Search("moisture sensor b", T0.AddHours(1))));
    }

    [Fact]
    public void ConditionFollowedByCommand_SuggestsTheCommandWithAReason()
    {
        var engine = new SuggestionEngine();
        var t = T0;
        for (var i = 0; i < 4; i++)
        {
            engine.Record(Condition("Moisture Sensor B below 20%", t));
            engine.Record(Command("Trigger Pump A", t.AddSeconds(40)));
            t = t.AddMinutes(30);
        }

        var suggestion = Assert.Single(engine.SuggestAfter(Condition("Moisture Sensor B below 20%", t)));

        Assert.Equal(ActivityKind.Command, suggestion.Kind);
        Assert.Equal(OverrideCommandType.TriggerPump, suggestion.CommandType);
        Assert.Equal("PUMP", suggestion.TargetMac);
        Assert.Equal("When Moisture Sensor B below 20%, operators ran \"Trigger Pump A\" within a minute (4 of 4 times).", suggestion.Reason);
    }

    [Fact]
    public void ActionsOutsideTheFollowWindow_AreNotLinked()
    {
        var engine = new SuggestionEngine(followWindow: TimeSpan.FromMinutes(1), minimumOccurrences: 1);
        engine.Record(Search("Power Meter A", T0));
        engine.Record(Search("Relay 1", T0.AddMinutes(10)));

        Assert.Empty(engine.SuggestAfter(Search("Power Meter A", T0)));
    }

    [Fact]
    public void SuggestNext_UsesTheMostRecentOperatorAction()
    {
        var engine = new SuggestionEngine(minimumOccurrences: 1);
        engine.Record(Search("Power Meter A", T0));
        engine.Record(Command("Toggle Relay 1", T0.AddSeconds(30), OverrideCommandType.ToggleRelay, "RELAY"));
        engine.Record(Search("Power Meter A", T0.AddHours(1)));

        var next = Assert.Single(engine.SuggestNext());
        Assert.Equal("Toggle Relay 1", next.Label);
    }

    [Fact]
    public void BoundedHistory_ForgetsTransitionsWhenOldEntriesAreEvicted()
    {
        var engine = new SuggestionEngine(historyCapacity: 4, minimumOccurrences: 1);
        engine.Record(Search("A", T0));
        engine.Record(Search("B", T0.AddSeconds(10)));   // A -> B
        Assert.Single(engine.SuggestAfter(Search("A", T0)));

        // Four unrelated, far-apart entries push A and B out of the bounded history.
        for (var i = 1; i <= 4; i++)
        {
            engine.Record(Search($"X{i}", T0.AddHours(i)));
        }

        Assert.Equal(4, engine.HistorySize);
        Assert.Empty(engine.SuggestAfter(Search("A", T0)));
        Assert.Equal(0, engine.PatternCount);
    }

    [Fact]
    public void RepeatingTheSameAction_IsNotAPattern()
    {
        var engine = new SuggestionEngine(minimumOccurrences: 1);
        engine.Record(Search("Pump A", T0));
        engine.Record(Search("Pump A", T0.AddSeconds(5)));

        Assert.Empty(engine.SuggestAfter(Search("Pump A", T0)));
    }

    [Fact]
    public void AnalyzerFlagsASustainedMoistureDropBeforeTheThreshold()
    {
        var insight = DeviceHealthAnalyzer.Analyze("MAC", "Moisture Sensor B", SensorCategory.Environmental,
            [40, 37, 34, 31, 28, 25], hasCriticalAlert: false, isDisconnected: false);

        Assert.NotNull(insight);
        Assert.Equal(InsightKind.SustainedTrend, insight!.Kind);
        Assert.Contains("fallen 15 points", insight.Reason);
    }

    [Fact]
    public void AnalyzerFlagsOutOfRangeAndFaultsAndIgnoresHealthyDevices()
    {
        Assert.Equal(InsightKind.OutOfRange, DeviceHealthAnalyzer.Analyze("M", "Power Meter A", SensorCategory.PowerConsumption, [500, 2500], false, false)!.Kind);
        Assert.Equal(InsightKind.Fault, DeviceHealthAnalyzer.Analyze("M", "Power Meter A", SensorCategory.PowerConsumption, [4300], true, false)!.Kind);
        Assert.Equal(InsightKind.Disconnected, DeviceHealthAnalyzer.Analyze("M", "Pump A", SensorCategory.Actuator, [], false, true)!.Kind);
        Assert.Null(DeviceHealthAnalyzer.Analyze("M", "Moisture Sensor A", SensorCategory.Environmental, [45, 47, 44, 46, 45, 48], false, false));
    }
}
