using System.Collections.Concurrent;

using MediaServer.Sse.Core.Broadcasting;
using MediaServer.Sse.Core.Models;
using MediaServer.Sse.Core.Stats;

using Jellyfin.Data.Events;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Sse.Consumers;

public class TaskSseConsumer(
    ITaskManager taskManager,
    ISseEventBroadcaster broadcaster,
    ILogger<TaskSseConsumer> logger) : IHostedService
{
    private readonly TaskProgressThrottle _throttle = new();
    private readonly ConcurrentDictionary<string, byte> _progressHooked = new();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        taskManager.TaskExecuting += OnTaskExecuting;
        taskManager.TaskCompleted += OnTaskCompleted;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        taskManager.TaskExecuting -= OnTaskExecuting;
        taskManager.TaskCompleted -= OnTaskCompleted;
        return Task.CompletedTask;
    }

    private void OnTaskExecuting(object? sender, GenericEventArgs<IScheduledTaskWorker> e)
    {
        try
        {
            var worker = e.Argument;

            // Workers are long-lived, one per task; hook progress once each
            if (_progressHooked.TryAdd(worker.Id, 1))
            {
                worker.TaskProgress += (_, progressArgs) => OnTaskProgress(worker, progressArgs.Argument);
            }

            broadcaster.Broadcast(new SseEvent
            {
                EventType = "task.started",
                TaskId = worker.Id,
                TaskName = worker.Name,
                TaskCategory = worker.Category,
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast task.started");
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

            broadcaster.Broadcast(new SseEvent
            {
                EventType = "task.progress",
                TaskId = worker.Id,
                TaskName = worker.Name,
                Progress = progress,
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast task.progress");
        }
    }

    private void OnTaskCompleted(object? sender, TaskCompletionEventArgs e)
    {
        try
        {
            var worker = e.Task;
            _throttle.Clear(worker.Id);

            broadcaster.Broadcast(new SseEvent
            {
                EventType = "task.completed",
                TaskId = worker.Id,
                TaskName = worker.Name,
                State = e.Result.Status.ToString(),
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast task.completed");
        }
    }
}
