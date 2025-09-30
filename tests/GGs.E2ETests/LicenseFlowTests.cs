using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using GGs.Desktop.Services;
using GGs.Shared.Enums;
using GGs.Shared.Licensing;
using Xunit;

namespace GGs.E2ETests;

public class LicenseFlowTests
{
    [Fact]
    public async Task DemoKeyActivation_ShouldSucceed_AndPersist()
    {
        // Arrange
        var tmpFile = Path.Combine(Path.GetTempPath(), "GGsTests", $"license_{Guid.NewGuid():N}.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(tmpFile)!);
        Environment.SetEnvironmentVariable("GGS_LICENSE_PATH", tmpFile);
        try
        {
            var svc = new LicenseService();
            var key = "1234567890ABCDEF"; // 16-char demo key

            // Act
            var (ok, msg) = await svc.ValidateAndSaveFromTextAsync(key);

            // Assert
            Assert.True(ok);
            Assert.NotNull(svc.CurrentPayload);
            Assert.True(svc.CurrentPayload!.IsAdminKey);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GGS_LICENSE_PATH", null);
            if (File.Exists(tmpFile)) File.Delete(tmpFile);
        }
    }

    [Fact]
    public async Task InvalidJson_ShouldFail_Gracefully()
    {
        var svc = new LicenseService();
        var (ok, msg) = await svc.ValidateAndSaveFromTextAsync("{ not json }");
        Assert.False(ok);
        Assert.Contains("Invalid", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AppConfig_DemoMode_ShouldFollowEnv()
    {
        try
        {
            Environment.SetEnvironmentVariable("GGS_DEMO_MODE", "true");
            Assert.True(AppConfig.DemoMode);
            Environment.SetEnvironmentVariable("GGS_DEMO_MODE", "false");
            Assert.False(AppConfig.DemoMode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GGS_DEMO_MODE", null);
        }
    }
}

