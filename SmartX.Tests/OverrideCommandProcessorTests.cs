using SmartX.Shared.Commands;
using SmartX.Shared.Dtos;
using SmartX.Shared.Enums;

namespace SmartX.Tests;

public class OverrideCommandProcessorTests
{
    private const string Pump = "PU:00";
    private const string Valve1 = "VA:01";
    private const string Valve2 = "VA:02";
    private const string Valve3 = "VA:03";
    private const string Relay = "RE:00";

    private static readonly DateTime Now = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);

    private static OverrideCommandProcessor CreateProcessor()
    {
        var processor = new OverrideCommandProcessor(new ActuatorStateBoard(), new OverrideCommandStack());
        processor.RegisterActuator(Pump, "Pump A", ActuatorKind.Pump);
        processor.RegisterActuator(Valve1, "Valve 1", ActuatorKind.Valve);
        processor.RegisterActuator(Valve2, "Valve 2", ActuatorKind.Valve);
        processor.RegisterActuator(Valve3, "Valve 3", ActuatorKind.Valve);
        processor.RegisterActuator(Relay, "Relay 1", ActuatorKind.Relay);
        return processor;
    }

    private static bool IsOn(CommandOutcome outcome, string mac) => outcome.Actuators.Single(a => a.MacAddress == mac).IsOn;

    private static CommandOutcome Run(OverrideCommandProcessor processor, OverrideCommandType type, string? target = null)
        => processor.Execute(new OverrideCommandRequest { CommandType = type, TargetMac = target }, Now);

    [Fact]
    public void Undo_OnEmptyStack_FailsGracefullyInsteadOfThrowing()
    {
        var processor = CreateProcessor();

        var outcome = processor.Undo();

        Assert.False(outcome.Success);
        Assert.True(outcome.IsUndo);
        Assert.Contains("Nothing to undo", outcome.Message);
    }

    [Fact]
    public void Undo_TriggerPump_TurnsThePumpBackOff()
    {
        var processor = CreateProcessor();
        Run(processor, OverrideCommandType.TriggerPump, Pump);

        var outcome = processor.Undo();

        Assert.True(outcome.Success);
        Assert.False(IsOn(outcome, Pump));
        Assert.Equal(0, processor.UndoDepth);
    }

    [Fact]
    public void Undo_CloseAllValves_ReopensExactlyTheValvesThatWereOpenBefore()
    {
        var processor = CreateProcessor();
        Run(processor, OverrideCommandType.OpenValve, Valve1);
        Run(processor, OverrideCommandType.OpenValve, Valve3);

        var closed = Run(processor, OverrideCommandType.CloseAllValves);
        Assert.False(IsOn(closed, Valve1));
        Assert.False(IsOn(closed, Valve3));

        var undone = processor.Undo();

        Assert.True(IsOn(undone, Valve1));
        Assert.False(IsOn(undone, Valve2)); // was closed before, must stay closed
        Assert.True(IsOn(undone, Valve3));
    }

    [Fact]
    public void MultiStepUndo_WalksHistoryBackwardsOneCommandAtATime()
    {
        var processor = CreateProcessor();
        Run(processor, OverrideCommandType.TriggerPump, Pump);   // 1: pump on
        Run(processor, OverrideCommandType.ToggleRelay, Relay);  // 2: relay on
        Run(processor, OverrideCommandType.OpenValve, Valve2);   // 3: valve 2 open

        var afterFirstUndo = processor.Undo();
        Assert.False(IsOn(afterFirstUndo, Valve2));
        Assert.True(IsOn(afterFirstUndo, Relay));
        Assert.True(IsOn(afterFirstUndo, Pump));

        var afterSecondUndo = processor.Undo();
        Assert.False(IsOn(afterSecondUndo, Relay));
        Assert.True(IsOn(afterSecondUndo, Pump));

        var afterThirdUndo = processor.Undo();
        Assert.False(IsOn(afterThirdUndo, Pump));

        Assert.False(processor.Undo().Success);
    }

    [Fact]
    public void ToggleRelay_TwiceThenUndoOnce_LeavesRelayOn()
    {
        var processor = CreateProcessor();
        Run(processor, OverrideCommandType.ToggleRelay, Relay);
        Run(processor, OverrideCommandType.ToggleRelay, Relay);

        var outcome = processor.Undo();

        Assert.True(IsOn(outcome, Relay));
    }

    [Fact]
    public void Execute_WrongActuatorKind_FailsAndDoesNotPushToTheStack()
    {
        var processor = CreateProcessor();

        var outcome = Run(processor, OverrideCommandType.TriggerPump, Valve1);

        Assert.False(outcome.Success);
        Assert.Equal(0, processor.UndoDepth);
    }

    [Fact]
    public void Execute_UnknownTarget_FailsWithoutThrowing()
    {
        var processor = CreateProcessor();

        var outcome = Run(processor, OverrideCommandType.ToggleRelay, "NOPE");

        Assert.False(outcome.Success);
    }

    [Fact]
    public void CommandStack_DropsTheOldestEntryWhenOverCapacity()
    {
        var stack = new OverrideCommandStack(capacity: 3);
        for (var i = 1; i <= 5; i++)
        {
            stack.Push(new CommandRecord
            {
                CommandType = OverrideCommandType.ToggleRelay,
                Description = $"cmd {i}",
                IssuedUtc = Now,
                PreviousStates = [],
                NewStates = []
            });
        }

        Assert.Equal(3, stack.Count);
        Assert.Equal(["cmd 5", "cmd 4", "cmd 3"], stack.Snapshot().Select(r => r.Description));
    }
}
