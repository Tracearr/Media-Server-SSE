using System.Reflection;

using MediaServer.Sse.Core.Broadcasting;
using MediaServer.Sse.Core.Models;

using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Sse.Consumers;

// A client downloading at reduced quality pulls a transcode through the stream endpoint,
// which writes no activity entry. Its session shows TranscodingInfo without a NowPlayingItem;
// a real player reports playback within seconds, so the signature outliving MinAge is a download.
// Jellyfin 12 leaves TranscodingInfo on the session after ffmpeg exits (CompletionPercentage
// goes null), so the end of a pull is the device's live job going away, or that null when the
// job list is unreadable.
public class TranscodePullSseService(
    ISessionManager sessionManager,
    ITranscodeManager transcodeManager,
    ISseEventBroadcaster broadcaster,
    ILogger<TranscodePullSseService> logger) : IHostedService, IDisposable
{
    private const int ScanIntervalMs = 5_000;
    private static readonly TimeSpan MinAge = TimeSpan.FromSeconds(20);

    // Jellyfin 12.0 through 12.2 keep active jobs in TranscodeManager's private
    // List<TranscodingJob> _activeTranscodingJobs (locked on itself). ITranscodeManager only
    // looks jobs up by play session id or output path, and a session exposes neither.
    private const string ActiveJobsField = "_activeTranscodingJobs";

    private readonly Dictionary<string, Pull> _pulls = new(StringComparer.OrdinalIgnoreCase);
    private FieldInfo? _jobsField;
    private bool _jobsUnavailable;
    private Timer? _timer;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(_ => Scan(DateTime.UtcNow), null, ScanIntervalMs, ScanIntervalMs);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
        GC.SuppressFinalize(this);
    }

    public void Scan(DateTime nowUtc)
    {
        try
        {
            lock (_pulls)
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var session in sessionManager.Sessions)
                {
                    if (session.TranscodingInfo is not { } info
                        || session.NowPlayingItem is not null
                        || string.IsNullOrEmpty(session.DeviceId)
                        || seen.Contains(session.DeviceId))
                    {
                        continue;
                    }

                    var job = FindJob(session.DeviceId);
                    var live = _jobsUnavailable ? info.CompletionPercentage is not null : job is not null;
                    if (!live)
                    {
                        continue;
                    }

                    seen.Add(session.DeviceId);
                    if (_pulls.TryGetValue(session.DeviceId, out var pull) && job is not null && pull.Job is not null && job != pull.Job)
                    {
                        if (pull.Started)
                        {
                            broadcaster.Broadcast(CreateEvent("ended", session.DeviceId, pull));
                        }

                        pull = null;
                    }

                    if (pull is null)
                    {
                        pull = new Pull(nowUtc);
                        _pulls[session.DeviceId] = pull;
                    }

                    pull.Job = job ?? pull.Job;
                    pull.UserId = session.UserId.ToString("N");
                    pull.Info = new TranscodingInfo
                    {
                        Bitrate = info.Bitrate,
                        Width = info.Width,
                        Height = info.Height,
                        VideoCodec = info.VideoCodec,
                        AudioCodec = info.AudioCodec,
                        Container = info.Container,
                        CompletionPercentage = info.CompletionPercentage,
                    };
                    if (!pull.Started && nowUtc - pull.FirstSeenUtc >= MinAge)
                    {
                        pull.Started = true;
                        broadcaster.Broadcast(CreateEvent("started", session.DeviceId, pull));
                    }
                }

                foreach (var (deviceId, pull) in _pulls.Where(p => !seen.Contains(p.Key)).ToList())
                {
                    _pulls.Remove(deviceId);
                    if (pull.Started)
                    {
                        broadcaster.Broadcast(CreateEvent("ended", deviceId, pull));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to scan sessions for download.transcoded");
        }
    }

    private static SseEvent CreateEvent(string phase, string deviceId, Pull pull) => new()
    {
        EventType = "download.transcoded",
        Phase = phase,
        UserId = pull.UserId,
        DeviceId = deviceId,
        ItemId = pull.Job?.MediaSource?.Id,
        Bitrate = pull.Info?.Bitrate,
        Width = pull.Info?.Width,
        Height = pull.Info?.Height,
        VideoCodec = pull.Info?.VideoCodec,
        AudioCodec = pull.Info?.AudioCodec,
        Container = pull.Info?.Container,
        BytesTranscoded = pull.Job?.BytesTranscoded,
        CompletionPercentage = pull.Info?.CompletionPercentage,
    };

    private TranscodingJob? FindJob(string deviceId)
    {
        if (_jobsUnavailable)
        {
            return null;
        }

        try
        {
            _jobsField ??= transcodeManager.GetType().GetField(ActiveJobsField, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(transcodeManager.GetType().FullName, ActiveJobsField);
            var jobs = (List<TranscodingJob>)_jobsField.GetValue(transcodeManager)!;
            lock (jobs)
            {
                return jobs.LastOrDefault(j => !j.HasExited && string.Equals(j.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
            }
        }
        catch (Exception ex)
        {
            _jobsUnavailable = true;
            logger.LogDebug(ex, "Transcoding jobs are not readable; download.transcoded will omit itemId and bytesTranscoded");
            return null;
        }
    }

    private sealed class Pull(DateTime firstSeenUtc)
    {
        public DateTime FirstSeenUtc { get; } = firstSeenUtc;

        public bool Started { get; set; }

        public string? UserId { get; set; }

        public TranscodingInfo? Info { get; set; }

        public TranscodingJob? Job { get; set; }
    }
}
