using MediaServer.Sse.Core.Broadcasting;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.Sse.Consumers;

public class PlaybackStopSseConsumer(ISseEventBroadcaster broadcaster) : IEventConsumer<PlaybackStopEventArgs>
{
    public Task OnEvent(PlaybackStopEventArgs eventArgs)
    {
        var evt = PlaybackEventHelper.TryCreateEvent(eventArgs, "stopped", "stopped");
        if (evt is not null)
        {
            evt.PlayedToCompletion = eventArgs.PlayedToCompletion;
            broadcaster.Broadcast(evt);
        }

        return Task.CompletedTask;
    }
}
