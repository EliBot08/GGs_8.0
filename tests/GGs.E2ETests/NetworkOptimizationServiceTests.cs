using System;
using System.IO;
using System.Threading.Tasks;
using GGs.Desktop.Services;
using Xunit;

namespace GGs.E2ETests;

public class NetworkOptimizationServiceTests
{
    [Fact]
    public async Task ApplyRollback_Simulation_ShouldSucceedAcrossAdapters()
    {
        // Arrange
        var temp = Path.Combine(Path.GetTempPath(), "ggs_net_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        Environment.SetEnvironmentVariable("GGS_DATA_DIR", temp);
        Environment.SetEnvironmentVariable("GGS_NETWORK_SIMULATE", "1");
        try
        {
            var svc = new NetworkOptimizationService();
            var adapters = new[] { "Ethernet0", "Wi-Fi" };
            var profile = new NetworkProfile
            {
                Name = "Low-Latency Gaming (Test)",
                Risk = NetRiskLevel.Medium,
                Autotuning = TcpAutotuningLevel.Normal
            };
            foreach (var a in adapters) profile.DnsPerAdapter[a] = new[] { "1.1.1.1" };

            // Act
            var res = await svc.ApplyProfileAsync(profile);
            Assert.True(res.Success);
            Assert.True(res.SnapshotId.HasValue);

            // Act - rollback
            var ok = await svc.RollbackLastAsync();
            Assert.True(ok);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GGS_NETWORK_SIMULATE", null);
            Environment.SetEnvironmentVariable("GGS_DATA_DIR", null);
            try { Directory.Delete(temp, true); } catch { }
        }
    }

    [Fact]
    public async Task Preflight_DryRun_WhenNotSimulating_ShouldNotChangeSystemAndIndicateSuccess()
    {
        var temp = Path.Combine(Path.GetTempPath(), "ggs_net_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        Environment.SetEnvironmentVariable("GGS_DATA_DIR", temp);
        Environment.SetEnvironmentVariable("GGS_NETWORK_SIMULATE", null);
        try
        {
            var svc = new NetworkOptimizationService();
            var profile = new NetworkProfile { Name = "Balanced Default", Autotuning = TcpAutotuningLevel.Normal, Risk = NetRiskLevel.Low };
            var res = await svc.ApplyProfileAsync(profile, dryRun: true);
            Assert.True(res.Success);
            Assert.Contains("Dry run", res.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GGS_DATA_DIR", null);
            try { Directory.Delete(temp, true); } catch { }
        }
    }
}

