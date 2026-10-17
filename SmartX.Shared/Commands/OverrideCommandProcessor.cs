using SmartX.Shared.Dtos;
using SmartX.Shared.Enums;

namespace SmartX.Shared.Commands;

/// <summary>
/// Executes manual override commands against the <see cref="ActuatorStateBoard"/>
/// and records each one on the <see cref="OverrideCommandStack"/> with the
/// before/after state of every actuator it touched. <see cref="Undo"/> pops
/// the most recent record and writes its previous states back - the inverse
/// of the command - so multi-step undo walks history backwards one command
/// at a time. Undo on an empty stack returns a failed outcome, never throws.
///
/// One lock guards the board and the stack together, so an execute and an
/// undo from two browser tabs can never interleave halfway.
/// </summary>
public sealed class OverrideCommandProcessor(ActuatorStateBoard board, OverrideCommandStack stack)
{
    private readonly Lock _gate = new();

    public int UndoDepth
    {
        get
        {
            lock (_gate)
            {
                return stack.Count;
            }
        }
    }

    public void RegisterActuator(string mac, string name, ActuatorKind kind)
    {
        lock (_gate)
        {
            board.Register(mac, name, kind);
        }
    }

    public CommandOutcome Execute(OverrideCommandRequest request, DateTime nowUtc)
    {
        lock (_gate)
        {
            var targets = ResolveTargets(request, out var error);
            if (error is not null)
            {
                return Fail(error);
            }

            var previous = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var next = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            foreach (var mac in targets)
            {
                board.TryGet(mac, out _, out var wasOn);
                var isOn = request.CommandType switch
                {
                    OverrideCommandType.TriggerPump => true,
                    OverrideCommandType.StopPump => false,
                    OverrideCommandType.ToggleRelay => !wasOn,
                    OverrideCommandType.OpenValve => true,
                    OverrideCommandType.CloseAllValves => false,
                    _ => wasOn
                };

                previous[mac] = wasOn;
                next[mac] = isOn;
                board.Set(mac, isOn);
            }

            var record = new CommandRecord
            {
                CommandType = request.CommandType,
                TargetMac = request.TargetMac,
                Description = Describe(request, next),
                IssuedUtc = nowUtc,
                PreviousStates = previous,
                NewStates = next
            };

            stack.Push(record);

            return new CommandOutcome
            {
                Success = true,
                Message = record.Description,
                Record = record,
                Actuators = board.Snapshot(),
                History = stack.Snapshot()
            };
        }
    }

    public CommandOutcome Undo()
    {
        lock (_gate)
        {
            if (!stack.TryPop(out var record) || record is null)
            {
                return Fail("Nothing to undo - the command history is empty.", isUndo: true);
            }

            foreach (var (mac, wasOn) in record.PreviousStates)
            {
                board.Set(mac, wasOn);
            }

            return new CommandOutcome
            {
                Success = true,
                IsUndo = true,
                Message = $"Undid: {record.Description}",
                Record = record,
                Actuators = board.Snapshot(),
                History = stack.Snapshot()
            };
        }
    }

    public CommandOutcome Snapshot()
    {
        lock (_gate)
        {
            return new CommandOutcome
            {
                Success = true,
                Message = "",
                Actuators = board.Snapshot(),
                History = stack.Snapshot()
            };
        }
    }

    public List<ActuatorState> Actuators()
    {
        lock (_gate)
        {
            return board.Snapshot();
        }
    }

    private List<string> ResolveTargets(OverrideCommandRequest request, out string? error)
    {
        error = null;

        if (request.CommandType == OverrideCommandType.CloseAllValves)
        {
            var valves = board.MacsOfKind(ActuatorKind.Valve).ToList();
            if (valves.Count == 0)
            {
                error = "There are no registered valves to close.";
            }

            return valves;
        }

        var requiredKind = request.CommandType switch
        {
            OverrideCommandType.TriggerPump or OverrideCommandType.StopPump => ActuatorKind.Pump,
            OverrideCommandType.ToggleRelay => ActuatorKind.Relay,
            OverrideCommandType.OpenValve => ActuatorKind.Valve,
            _ => ActuatorKind.None
        };

        if (string.IsNullOrWhiteSpace(request.TargetMac))
        {
            error = $"{request.CommandType} needs a target {requiredKind.ToString().ToLowerInvariant()}.";
            return [];
        }

        if (!board.TryGet(request.TargetMac, out var kind, out _))
        {
            error = $"No actuator is registered with MAC address '{request.TargetMac}'.";
            return [];
        }

        if (kind != requiredKind)
        {
            error = $"{board.NameOf(request.TargetMac)} is a {kind}, but {request.CommandType} needs a {requiredKind}.";
            return [];
        }

        return [request.TargetMac];
    }

    private string Describe(OverrideCommandRequest request, Dictionary<string, bool> next) => request.CommandType switch
    {
        OverrideCommandType.TriggerPump => $"Triggered {board.NameOf(request.TargetMac!)}",
        OverrideCommandType.StopPump => $"Stopped {board.NameOf(request.TargetMac!)}",
        OverrideCommandType.ToggleRelay => $"Switched {board.NameOf(request.TargetMac!)} {(next[request.TargetMac!] ? "on" : "off")}",
        OverrideCommandType.OpenValve => $"Opened {board.NameOf(request.TargetMac!)}",
        OverrideCommandType.CloseAllValves => $"Closed all valves ({next.Count})",
        _ => request.CommandType.ToString()
    };

    private CommandOutcome Fail(string message, bool isUndo = false) => new()
    {
        Success = false,
        IsUndo = isUndo,
        Message = message,
        Actuators = board.Snapshot(),
        History = stack.Snapshot()
    };
}
