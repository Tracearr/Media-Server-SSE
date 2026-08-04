using MediaServer.Sse.Core.Broadcasting;

using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Sse.Consumers;

// ILibraryManager exposes ItemAdded/ItemRemoved as plain events rather than through
// Jellyfin's IEventConsumer<T> bus, so this subscribes directly instead of being
// DI-dispatched like the playback/session consumers.
public class LibrarySseConsumer(
    ILibraryManager libraryManager,
    ISseEventBroadcaster broadcaster,
    ILogger<LibrarySseConsumer> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        libraryManager.ItemAdded += OnItemAdded;
        libraryManager.ItemRemoved += OnItemRemoved;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        libraryManager.ItemAdded -= OnItemAdded;
        libraryManager.ItemRemoved -= OnItemRemoved;
        return Task.CompletedTask;
    }

    private void OnItemAdded(object? sender, ItemChangeEventArgs args) => Handle(args, "library.item.added");

    private void OnItemRemoved(object? sender, ItemChangeEventArgs args) => Handle(args, "library.item.removed");

    private void Handle(ItemChangeEventArgs args, string eventType)
    {
        // Never let a malformed item shape throw into the library manager's event dispatch.
        try
        {
            var evt = LibraryEventHelper.TryCreateEvent(args, eventType);
            if (evt is not null)
            {
                broadcaster.Broadcast(evt);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to emit {EventType} event", eventType);
        }
    }
}
