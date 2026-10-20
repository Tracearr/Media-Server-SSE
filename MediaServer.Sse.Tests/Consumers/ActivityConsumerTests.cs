using MediaServer.Sse.Core.Broadcasting;
using Jellyfin.Plugin.Sse.Consumers;
using MediaServer.Sse.Core.Models;
using Jellyfin.Data.Events;
using MediaBrowser.Model.Activity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MediaServer.Sse.Tests.Consumers;

public class ActivityConsumerTests
{
    private readonly Mock<ISseEventBroadcaster> _broadcaster;
    private readonly Mock<IActivityManager> _activityManager;
    private readonly ActivitySseConsumer _consumer;

    public ActivityConsumerTests()
    {
        _broadcaster = new Mock<ISseEventBroadcaster>();
        _activityManager = new Mock<IActivityManager>();
        _consumer = new ActivitySseConsumer(
            _activityManager.Object, _broadcaster.Object, NullLogger<ActivitySseConsumer>.Instance);
    }

    [Fact]
    public async Task EntryCreated_OtherType_BroadcastsNothing()
    {
        await _consumer.StartAsync(CancellationToken.None);

        Raise(new ActivityLogEntry("alice is playing Heat", "VideoPlayback", Guid.NewGuid()));

        _broadcaster.Verify(b => b.Broadcast(It.IsAny<SseEvent>()), Times.Never);
    }

    [Fact]
    public async Task EntryCreated_Download_BroadcastsLibraryItemDownloaded()
    {
        await _consumer.StartAsync(CancellationToken.None);

        var userId = Guid.NewGuid();
        Raise(new ActivityLogEntry("alice is downloading Heat", "UserDownloadingContent", userId)
        {
            ShortOverview = "App: Jellyfin Web, Device: Firefox",
            ItemId = "0a1b2c3d4e5f60718293a4b5c6d7e8f9",
        });

        _broadcaster.Verify(b => b.Broadcast(It.Is<SseEvent>(e =>
            e.EventType == "library.item.downloaded" &&
            e.UserId == userId.ToString("N") &&
            e.ItemId == "0a1b2c3d4e5f60718293a4b5c6d7e8f9" &&
            e.ItemName == "alice is downloading Heat" &&
            e.App == "Jellyfin Web" &&
            e.Device == "Firefox")), Times.Once);
    }

    [Theory]
    [InlineData("App: Jellyfin Web, Gerät: Firefox")]
    [InlineData("Application : Jellyfin Web, Appareil : Firefox")]
    [InlineData("应用：Jellyfin Web，设备：Firefox")]
    public async Task EntryCreated_LocalizedOverview_ParsesAppAndDevice(string overview)
    {
        await _consumer.StartAsync(CancellationToken.None);

        Raise(new ActivityLogEntry("alice", "UserDownloadingContent", Guid.NewGuid()) { ShortOverview = overview });

        _broadcaster.Verify(b => b.Broadcast(It.Is<SseEvent>(e =>
            e.App == "Jellyfin Web" && e.Device == "Firefox")), Times.Once);
    }

    [Fact]
    public async Task StopAsync_UnsubscribesFromActivityManager()
    {
        await _consumer.StartAsync(CancellationToken.None);
        await _consumer.StopAsync(CancellationToken.None);

        Raise(new ActivityLogEntry("alice is downloading Heat", "UserDownloadingContent", Guid.NewGuid()));

        _broadcaster.Verify(b => b.Broadcast(It.IsAny<SseEvent>()), Times.Never);
    }

    private void Raise(ActivityLogEntry entry) =>
        _activityManager.Raise(
            m => m.EntryCreated += null,
            _activityManager.Object,
            new GenericEventArgs<ActivityLogEntry>(entry));
}
