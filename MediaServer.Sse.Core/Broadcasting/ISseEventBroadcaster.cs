using System.Threading.Channels;
using MediaServer.Sse.Core.Models;

namespace MediaServer.Sse.Core.Broadcasting;

public interface ISseEventBroadcaster : IDisposable
{
    int SubscriberCount { get; }

    (Guid Id, ChannelReader<SseEvent> Reader) Subscribe();

    void Unsubscribe(Guid id);

    void Broadcast(SseEvent sseEvent);
}
