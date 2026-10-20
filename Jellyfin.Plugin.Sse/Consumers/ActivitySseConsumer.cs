using System.Text.RegularExpressions;

using MediaServer.Sse.Core.Broadcasting;
using MediaServer.Sse.Core.Models;

using Jellyfin.Data.Events;
using MediaBrowser.Model.Activity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Sse.Consumers;

public partial class ActivitySseConsumer(
    IActivityManager activityManager,
    ISseEventBroadcaster broadcaster,
    ILogger<ActivitySseConsumer> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        activityManager.EntryCreated += OnEntryCreated;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        activityManager.EntryCreated -= OnEntryCreated;
        return Task.CompletedTask;
    }

    private void OnEntryCreated(object? sender, GenericEventArgs<ActivityLogEntry> e)
    {
        try
        {
            var entry = e.Argument;
            if (entry.Type != "UserDownloadingContent")
            {
                return;
            }

            var overview = AppDevicePattern().Match(entry.ShortOverview ?? string.Empty);
            broadcaster.Broadcast(new SseEvent
            {
                EventType = "library.item.downloaded",
                UserId = entry.UserId.ToString("N"),
                ItemId = entry.ItemId,
                ItemName = entry.Name,
                App = overview.Success ? overview.Groups["app"].Value : null,
                Device = overview.Success ? overview.Groups["device"].Value : null,
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast library.item.downloaded");
        }
    }

    // ShortOverview is the server-localized "AppDeviceValues" string ("App: {0}, Device: {1}"
    // in en-US, "应用：{0}，设备：{1}" in zh-CN), so match on separators, not label text.
    [GeneratedRegex(@"^[^:：]*[:：]\s*(?<app>.+)[,，]\s*[^:：,，]*[:：]\s*(?<device>.+)$")]
    private static partial Regex AppDevicePattern();
}
