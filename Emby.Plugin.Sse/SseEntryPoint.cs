using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Emby.Plugin.Sse.Logging;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Events;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;
using MediaServer.Sse.Core.Broadcasting;
using MediaServer.Sse.Core.Models;
using MediaServer.Sse.Core.Stats;

namespace Emby.Plugin.Sse
{
    public class SseEntryPoint : IServerEntryPoint
    {
        // Matches the 6s resolution Plex's statistics endpoints report at
        private const int StatsIntervalMs = 6_000;

        private readonly ISessionManager _sessionManager;
        private readonly ILibraryManager _libraryManager;
        private readonly ITaskManager _taskManager;
        private readonly ILogger _logger;
        private readonly ServerStatsSampler _sampler = new ServerStatsSampler();
        private readonly TaskProgressThrottle _throttle = new TaskProgressThrottle();
        private readonly HashSet<string> _progressHooked = new HashSet<string>();
        private readonly object _progressHookLock = new object();
        private SseEventBroadcaster? _broadcaster;
        private Timer? _statsTimer;

        public static ISseEventBroadcaster? Broadcaster { get; private set; }

        public SseEntryPoint(
            ISessionManager sessionManager,
            ILibraryManager libraryManager,
            ITaskManager taskManager,
            ILogManager logManager)
        {
            _sessionManager = sessionManager;
            _libraryManager = libraryManager;
            _taskManager = taskManager;
            _logger = logManager.GetLogger(GetType().Name);
        }

        public void Run()
        {
            _broadcaster = new SseEventBroadcaster(
                new EmbyLoggerAdapter<SseEventBroadcaster>(_logger));
            Broadcaster = _broadcaster;

            _sessionManager.PlaybackStart += OnPlaybackStart;
            _sessionManager.PlaybackProgress += OnPlaybackProgress;
            _sessionManager.PlaybackStopped += OnPlaybackStopped;
            _sessionManager.SessionStarted += OnSessionStarted;
            _sessionManager.SessionEnded += OnSessionEnded;
            _libraryManager.ItemAdded += OnItemAdded;
            _libraryManager.ItemRemoved += OnItemRemoved;
            _taskManager.TaskExecuting += OnTaskExecuting;
            _taskManager.TaskCompleted += OnTaskCompleted;

            _statsTimer = new Timer(OnStatsTick, null, StatsIntervalMs, StatsIntervalMs);

            _logger.Info("SSE plugin started");
        }

        public void Dispose()
        {
            _sessionManager.PlaybackStart -= OnPlaybackStart;
            _sessionManager.PlaybackProgress -= OnPlaybackProgress;
            _sessionManager.PlaybackStopped -= OnPlaybackStopped;
            _sessionManager.SessionStarted -= OnSessionStarted;
            _sessionManager.SessionEnded -= OnSessionEnded;
            _libraryManager.ItemAdded -= OnItemAdded;
            _libraryManager.ItemRemoved -= OnItemRemoved;
            _taskManager.TaskExecuting -= OnTaskExecuting;
            _taskManager.TaskCompleted -= OnTaskCompleted;

            _statsTimer?.Dispose();
            _statsTimer = null;

            _broadcaster?.Dispose();
            Broadcaster = null;

            _logger.Info("SSE plugin stopped");
        }

        private void OnTaskExecuting(object sender, GenericEventArgs<IScheduledTaskWorker> e)
        {
            try
            {
                var worker = e.Argument;

                // Workers are long-lived, one per task; hook progress once each
                var hook = false;
                lock (_progressHookLock)
                {
                    hook = _progressHooked.Add(worker.Id);
                }

                if (hook)
                {
                    worker.TaskProgress += (_, progressArgs) => OnTaskProgress(worker, progressArgs.Argument);
                }

                _broadcaster?.Broadcast(new SseEvent
                {
                    EventType = "task.started",
                    TaskId = worker.Id,
                    TaskName = worker.Name,
                    TaskCategory = worker.Category,
                });
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Failed to broadcast task.started", ex);
            }
        }

        private void OnTaskProgress(IScheduledTaskWorker worker, double progress)
        {
            try
            {
                if (!_throttle.ShouldEmit(worker.Id, progress, DateTime.UtcNow))
                {
                    return;
                }

                _broadcaster?.Broadcast(new SseEvent
                {
                    EventType = "task.progress",
                    TaskId = worker.Id,
                    TaskName = worker.Name,
                    Progress = progress,
                });
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Failed to broadcast task.progress", ex);
            }
        }

        private void OnTaskCompleted(object sender, TaskCompletionEventArgs e)
        {
            try
            {
                var worker = e.Task;
                _throttle.Clear(worker.Id);

                _broadcaster?.Broadcast(new SseEvent
                {
                    EventType = "task.completed",
                    TaskId = worker.Id,
                    TaskName = worker.Name,
                    State = e.Result.Status.ToString(),
                });
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Failed to broadcast task.completed", ex);
            }
        }

        private void OnStatsTick(object state)
        {
            try
            {
                // Sample every tick so CPU deltas stay accurate, broadcast
                // only when someone is listening
                var sample = _sampler.Sample();
                if (sample == null || _broadcaster == null || _broadcaster.SubscriberCount == 0)
                {
                    return;
                }

                _broadcaster.Broadcast(new SseEvent
                {
                    EventType = "server.stats",
                    At = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    HostCpuUtilization = sample.HostCpuUtilization,
                    ProcessCpuUtilization = sample.ProcessCpuUtilization,
                    HostMemoryUtilization = sample.HostMemoryUtilization,
                    ProcessMemoryUtilization = sample.ProcessMemoryUtilization,
                });
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Failed to broadcast server.stats", ex);
            }
        }

        private void OnPlaybackStart(object sender, PlaybackProgressEventArgs e)
        {
            var evt = TryCreatePlaybackEvent(e, "playing", "playing");
            if (evt != null)
            {
                _broadcaster?.Broadcast(evt);
            }
        }

        private void OnPlaybackProgress(object sender, PlaybackProgressEventArgs e)
        {
            string eventType;
            string state;
            if (e.IsPaused)
            {
                eventType = "paused";
                state = "paused";
            }
            else
            {
                eventType = "progress";
                state = "playing";
            }

            var evt = TryCreatePlaybackEvent(e, eventType, state);
            if (evt != null)
            {
                _broadcaster?.Broadcast(evt);
            }
        }

        private void OnPlaybackStopped(object sender, PlaybackStopEventArgs e)
        {
            var evt = TryCreatePlaybackEvent(e, "stopped", "stopped");
            if (evt != null)
            {
                evt.PlayedToCompletion = e.PlayedToCompletion;
                _broadcaster?.Broadcast(evt);
            }
        }

        private void OnSessionStarted(object sender, SessionEventArgs e)
        {
            var session = e.SessionInfo;
            _broadcaster?.Broadcast(new SseEvent
            {
                EventType = "session.start",
                SessionId = session.Id,
                UserId = session.UserId
            });
        }

        private void OnSessionEnded(object sender, SessionEventArgs e)
        {
            var session = e.SessionInfo;
            _broadcaster?.Broadcast(new SseEvent
            {
                EventType = "session.end",
                SessionId = session.Id,
                UserId = session.UserId
            });
        }

        private void OnItemAdded(object sender, ItemChangeEventArgs e) => HandleLibraryEvent(e, "library.item.added");

        private void OnItemRemoved(object sender, ItemChangeEventArgs e) => HandleLibraryEvent(e, "library.item.removed");

        private void HandleLibraryEvent(ItemChangeEventArgs args, string eventType)
        {
            // Never let a malformed item shape throw into the library manager's event dispatch.
            try
            {
                var evt = TryCreateLibraryEvent(args, eventType);
                if (evt != null)
                {
                    _broadcaster?.Broadcast(evt);
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException($"Failed to emit {eventType} event", ex);
            }
        }

        private static SseEvent? TryCreateLibraryEvent(ItemChangeEventArgs args, string eventType)
        {
            var item = args.Item;
            if (item == null || item.IsThemeMedia || item.IsVirtualItem)
                return null;

            // InternalId, not the Guid: Emby's REST API identifies items by the numeric
            // internal id, so this is the only form consumers can correlate.
            return new SseEvent
            {
                EventType = eventType,
                ItemId = item.InternalId.ToString(CultureInfo.InvariantCulture),
                ItemType = item.GetClientTypeName(),
                ParentId = args.Parent?.InternalId.ToString(CultureInfo.InvariantCulture)
            };
        }

        private static SseEvent? TryCreatePlaybackEvent(PlaybackProgressEventArgs args, string eventType, string state)
        {
            if (args.Users == null || args.Users.Count == 0)
                return null;
            if (args.Item == null || args.Item.IsThemeMedia)
                return null;
            if (args.Session == null)
                return null;

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
}
