using MediaServer.Sse.Core.Broadcasting;
using MediaServer.Sse.Core.Models;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Events.Session;

namespace Jellyfin.Plugin.Sse.Consumers;

public class SessionEndSseConsumer(ISseEventBroadcaster broadcaster) : IEventConsumer<SessionEndedEventArgs>
{
    public Task OnEvent(SessionEndedEventArgs eventArgs)
    {
        var session = eventArgs.Argument;
        broadcaster.Broadcast(new SseEvent
        {
            EventType = "session.end",
            SessionId = session.Id,
            UserId = session.UserId.ToString("N")
        });

        return Task.CompletedTask;
    }
}
