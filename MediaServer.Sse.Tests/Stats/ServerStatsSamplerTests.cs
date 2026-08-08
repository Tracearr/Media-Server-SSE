using MediaServer.Sse.Core.Stats;
using Xunit;

namespace MediaServer.Sse.Tests.Stats;

public class ServerStatsSamplerTests
{
    [Fact]
    public void Sample_FirstCall_PrimesAndReturnsNull()
    {
        var sampler = new ServerStatsSampler();

        Assert.Null(sampler.Sample());
    }

    [Fact]
    public async Task Sample_SecondCall_ReturnsProcessCpuWithinRange()
    {
        var sampler = new ServerStatsSampler();
        sampler.Sample();
        await Task.Delay(50);

        var sample = sampler.Sample();

        Assert.NotNull(sample);
        Assert.NotNull(sample!.ProcessCpuUtilization);
        Assert.InRange(sample.ProcessCpuUtilization!.Value, 0, 100);
    }

    [Fact]
    public async Task Sample_OnLinux_ReturnsHostMetricsWithinRange()
    {
        if (!File.Exists("/proc/stat"))
        {
            return;
        }

        var sampler = new ServerStatsSampler();
        sampler.Sample();
        await Task.Delay(50);

        var sample = sampler.Sample();

        Assert.NotNull(sample);
        Assert.NotNull(sample!.HostCpuUtilization);
        Assert.InRange(sample.HostCpuUtilization!.Value, 0, 100);
        Assert.NotNull(sample.HostMemoryUtilization);
        Assert.InRange(sample.HostMemoryUtilization!.Value, 0, 100);
        Assert.NotNull(sample.ProcessMemoryUtilization);
        Assert.InRange(sample.ProcessMemoryUtilization!.Value, 0, 100);
    }
}
