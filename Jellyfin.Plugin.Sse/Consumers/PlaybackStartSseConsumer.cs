using MediaServer.Sse.Core.Broadcasting;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.Sse.Consumers;

public class PlaybackStartSseConsumer(ISseEventBroadcaster broadcaster) : IEventConsumer<PlaybackStartEventArgs>
{
    public Task OnEvent(PlaybackStartEventArgs eventArgs)
    {
        var evt = PlaybackEventHelper.TryCreateEvent(eventArgs, "playing", "playing");
        if (evt is not null)
        {
            broadcaster.Broadcast(evt);
        }

        return Task.CompletedTask;
    }
}
