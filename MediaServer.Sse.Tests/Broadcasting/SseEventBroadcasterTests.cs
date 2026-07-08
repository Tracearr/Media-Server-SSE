using MediaServer.Sse.Core.Broadcasting;
using MediaServer.Sse.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MediaServer.Sse.Tests.Broadcasting;

public class SseEventBroadcasterTests : IDisposable
{
    private readonly SseEventBroadcaster _broadcaster;

    public SseEventBroadcasterTests()
    {
        _broadcaster = new SseEventBroadcaster(NullLogger<SseEventBroadcaster>.Instance);
    }

    public void Dispose()
    {
        _broadcaster.Dispose();
    }

    [Fact]
    public void Subscribe_ReturnsUniqueIdAndReader()
    {
        var (id1, reader1) = _broadcaster.Subscribe();
        var (id2, reader2) = _broadcaster.Subscribe();

        Assert.NotEqual(id1, id2);
        Assert.NotNull(reader1);
        Assert.NotNull(reader2);
    }

    [Fact]
    public void Broadcast_DeliversEventToAllSubscribers()
    {
        var (_, reader1) = _broadcaster.Subscribe();
        var (_, reader2) = _broadcaster.Subscribe();

        var evt = new SseEvent { EventType = "playing", SessionId = "s1", State = "playing" };
        _broadcaster.Broadcast(evt);

        Assert.True(reader1.TryRead(out var received1));
        Assert.Equal("s1", received1!.SessionId);

        Assert.True(reader2.TryRead(out var received2));
        Assert.Equal("s1", received2!.SessionId);
    }

    [Fact]
    public void Broadcast_DoesNotDeliverToUnsubscribed()
    {
        var (id1, reader1) = _broadcaster.Subscribe();
        var (_, reader2) = _broadcaster.Subscribe();

        _broadcaster.Unsubscribe(id1);

        var evt = new SseEvent { EventType = "playing", SessionId = "s1" };
        _broadcaster.Broadcast(evt);

        Assert.False(reader1.TryRead(out _));
        Assert.True(reader2.TryRead(out _));
    }

    [Fact]
    public async Task PingTimer_SendsPingEvents()
    {
        using var fastBroadcaster = new SseEventBroadcaster(
            NullLogger<SseEventBroadcaster>.Instance,
            pingIntervalMs: 100);

        var (_, reader) = fastBroadcaster.Subscribe();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var evt = await reader.ReadAsync(cts.Token);

        Assert.Equal("ping", evt.EventType);
    }

    [Fact]
    public async Task Broadcast_CompletesSubscriberChannel_WhenBufferOverflows()
    {
        using var broadcaster = new SseEventBroadcaster(NullLogger<SseEventBroadcaster>.Instance);
        var (_, reader) = broadcaster.Subscribe();

        // Never read; exceed the buffer (capacity 512) to force overflow
        for (var i = 0; i < 600; i++)
        {
            broadcaster.Broadcast(new SseEvent { EventType = "progress", SessionId = i.ToString() });
        }

        // Drain what was buffered; the channel must then be completed, not open-and-lossy
        var drained = 0;
        while (reader.TryRead(out _))
        {
            drained++;
        }

        Assert.Equal(512, drained);
        await reader.Completion.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(reader.Completion.IsCompleted);
    }

    [Fact]
    public void Broadcast_DoesNotAffectHealthySubscriber_WhenAnotherOverflows()
    {
        using var broadcaster = new SseEventBroadcaster(NullLogger<SseEventBroadcaster>.Instance);
        var (_, stalled) = broadcaster.Subscribe();
        var (_, healthy) = broadcaster.Subscribe();

        for (var i = 0; i < 600; i++)
        {
            broadcaster.Broadcast(new SseEvent { EventType = "progress", SessionId = i.ToString() });
            // Healthy subscriber keeps up
            healthy.TryRead(out _);
        }

        // Healthy channel still open and writable
        broadcaster.Broadcast(new SseEvent { EventType = "ping" });
        Assert.True(healthy.TryRead(out var evt));
        Assert.Equal("ping", evt!.EventType);
        Assert.False(healthy.Completion.IsCompleted);
        _ = stalled; // overflowed subscriber asserted in the other test
    }
}
