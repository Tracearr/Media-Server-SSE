using System;
using System.Collections.Generic;
using System.Globalization;
using Emby.Plugin.Sse.Logging;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Logging;
using MediaServer.Sse.Core.Broadcasting;
using MediaServer.Sse.Core.Models;

namespace Emby.Plugin.Sse
{
    public class SseEntryPoint : IServerEntryPoint
    {
        private readonly ISessionManager _sessionManager;
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger _logger;
        private SseEventBroadcaster? _broadcaster;

        public static ISseEventBroadcaster? Broadcaster { get; private set; }

        public SseEntryPoint(ISessionManager sessionManager, ILibraryManager libraryManager, ILogManager logManager)
        {
            _sessionManager = sessionManager;
            _libraryManager = libraryManager;
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

            _broadcaster?.Dispose();
            Broadcaster = null;

            _logger.Info("SSE plugin stopped");
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
