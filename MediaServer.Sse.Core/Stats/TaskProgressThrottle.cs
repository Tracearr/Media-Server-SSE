namespace MediaServer.Sse.Core.Stats;

/// <summary>
/// Per-task gate for progress events: emit when progress moved at least one
/// percent or enough time passed, so chatty tasks (library scans report every
/// item) cannot flood the bounded channels.
/// </summary>
public sealed class TaskProgressThrottle
{
    private readonly object _lock = new object();
    private readonly Dictionary<string, (double Progress, DateTime AtUtc)> _last =
        new Dictionary<string, (double, DateTime)>();

    private readonly double _minProgressDelta;
    private readonly TimeSpan _minInterval;

    public TaskProgressThrottle(double minProgressDelta = 1.0, int minIntervalMs = 2000)
    {
        _minProgressDelta = minProgressDelta;
        _minInterval = TimeSpan.FromMilliseconds(minIntervalMs);
    }

    public bool ShouldEmit(string taskId, double progress, DateTime nowUtc)
    {
        lock (_lock)
        {
            if (_last.TryGetValue(taskId, out var last)
                && Math.Abs(progress - last.Progress) < _minProgressDelta
                && nowUtc - last.AtUtc < _minInterval)
            {
                return false;
            }

            _last[taskId] = (progress, nowUtc);
            return true;
        }
    }

    public void Clear(string taskId)
    {
        lock (_lock)
        {
            _last.Remove(taskId);
        }
    }
}
