using MediaServer.Sse.Core.Models;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.Sse.Consumers;

internal static class PlaybackEventHelper
{
    internal static SseEvent? TryCreateEvent(PlaybackProgressEventArgs args, string eventType, string state)
    {
        if (args.Users.Count == 0 || args.Item is null || args.Item.IsThemeMedia || args.Session is null)
        {
            return null;
        }

        return new SseEvent
        {
            EventType = eventType,
            SessionId = args.Session.Id,
            ItemId = args.Item.Id.ToString("N"),
            UserId = args.Users[0].Id.ToString("N"),
            State = state,
            PositionTicks = args.PlaybackPositionTicks
        };
    }
}
