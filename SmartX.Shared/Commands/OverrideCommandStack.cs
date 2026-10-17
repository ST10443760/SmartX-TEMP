using SmartX.Shared.Dtos;

namespace SmartX.Shared.Commands;

/// <summary>
/// History of manual override commands, wrapping <see cref="Stack{T}"/>.
/// Last-in-first-out is exactly the semantics Undo needs: the most recent
/// command is the first one reversed, then the one before it, and so on.
///
/// The stack is capped so memory stays bounded; when the cap is exceeded
/// the oldest entry (the bottom of the stack) is dropped. That is the only
/// O(n) operation and only happens on overflow - push/pop stay O(1).
/// Not thread-safe on its own; <see cref="OverrideCommandProcessor"/> owns
/// the lock for the stack and the actuator board together.
/// </summary>
public sealed class OverrideCommandStack(int capacity = 100)
{
    private Stack<CommandRecord> _stack = new();

    public int Capacity { get; } = capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity));

    public int Count => _stack.Count;

    public void Push(CommandRecord record)
    {
        _stack.Push(record);

        if (_stack.Count > Capacity)
        {
            // Rebuild without the oldest entry. ToArray() returns top-first,
            // so take the newest Capacity items and push them back oldest-first.
            var newestFirst = _stack.ToArray().Take(Capacity);
            _stack = new Stack<CommandRecord>(newestFirst.Reverse());
        }
    }

    public bool TryPop(out CommandRecord? record) => _stack.TryPop(out record);

    public bool TryPeek(out CommandRecord? record) => _stack.TryPeek(out record);

    /// <summary>Most recent first.</summary>
    public List<CommandRecord> Snapshot() => [.. _stack];
}
