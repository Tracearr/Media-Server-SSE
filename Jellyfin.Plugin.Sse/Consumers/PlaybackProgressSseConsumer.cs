using MediaServer.Sse.Core.Broadcasting;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.Sse.Consumers;

public class PlaybackProgressSseConsumer(ISseEventBroadcaster broadcaster) : IEventConsumer<PlaybackProgressEventArgs>
{
    public Task OnEvent(PlaybackProgressEventArgs eventArgs)
    {
        var (eventType, state) = eventArgs.IsPaused ? ("paused", "paused") : ("progress", "playing");
        var evt = PlaybackEventHelper.TryCreateEvent(eventArgs, eventType, state);
        if (evt is not null)
        {
            broadcaster.Broadcast(evt);
        }

        return Task.CompletedTask;
    }
}
