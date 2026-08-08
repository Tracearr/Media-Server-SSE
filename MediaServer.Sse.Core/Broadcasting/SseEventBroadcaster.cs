using System.Collections.Concurrent;
using System.Threading.Channels;
using MediaServer.Sse.Core.Models;
using Microsoft.Extensions.Logging;

namespace MediaServer.Sse.Core.Broadcasting;

public class SseEventBroadcaster : ISseEventBroadcaster
{
    private const int ChannelCapacity = 512;

    private readonly ConcurrentDictionary<Guid, ChannelWriter<SseEvent>> _subscribers = new();
    private readonly ILogger<SseEventBroadcaster> _logger;
    private readonly Timer _pingTimer;
    private bool _disposed;
    private long _overflowDisconnects;

    public SseEventBroadcaster(ILogger<SseEventBroadcaster> logger, int pingIntervalMs = 30_000)
    {
        _logger = logger;
        _pingTimer = new Timer(SendPing, null, pingIntervalMs, pingIntervalMs);
    }

    public int SubscriberCount => _subscribers.Count;

    public (Guid Id, ChannelReader<SseEvent> Reader) Subscribe()
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<SseEvent>(new BoundedChannelOptions(ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = false,
            SingleReader = true
        });

        _subscribers[id] = channel.Writer;
        _logger.LogInformation("SSE subscriber {Id} connected, total: {Count}", id, _subscribers.Count);

        return (id, channel.Reader);
    }

    public void Unsubscribe(Guid id)
    {
        if (_subscribers.TryRemove(id, out var writer))
        {
            writer.TryComplete();
            _logger.LogInformation("SSE subscriber {Id} disconnected, total: {Count}", id, _subscribers.Count);
        }
    }

    public void Broadcast(SseEvent sseEvent)
    {
        foreach (var kvp in _subscribers)
        {
            if (!kvp.Value.TryWrite(sseEvent))
            {
                // Full or completed channel: end this subscriber's stream so the
                // client reconnects and resyncs, rather than losing events silently
                if (kvp.Value.TryComplete())
                {
                    if (_subscribers.TryRemove(kvp.Key, out _))
                    {
                        var count = Interlocked.Increment(ref _overflowDisconnects);
                        _logger.LogWarning(
                            "SSE subscriber {Id} disconnected on buffer overflow (total overflow disconnects: {Count})",
                            kvp.Key, count);
                    }
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _pingTimer.Dispose();

        foreach (var kvp in _subscribers)
        {
            kvp.Value.TryComplete();
        }

        _subscribers.Clear();
        GC.SuppressFinalize(this);
    }

    private void SendPing(object? state)
    {
        if (!_subscribers.IsEmpty)
        {
            Broadcast(new SseEvent { EventType = "ping" });
        }
    }
}
