using MediaServer.Sse.Core.Broadcasting;
using Jellyfin.Plugin.Sse.Consumers;
using MediaServer.Sse.Core.Models;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Session;
using MediaBrowser.Controller.Streaming;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MediaServer.Sse.Tests.Consumers;

public class TranscodePullServiceTests
{
    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<ISseEventBroadcaster> _broadcaster = new();
    private readonly Mock<ISessionManager> _sessionManager = new();
    private readonly List<SessionInfo> _sessions = [];
    private readonly Guid _userId = Guid.NewGuid();

    public TranscodePullServiceTests()
    {
        _sessionManager.SetupGet(m => m.Sessions).Returns(() => _sessions.ToList());
    }

    [Fact]
    public void Scan_SignaturePersists20Seconds_BroadcastsStartedThenEnded()
    {
        var service = CreateService(Mock.Of<ITranscodeManager>());
        _sessions.Add(CreateSession(nowPlaying: false));

        service.Scan(Start);
        service.Scan(Start.AddSeconds(15));
        _broadcaster.Verify(b => b.Broadcast(It.IsAny<SseEvent>()), Times.Never);

        service.Scan(Start.AddSeconds(20));
        _broadcaster.Verify(b => b.Broadcast(It.Is<SseEvent>(e =>
            e.EventType == "download.transcoded" &&
            e.Phase == "started" &&
            e.UserId == _userId.ToString("N") &&
            e.DeviceId == "device-1" &&
            e.ItemId == null &&
            e.Bitrate == 1_500_000 &&
            e.Width == 1280 &&
            e.Height == 720 &&
            e.VideoCodec == "h264" &&
            e.AudioCodec == "aac" &&
            e.Container == "mp4" &&
            e.BytesTranscoded == null &&
            e.CompletionPercentage == 42.5)), Times.Once);

        service.Scan(Start.AddSeconds(25));
        _sessions.Clear();
        service.Scan(Start.AddSeconds(30));

        _broadcaster.Verify(b => b.Broadcast(It.Is<SseEvent>(e => e.Phase == "started")), Times.Once);
        _broadcaster.Verify(b => b.Broadcast(It.Is<SseEvent>(e =>
            e.EventType == "download.transcoded" &&
            e.Phase == "ended" &&
            e.DeviceId == "device-1")), Times.Once);
    }

    [Fact]
    public void Scan_SessionIsPlaying_BroadcastsNothing()
    {
        var service = CreateService(Mock.Of<ITranscodeManager>());
        _sessions.Add(CreateSession(nowPlaying: true));

        service.Scan(Start);
        service.Scan(Start.AddSeconds(25));
        _sessions.Clear();
        service.Scan(Start.AddSeconds(30));

        _broadcaster.Verify(b => b.Broadcast(It.IsAny<SseEvent>()), Times.Never);
    }

    [Fact]
    public void Scan_SignatureGoneBefore20Seconds_BroadcastsNothing()
    {
        var service = CreateService(Mock.Of<ITranscodeManager>());
        _sessions.Add(CreateSession(nowPlaying: false));

        service.Scan(Start);
        _sessions.Clear();
        service.Scan(Start.AddSeconds(10));
        service.Scan(Start.AddSeconds(25));

        _broadcaster.Verify(b => b.Broadcast(It.IsAny<SseEvent>()), Times.Never);
    }

    [Fact]
    public void Scan_ActiveJobForDevice_AddsItemIdAndBytesTranscoded()
    {
        var job = new TranscodingJob(NullLogger<TranscodingJob>.Instance)
        {
            DeviceId = "device-1",
            MediaSource = new MediaSourceInfo { Id = "item-1" },
            BytesTranscoded = 1_000,
        };
        var service = CreateService(new FakeTranscodeManager(job));
        _sessions.Add(CreateSession(nowPlaying: false));

        service.Scan(Start);
        service.Scan(Start.AddSeconds(20));
        job.BytesTranscoded = 5_000;
        service.Scan(Start.AddSeconds(25));
        _sessions.Clear();
        service.Scan(Start.AddSeconds(30));

        _broadcaster.Verify(b => b.Broadcast(It.Is<SseEvent>(e =>
            e.Phase == "started" && e.ItemId == "item-1" && e.BytesTranscoded == 1_000)), Times.Once);
        _broadcaster.Verify(b => b.Broadcast(It.Is<SseEvent>(e =>
            e.Phase == "ended" && e.ItemId == "item-1" && e.BytesTranscoded == 5_000)), Times.Once);
    }

    [Fact]
    public void Scan_JobExitsWhileSessionKeepsTranscodingInfo_EndsThenNextJobStartsFresh()
    {
        var first = CreateJob("item-1");
        var manager = new FakeTranscodeManager(first);
        var service = CreateService(manager);
        _sessions.Add(CreateSession(nowPlaying: false));

        service.Scan(Start);
        service.Scan(Start.AddSeconds(20));
        first.BytesTranscoded = 5_000;
        first.HasExited = true;
        service.Scan(Start.AddSeconds(25));

        _broadcaster.Verify(b => b.Broadcast(It.Is<SseEvent>(e =>
            e.Phase == "ended" && e.ItemId == "item-1" && e.BytesTranscoded == 5_000)), Times.Once);

        manager.Add(CreateJob("item-2"));
        service.Scan(Start.AddSeconds(30));
        service.Scan(Start.AddSeconds(45));
        _broadcaster.Verify(b => b.Broadcast(It.Is<SseEvent>(e => e.ItemId == "item-2")), Times.Never);

        service.Scan(Start.AddSeconds(50));
        _broadcaster.Verify(b => b.Broadcast(It.Is<SseEvent>(e =>
            e.Phase == "started" && e.ItemId == "item-2")), Times.Once);
    }

    [Fact]
    public void Scan_NextJobWithoutGap_EndsPreviousPull()
    {
        var first = CreateJob("item-1");
        var manager = new FakeTranscodeManager(first);
        var service = CreateService(manager);
        _sessions.Add(CreateSession(nowPlaying: false));

        service.Scan(Start);
        service.Scan(Start.AddSeconds(20));
        first.HasExited = true;
        manager.Add(CreateJob("item-2"));
        service.Scan(Start.AddSeconds(25));
        service.Scan(Start.AddSeconds(45));

        _broadcaster.Verify(b => b.Broadcast(It.Is<SseEvent>(e =>
            e.Phase == "ended" && e.ItemId == "item-1")), Times.Once);
        _broadcaster.Verify(b => b.Broadcast(It.Is<SseEvent>(e =>
            e.Phase == "started" && e.ItemId == "item-2")), Times.Once);
    }

    [Fact]
    public void Scan_JobsUnreadable_CompletionPercentageNullEndsAndReturnStartsFresh()
    {
        var service = CreateService(Mock.Of<ITranscodeManager>());
        var session = CreateSession(nowPlaying: false);
        _sessions.Add(session);

        service.Scan(Start);
        service.Scan(Start.AddSeconds(20));
        session.TranscodingInfo.CompletionPercentage = null;
        service.Scan(Start.AddSeconds(25));

        _broadcaster.Verify(b => b.Broadcast(It.Is<SseEvent>(e =>
            e.Phase == "ended" && e.CompletionPercentage == 42.5)), Times.Once);

        session.TranscodingInfo.CompletionPercentage = 3;
        service.Scan(Start.AddSeconds(30));
        service.Scan(Start.AddSeconds(50));

        _broadcaster.Verify(b => b.Broadcast(It.Is<SseEvent>(e => e.Phase == "started")), Times.Exactly(2));
        _broadcaster.Verify(b => b.Broadcast(It.Is<SseEvent>(e =>
            e.Phase == "started" && e.CompletionPercentage == 3)), Times.Once);
    }

    private static TranscodingJob CreateJob(string itemId) =>
        new(NullLogger<TranscodingJob>.Instance)
        {
            DeviceId = "device-1",
            MediaSource = new MediaSourceInfo { Id = itemId },
        };

    private TranscodePullSseService CreateService(ITranscodeManager transcodeManager) =>
        new(_sessionManager.Object, transcodeManager, _broadcaster.Object, NullLogger<TranscodePullSseService>.Instance);

    private SessionInfo CreateSession(bool nowPlaying) =>
        new(_sessionManager.Object, NullLogger.Instance)
        {
            DeviceId = "device-1",
            UserId = _userId,
            NowPlayingItem = nowPlaying ? new BaseItemDto() : null,
            TranscodingInfo = new TranscodingInfo
            {
                Bitrate = 1_500_000,
                Width = 1280,
                Height = 720,
                VideoCodec = "h264",
                AudioCodec = "aac",
                Container = "mp4",
                CompletionPercentage = 42.5,
            },
        };

    // Mirrors the private field Jellyfin's TranscodeManager keeps its jobs in.
    private sealed class FakeTranscodeManager(TranscodingJob job) : ITranscodeManager
    {
        private readonly List<TranscodingJob> _activeTranscodingJobs = [job];

        public void Add(TranscodingJob job) => _activeTranscodingJobs.Add(job);

        public TranscodingJob? GetTranscodingJob(string playSessionId) => throw new NotSupportedException();

        public TranscodingJob? GetTranscodingJob(string path, TranscodingJobType type) => throw new NotSupportedException();

        public void PingTranscodingJob(string playSessionId, bool? isUserPaused) => throw new NotSupportedException();

        public Task KillTranscodingJobs(string deviceId, string? playSessionId, Func<string, bool> deleteFiles) => throw new NotSupportedException();

        public void ReportTranscodingProgress(TranscodingJob job, StreamState state, TimeSpan? transcodingPosition, float? framerate, double? percentComplete, long? bytesTranscoded, int? bitRate) => throw new NotSupportedException();

        public Task<TranscodingJob> StartFfMpeg(StreamState state, string outputPath, string commandLineArguments, Guid userId, TranscodingJobType transcodingJobType, CancellationTokenSource cancellationTokenSource, string? workingDirectory = null) => throw new NotSupportedException();

        public TranscodingJob? OnTranscodeBeginRequest(string path, TranscodingJobType type) => throw new NotSupportedException();

        public void OnTranscodeEndRequest(TranscodingJob job) => throw new NotSupportedException();

        public ValueTask<IDisposable> LockAsync(string outputPath, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
