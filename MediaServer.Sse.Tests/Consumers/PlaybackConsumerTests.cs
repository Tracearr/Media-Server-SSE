using Jellyfin.Database.Implementations.Entities;
using MediaServer.Sse.Core.Broadcasting;
using Jellyfin.Plugin.Sse.Consumers;
using MediaServer.Sse.Core.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MediaServer.Sse.Tests.Consumers;

internal class TestItem : BaseItem
{
    public override bool SupportsLocalMetadata => false;
}

public class PlaybackConsumerTests
{
    private readonly Mock<ISseEventBroadcaster> _broadcaster;

    public PlaybackConsumerTests()
    {
        _broadcaster = new Mock<ISseEventBroadcaster>();
    }

    [Fact]
    public async Task PlaybackStart_BroadcastsPlayingEvent()
    {
        var consumer = new PlaybackStartSseConsumer(_broadcaster.Object);
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var args = CreatePlaybackStartArgs(userId, itemId, sessionId: "session1");

        await consumer.OnEvent(args);

        _broadcaster.Verify(b => b.Broadcast(It.Is<SseEvent>(e =>
            e.EventType == "playing" &&
            e.SessionId == "session1" &&
            e.ItemId == itemId.ToString("N") &&
            e.UserId == userId.ToString("N") &&
            e.State == "playing")), Times.Once);
    }

    [Fact]
    public async Task PlaybackStart_SkipsWhenNoUsers()
    {
        var consumer = new PlaybackStartSseConsumer(_broadcaster.Object);
        var args = CreatePlaybackStartArgs(Guid.Empty, Guid.NewGuid(), sessionId: "s1", includeUser: false);

        await consumer.OnEvent(args);

        _broadcaster.Verify(b => b.Broadcast(It.IsAny<SseEvent>()), Times.Never);
    }

    [Fact]
    public async Task PlaybackStop_IncludesPlayedToCompletion()
    {
        var consumer = new PlaybackStopSseConsumer(_broadcaster.Object);
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var args = CreatePlaybackStopArgs(userId, itemId, sessionId: "session1", playedToCompletion: true);

        await consumer.OnEvent(args);

        _broadcaster.Verify(b => b.Broadcast(It.Is<SseEvent>(e =>
            e.EventType == "stopped" &&
            e.State == "stopped" &&
            e.PlayedToCompletion == true)), Times.Once);
    }

    [Fact]
    public async Task PlaybackProgress_EmitsPausedWhenPaused()
    {
        var consumer = new PlaybackProgressSseConsumer(_broadcaster.Object);
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var args = CreatePlaybackProgressArgs(userId, itemId, sessionId: "s1", isPaused: true, positionTicks: 5000);

        await consumer.OnEvent(args);

        _broadcaster.Verify(b => b.Broadcast(It.Is<SseEvent>(e =>
            e.EventType == "paused" &&
            e.State == "paused" &&
            e.PositionTicks == 5000)), Times.Once);
    }

    [Fact]
    public async Task PlaybackProgress_EmitsProgressWhenPlaying()
    {
        var consumer = new PlaybackProgressSseConsumer(_broadcaster.Object);
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var args = CreatePlaybackProgressArgs(userId, itemId, sessionId: "s1", isPaused: false, positionTicks: 10000);

        await consumer.OnEvent(args);

        _broadcaster.Verify(b => b.Broadcast(It.Is<SseEvent>(e =>
            e.EventType == "progress" &&
            e.State == "playing" &&
            e.PositionTicks == 10000)), Times.Once);
    }

    private static PlaybackStartEventArgs CreatePlaybackStartArgs(
        Guid userId, Guid itemId, string sessionId, bool includeUser = true)
    {
        var item = new TestItem { Id = itemId };
        var session = new SessionInfo(null!, NullLogger.Instance) { Id = sessionId };

        var args = new PlaybackStartEventArgs
        {
            Item = item,
            Session = session,
            PlaybackPositionTicks = 0
        };

        if (includeUser)
        {
            var user = new User("testuser", "Jellyfin.DefaultAuthProvider", "Jellyfin.DefaultPasswordResetProvider") { Id = userId };
            args.Users = new List<User> { user };
        }
        else
        {
            args.Users = new List<User>();
        }

        return args;
    }

    private static PlaybackStopEventArgs CreatePlaybackStopArgs(
        Guid userId, Guid itemId, string sessionId, bool playedToCompletion)
    {
        var item = new TestItem { Id = itemId };
        var session = new SessionInfo(null!, NullLogger.Instance) { Id = sessionId };
        var user = new User("testuser", "Jellyfin.DefaultAuthProvider", "Jellyfin.DefaultPasswordResetProvider") { Id = userId };

        return new PlaybackStopEventArgs
        {
            Item = item,
            Session = session,
            Users = new List<User> { user },
            PlaybackPositionTicks = 0,
            PlayedToCompletion = playedToCompletion
        };
    }

    private static PlaybackProgressEventArgs CreatePlaybackProgressArgs(
        Guid userId, Guid itemId, string sessionId, bool isPaused, long positionTicks)
    {
        var item = new TestItem { Id = itemId };
        var session = new SessionInfo(null!, NullLogger.Instance) { Id = sessionId };
        var user = new User("testuser", "Jellyfin.DefaultAuthProvider", "Jellyfin.DefaultPasswordResetProvider") { Id = userId };

        return new PlaybackProgressEventArgs
        {
            Item = item,
            Session = session,
            Users = new List<User> { user },
            PlaybackPositionTicks = positionTicks,
            IsPaused = isPaused
        };
    }
}
