using MediaServer.Sse.Core.Broadcasting;
using MediaServer.Sse.Core.Models;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Events.Session;

namespace Jellyfin.Plugin.Sse.Consumers;

public class SessionStartSseConsumer(ISseEventBroadcaster broadcaster) : IEventConsumer<SessionStartedEventArgs>
{
    public Task OnEvent(SessionStartedEventArgs eventArgs)
    {
        var session = eventArgs.Argument;
        broadcaster.Broadcast(new SseEvent
        {
            EventType = "session.start",
            SessionId = session.Id,
            UserId = session.UserId.ToString("N")
        });

        return Task.CompletedTask;
    }
}
