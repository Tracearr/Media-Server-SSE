using MediaServer.Sse.Core.Broadcasting;
using MediaServer.Sse.Core.Models;
using MediaServer.Sse.Core.Stats;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Sse.Consumers;

public class ServerStatsSseService(
    ISseEventBroadcaster broadcaster,
    ILogger<ServerStatsSseService> logger) : IHostedService, IDisposable
{
    // Matches the 6s resolution Plex's statistics endpoints report at, so
    // dashboards chart all server types on the same cadence
    private const int SampleIntervalMs = 6_000;

    private readonly ServerStatsSampler _sampler = new();
    private Timer? _timer;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(OnTick, null, SampleIntervalMs, SampleIntervalMs);
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

    private void OnTick(object? state)
    {
        try
        {
            // Sample every tick so CPU deltas stay accurate, broadcast only
            // when someone is listening
            var sample = _sampler.Sample();
            if (sample == null || broadcaster.SubscriberCount == 0)
            {
                return;
            }

            broadcaster.Broadcast(new SseEvent
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
            logger.LogWarning(ex, "Failed to broadcast server.stats");
        }
    }
}
