using System.Diagnostics;

namespace MediaServer.Sse.Core.Stats;

public class ServerStatsSample
{
    public double? HostCpuUtilization { get; set; }

    public double? ProcessCpuUtilization { get; set; }

    public double? HostMemoryUtilization { get; set; }

    public double? ProcessMemoryUtilization { get; set; }
}

/// <summary>
/// Samples process and host CPU/RAM utilization. CPU values are deltas
/// between consecutive calls, so the first call primes state and returns
/// null. Host metrics read /proc and are null off Linux; inside a container
/// /proc reports the host, matching what Plex's resources endpoint shows.
/// </summary>
public sealed class ServerStatsSampler
{
    private const string ProcStatPath = "/proc/stat";
    private const string ProcMeminfoPath = "/proc/meminfo";

    private DateTime _lastSampleUtc;
    private TimeSpan _lastProcessCpu;
    private long _lastHostIdle;
    private long _lastHostTotal;
    private bool _primed;

    public ServerStatsSample? Sample()
    {
        var now = DateTime.UtcNow;
        TimeSpan processCpu;
        try
        {
            using var process = Process.GetCurrentProcess();
            processCpu = process.TotalProcessorTime;
        }
        catch
        {
            return null;
        }

        var host = ReadHostCpu();

        if (!_primed)
        {
            _lastSampleUtc = now;
            _lastProcessCpu = processCpu;
            if (host.HasValue)
            {
                _lastHostIdle = host.Value.Idle;
                _lastHostTotal = host.Value.Total;
            }

            _primed = true;
            return null;
        }

        var elapsedMs = (now - _lastSampleUtc).TotalMilliseconds;
        if (elapsedMs <= 0)
        {
            return null;
        }

        var sample = new ServerStatsSample();

        var cpuMs = (processCpu - _lastProcessCpu).TotalMilliseconds;
        sample.ProcessCpuUtilization = Clamp(cpuMs / elapsedMs / Environment.ProcessorCount * 100);

        if (host.HasValue && _lastHostTotal > 0)
        {
            var totalDelta = host.Value.Total - _lastHostTotal;
            var idleDelta = host.Value.Idle - _lastHostIdle;
            if (totalDelta > 0)
            {
                sample.HostCpuUtilization = Clamp(100.0 * (totalDelta - idleDelta) / totalDelta);
            }
        }

        var memory = ReadHostMemory();
        if (memory.HasValue)
        {
            var (totalBytes, availableBytes) = memory.Value;
            if (totalBytes > 0)
            {
                sample.HostMemoryUtilization = Clamp(100.0 * (totalBytes - availableBytes) / totalBytes);
                try
                {
                    using var process = Process.GetCurrentProcess();
                    sample.ProcessMemoryUtilization = Clamp(100.0 * process.WorkingSet64 / totalBytes);
                }
                catch
                {
                    // WorkingSet unavailable; host memory alone is still useful
                }
            }
        }

        _lastSampleUtc = now;
        _lastProcessCpu = processCpu;
        if (host.HasValue)
        {
            _lastHostIdle = host.Value.Idle;
            _lastHostTotal = host.Value.Total;
        }

        return sample;
    }

    private static double Clamp(double value)
    {
        if (value < 0)
        {
            return 0;
        }

        return value > 100 ? 100 : Math.Round(value, 3);
    }

    private static (long Idle, long Total)? ReadHostCpu()
    {
        try
        {
            if (!File.Exists(ProcStatPath))
            {
                return null;
            }

            using var reader = new StreamReader(ProcStatPath);
            var line = reader.ReadLine();
            if (line == null || !line.StartsWith("cpu ", StringComparison.Ordinal))
            {
                return null;
            }

            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5)
            {
                return null;
            }

            long total = 0;
            for (var i = 1; i < parts.Length; i++)
            {
                if (long.TryParse(parts[i], out var value))
                {
                    total += value;
                }
            }

            // idle + iowait both count as not-busy
            long idle = 0;
            if (long.TryParse(parts[4], out var idleValue))
            {
                idle += idleValue;
            }

            if (parts.Length > 5 && long.TryParse(parts[5], out var iowaitValue))
            {
                idle += iowaitValue;
            }

            return (idle, total);
        }
        catch
        {
            return null;
        }
    }

    private static (long TotalBytes, long AvailableBytes)? ReadHostMemory()
    {
        try
        {
            if (!File.Exists(ProcMeminfoPath))
            {
                return null;
            }

            long totalKb = 0;
            long availableKb = 0;
            foreach (var line in File.ReadLines(ProcMeminfoPath))
            {
                if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
                {
                    totalKb = ParseMeminfoKb(line);
                }
                else if (line.StartsWith("MemAvailable:", StringComparison.Ordinal))
                {
                    availableKb = ParseMeminfoKb(line);
                }

                if (totalKb > 0 && availableKb > 0)
                {
                    break;
                }
            }

            if (totalKb <= 0)
            {
                return null;
            }

            return (totalKb * 1024, availableKb * 1024);
        }
        catch
        {
            return null;
        }
    }

    private static long ParseMeminfoKb(string line)
    {
        var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && long.TryParse(parts[1], out var value) ? value : 0;
    }
}
