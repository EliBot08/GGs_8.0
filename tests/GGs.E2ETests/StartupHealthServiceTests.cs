using System;
using System.IO;
using System.Text.Json;
using Xunit;
using GGs.Desktop.Services;

namespace GGs.E2ETests;

public class StartupHealthServiceTests
{
    [Fact]
    public void CrashLoop_Detected_After_Three_Unclean_Starts_In_Window()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "ggs_health_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        Environment.SetEnvironmentVariable("GGS_HEALTH_DIR", tmp);
        try
        {
            var svc = new StartupHealthService();
            // Simulate 3 quick unclean startups in the last minute
            svc.OnStartupBegin();
            // No clean exit for first
            var svc2 = new StartupHealthService();
            svc2.OnStartupBegin();
            // No clean exit for second
            var svc3 = new StartupHealthService();
            svc3.OnStartupBegin();
            // Do not mark clean exits -> unclean

            var loop = svc3.IsCrashLoop(thresholdCount: 3, windowSeconds: 120);
            Assert.True(loop);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GGS_HEALTH_DIR", null);
            try { Directory.Delete(tmp, true); } catch { }
        }
    }

    [Fact]
    public void Clean_Exit_Breaks_Loop()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "ggs_health_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        Environment.SetEnvironmentVariable("GGS_HEALTH_DIR", tmp);
        try
        {
            var svc = new StartupHealthService();
            svc.OnStartupBegin();
            svc.MarkCleanExit();
            // Now only 1 clean entry
            var loop = svc.IsCrashLoop(thresholdCount: 3, windowSeconds: 120);
            Assert.False(loop);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GGS_HEALTH_DIR", null);
            try { Directory.Delete(tmp, true); } catch { }
        }
    }
}

