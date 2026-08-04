using MediaServer.Sse.Core.Broadcasting;
using Jellyfin.Plugin.Sse.Consumers;
using MediaServer.Sse.Core.Models;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MediaServer.Sse.Tests.Consumers;

public class LibraryConsumerTests
{
    private readonly Mock<ISseEventBroadcaster> _broadcaster;
    private readonly Mock<ILibraryManager> _libraryManager;
    private readonly LibrarySseConsumer _consumer;

    public LibraryConsumerTests()
    {
        _broadcaster = new Mock<ISseEventBroadcaster>();
        _libraryManager = new Mock<ILibraryManager>();
        _consumer = new LibrarySseConsumer(
            _libraryManager.Object, _broadcaster.Object, NullLogger<LibrarySseConsumer>.Instance);
    }

    [Fact]
    public async Task StartAsync_SubscribesToItemAdded_BroadcastsLibraryItemAddedEvent()
    {
        await _consumer.StartAsync(CancellationToken.None);

        var itemId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var args = CreateArgs(itemId, parentId);

        _libraryManager.Raise(m => m.ItemAdded += null, _libraryManager.Object, args);

        _broadcaster.Verify(b => b.Broadcast(It.Is<SseEvent>(e =>
            e.EventType == "library.item.added" &&
            e.ItemId == itemId.ToString("N") &&
            e.ItemType == "TestItem" &&
            e.ParentId == parentId.ToString("N"))), Times.Once);
    }

    [Fact]
    public async Task StartAsync_SubscribesToItemRemoved_BroadcastsLibraryItemRemovedEvent()
    {
        await _consumer.StartAsync(CancellationToken.None);

        var itemId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var args = CreateArgs(itemId, parentId);

        _libraryManager.Raise(m => m.ItemRemoved += null, _libraryManager.Object, args);

        _broadcaster.Verify(b => b.Broadcast(It.Is<SseEvent>(e =>
            e.EventType == "library.item.removed" &&
            e.ItemId == itemId.ToString("N"))), Times.Once);
    }

    [Fact]
    public async Task StopAsync_UnsubscribesFromLibraryEvents()
    {
        await _consumer.StartAsync(CancellationToken.None);
        await _consumer.StopAsync(CancellationToken.None);

        var args = CreateArgs(Guid.NewGuid(), Guid.NewGuid());
        _libraryManager.Raise(m => m.ItemAdded += null, _libraryManager.Object, args);

        _broadcaster.Verify(b => b.Broadcast(It.IsAny<SseEvent>()), Times.Never);
    }

    [Fact]
    public async Task ItemAdded_SkipsThemeMedia()
    {
        await _consumer.StartAsync(CancellationToken.None);

        var item = new TestItem { Id = Guid.NewGuid(), ExtraType = ExtraType.ThemeSong };
        var args = new ItemChangeEventArgs { Item = item };

        _libraryManager.Raise(m => m.ItemAdded += null, _libraryManager.Object, args);

        _broadcaster.Verify(b => b.Broadcast(It.IsAny<SseEvent>()), Times.Never);
    }

    [Fact]
    public async Task ItemAdded_SkipsVirtualItem()
    {
        await _consumer.StartAsync(CancellationToken.None);

        var item = new TestItem { Id = Guid.NewGuid(), IsVirtualItem = true };
        var args = new ItemChangeEventArgs { Item = item };

        _libraryManager.Raise(m => m.ItemAdded += null, _libraryManager.Object, args);

        _broadcaster.Verify(b => b.Broadcast(It.IsAny<SseEvent>()), Times.Never);
    }

    [Fact]
    public async Task ItemAdded_UnexpectedItemShape_DropsInsteadOfThrowing()
    {
        await _consumer.StartAsync(CancellationToken.None);

        var item = new ThrowingItem { Id = Guid.NewGuid() };
        var args = new ItemChangeEventArgs { Item = item };

        // Raising through the mocked event must not let the consumer's exception escape.
        _libraryManager.Raise(m => m.ItemAdded += null, _libraryManager.Object, args);

        _broadcaster.Verify(b => b.Broadcast(It.IsAny<SseEvent>()), Times.Never);
    }

    private static ItemChangeEventArgs CreateArgs(Guid itemId, Guid parentId)
    {
        var item = new TestItem { Id = itemId };
        var parent = new TestItem { Id = parentId };
        return new ItemChangeEventArgs { Item = item, Parent = parent };
    }

    private sealed class ThrowingItem : TestItem
    {
        public override string GetClientTypeName() => throw new InvalidOperationException("boom");
    }
}
