using SmartX.Shared.Realtime;

namespace SmartX.Tests;

public class PacketQueueTests
{
    [Fact]
    public void Dequeue_ReturnsPacketsInArrivalOrder()
    {
        var queue = new PacketQueue<string>();
        queue.Enqueue("first");
        queue.Enqueue("second");
        queue.Enqueue("third");

        var drained = new List<string>();
        while (queue.TryDequeue(out var item))
        {
            drained.Add(item);
        }

        Assert.Equal(["first", "second", "third"], drained);
    }

    [Fact]
    public void TryDequeue_OnEmptyQueue_ReturnsFalseWithoutThrowing()
    {
        var queue = new PacketQueue<int>();

        Assert.False(queue.TryDequeue(out _));
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void ConcurrentEnqueues_AreAllKept()
    {
        var queue = new PacketQueue<int>();

        Parallel.For(0, 1000, queue.Enqueue);

        Assert.Equal(1000, queue.Count);
    }
}
