using System;
using System.Collections.Generic;
using GGs.Desktop.Services;
using Xunit;

namespace GGs.E2ETests;

public class GameDetectionServiceTests
{
    [Fact]
    public void DetectGameByNames_ShouldMatchBuiltInProfile()
    {
        var svc = new GameDetectionService();
        var profile = svc.DetectGameByNames(new[] { "cs2", "steam.exe" });
        Assert.NotNull(profile);
        Assert.Contains("cs2", profile!.ProcessName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CloseApps_ShouldNotCloseCriticalProcesses()
    {
        var actions = new DefaultGameOptimizationActions();
        var result = actions.CloseApps(new[] { "winlogon.exe", "explorer.exe" });
        foreach (var r in result)
        {
            Assert.NotEqual("winlogon.exe", r.name, StringComparer.OrdinalIgnoreCase);
            Assert.NotEqual("explorer.exe", r.name, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Start_DisabledEnv_ShouldNotThrow()
    {
        Environment.SetEnvironmentVariable("GGS_GAME_DETECTION_ENABLED", "false");
        var svc = new GameDetectionService();
        svc.Start();
        svc.Stop();
        Environment.SetEnvironmentVariable("GGS_GAME_DETECTION_ENABLED", null);
    }
}

