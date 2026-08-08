using MediaServer.Sse.Core.Stats;
using Xunit;

namespace MediaServer.Sse.Tests.Stats;

public class TaskProgressThrottleTests
{
    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ShouldEmit_FirstProgress_Emits()
    {
        var throttle = new TaskProgressThrottle();

        Assert.True(throttle.ShouldEmit("t1", 0, Start));
    }

    [Fact]
    public void ShouldEmit_SmallFastDelta_Suppresses()
    {
        var throttle = new TaskProgressThrottle();
        throttle.ShouldEmit("t1", 10, Start);

        Assert.False(throttle.ShouldEmit("t1", 10.5, Start.AddMilliseconds(500)));
    }

    [Fact]
    public void ShouldEmit_ProgressJump_Emits()
    {
        var throttle = new TaskProgressThrottle();
        throttle.ShouldEmit("t1", 10, Start);

        Assert.True(throttle.ShouldEmit("t1", 11.5, Start.AddMilliseconds(500)));
    }

    [Fact]
    public void ShouldEmit_AfterInterval_EmitsEvenWithoutDelta()
    {
        var throttle = new TaskProgressThrottle();
        throttle.ShouldEmit("t1", 10, Start);

        Assert.True(throttle.ShouldEmit("t1", 10.1, Start.AddSeconds(3)));
    }

    [Fact]
    public void ShouldEmit_TasksThrottleIndependently()
    {
        var throttle = new TaskProgressThrottle();
        throttle.ShouldEmit("t1", 10, Start);

        Assert.True(throttle.ShouldEmit("t2", 10.1, Start.AddMilliseconds(100)));
    }

    [Fact]
    public void Clear_ResetsTask_NextProgressEmits()
    {
        var throttle = new TaskProgressThrottle();
        throttle.ShouldEmit("t1", 10, Start);
        throttle.Clear("t1");

        Assert.True(throttle.ShouldEmit("t1", 10.1, Start.AddMilliseconds(100)));
    }
}
