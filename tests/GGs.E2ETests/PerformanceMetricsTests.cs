using System;
using System.Threading.Tasks;
using GGs.Desktop.Services;
using Xunit;

namespace GGs.E2ETests;

public class PerformanceMetricsTests
{
    [Fact]
    public async Task SystemMonitorService_ShouldProduceReasonableStats()
    {
        var svc = new SystemMonitorService();
        SystemStats? last = null;
        var tcs = new TaskCompletionSource<bool>();
        svc.StatsUpdated += (s, e) => { last = e.Stats; tcs.TrySetResult(true); };
        svc.Start();
        await Task.WhenAny(tcs.Task, Task.Delay(3000));
        svc.Stop();
        Assert.NotNull(last);
        Assert.InRange(last!.CpuUsage, 0, 100);
        Assert.InRange(last!.GpuUsage >= 0 ? last!.GpuUsage : 0, 0, 100);
        Assert.InRange(last!.RamUsage.UsagePercent >= 0 ? last!.RamUsage.UsagePercent : 0, 0, 100);
        Assert.InRange(last!.DiskUsage >= 0 ? last!.DiskUsage : 0, 0, 100);
    }
}

