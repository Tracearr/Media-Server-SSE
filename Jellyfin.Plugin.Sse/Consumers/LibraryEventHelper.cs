using MediaServer.Sse.Core.Models;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.Sse.Consumers;

internal static class LibraryEventHelper
{
    internal static SseEvent? TryCreateEvent(ItemChangeEventArgs args, string eventType)
    {
        var item = args.Item;
        if (item is null || item.IsThemeMedia || item.IsVirtualItem)
        {
            return null;
        }

        return new SseEvent
        {
            EventType = eventType,
            ItemId = item.Id.ToString("N"),
            ItemType = item.GetClientTypeName(),
            ParentId = args.Parent?.Id.ToString("N")
        };
    }
}
