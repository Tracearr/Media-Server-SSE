using MediaServer.Sse.Core.Broadcasting;
using Jellyfin.Plugin.Sse.Consumers;
using MediaServer.Sse.Core.Models;
using Jellyfin.Data.Events;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MediaServer.Sse.Tests.Consumers;

public class TaskConsumerTests
{
    private readonly Mock<ISseEventBroadcaster> _broadcaster;
    private readonly Mock<ITaskManager> _taskManager;
    private readonly Mock<IScheduledTaskWorker> _worker;
    private readonly TaskSseConsumer _consumer;

    public TaskConsumerTests()
    {
        _broadcaster = new Mock<ISseEventBroadcaster>();
        _taskManager = new Mock<ITaskManager>();
        _worker = new Mock<IScheduledTaskWorker>();
        _worker.SetupGet(w => w.Id).Returns("task-1");
        _worker.SetupGet(w => w.Name).Returns("Scan Media Library");
        _worker.SetupGet(w => w.Category).Returns("Library");
        _consumer = new TaskSseConsumer(
            _taskManager.Object, _broadcaster.Object, NullLogger<TaskSseConsumer>.Instance);
    }

    [Fact]
    public async Task TaskExecuting_BroadcastsTaskStarted()
    {
        await _consumer.StartAsync(CancellationToken.None);

        _taskManager.Raise(
            m => m.TaskExecuting += null,
            _taskManager.Object,
            new GenericEventArgs<IScheduledTaskWorker>(_worker.Object));

        _broadcaster.Verify(b => b.Broadcast(It.Is<SseEvent>(e =>
            e.EventType == "task.started" &&
            e.TaskId == "task-1" &&
            e.TaskName == "Scan Media Library" &&
            e.TaskCategory == "Library")), Times.Once);
    }

    [Fact]
    public async Task TaskProgress_BroadcastsThrottledProgress()
    {
        await _consumer.StartAsync(CancellationToken.None);

        _taskManager.Raise(
            m => m.TaskExecuting += null,
            _taskManager.Object,
            new GenericEventArgs<IScheduledTaskWorker>(_worker.Object));

        _worker.Raise(w => w.TaskProgress += null, _worker.Object, new GenericEventArgs<double>(25.0));
        _worker.Raise(w => w.TaskProgress += null, _worker.Object, new GenericEventArgs<double>(25.2));

        _broadcaster.Verify(b => b.Broadcast(It.Is<SseEvent>(e =>
            e.EventType == "task.progress" &&
            e.TaskId == "task-1" &&
            e.Progress == 25.0)), Times.Once);
        _broadcaster.Verify(b => b.Broadcast(It.Is<SseEvent>(e =>
            e.EventType == "task.progress" &&
            e.Progress == 25.2)), Times.Never);
    }

    [Fact]
    public async Task TaskCompleted_BroadcastsCompletionWithStatus()
    {
        await _consumer.StartAsync(CancellationToken.None);

        var result = new TaskResult { Status = TaskCompletionStatus.Completed };

        _taskManager.Raise(
            m => m.TaskCompleted += null,
            _taskManager.Object,
            new TaskCompletionEventArgs(_worker.Object, result));

        _broadcaster.Verify(b => b.Broadcast(It.Is<SseEvent>(e =>
            e.EventType == "task.completed" &&
            e.TaskId == "task-1" &&
            e.State == "Completed")), Times.Once);
    }

    [Fact]
    public async Task StopAsync_UnsubscribesFromManager()
    {
        await _consumer.StartAsync(CancellationToken.None);
        await _consumer.StopAsync(CancellationToken.None);

        _taskManager.Raise(
            m => m.TaskExecuting += null,
            _taskManager.Object,
            new GenericEventArgs<IScheduledTaskWorker>(_worker.Object));

        _broadcaster.Verify(b => b.Broadcast(It.IsAny<SseEvent>()), Times.Never);
    }
}
