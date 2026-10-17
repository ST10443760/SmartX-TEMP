using SmartX.Shared.Enums;
using SmartX.Shared.Realtime;

namespace SmartX.Tests;

public class CriticalAlertQueueTests
{
    [Fact]
    public void Dequeue_ServesHigherSeverityFirst()
    {
        var queue = new CriticalAlertQueue<string>();
        queue.Enqueue("warning", AlertSeverity.Warning);
        queue.Enqueue("critical", AlertSeverity.Critical);

        queue.TryDequeue(out var first, out var firstSeverity);
        queue.TryDequeue(out var second, out _);

        Assert.Equal("critical", first);
        Assert.Equal(AlertSeverity.Critical, firstSeverity);
        Assert.Equal("warning", second);
    }

    [Fact]
    public void EqualSeverity_UsesInsertionOrderAsTiebreaker()
    {
        var queue = new CriticalAlertQueue<string>();
        queue.Enqueue("spike-1", AlertSeverity.Critical);
        queue.Enqueue("warn-1", AlertSeverity.Warning);
        queue.Enqueue("spike-2", AlertSeverity.Critical);
        queue.Enqueue("spike-3", AlertSeverity.Critical);
        queue.Enqueue("warn-2", AlertSeverity.Warning);

        var drained = new List<string>();
        while (queue.TryDequeue(out var item, out _))
        {
            drained.Add(item);
        }

        Assert.Equal(["spike-1", "spike-2", "spike-3", "warn-1", "warn-2"], drained);
    }

    [Fact]
    public void TryDequeue_OnEmptyQueue_ReturnsFalse()
    {
        var queue = new CriticalAlertQueue<string>();

        Assert.False(queue.TryDequeue(out _, out _));
    }
}
