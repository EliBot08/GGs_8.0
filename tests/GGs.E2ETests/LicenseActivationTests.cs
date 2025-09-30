using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using GGs.Desktop.Services;

namespace GGs.E2ETests;

public class LicenseActivationTests
{
    [Fact]
    public async Task DemoKeyActivation_ShouldPersistPayload()
    {
        var tmpFile = Path.Combine(Path.GetTempPath(), "GGsTests", $"license_{Guid.NewGuid():N}.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(tmpFile)!);
        Environment.SetEnvironmentVariable("GGS_LICENSE_PATH", tmpFile);
        try
        {
            var svc = new LicenseService();
            var (ok, msg) = await svc.ValidateAndSaveFromTextAsync("1234567890ABCDEF");
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
    public async Task InvalidJson_ShouldFailGracefully()
    {
        var tmpFile = Path.Combine(Path.GetTempPath(), "GGsTests", $"license_{Guid.NewGuid():N}.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(tmpFile)!);
        Environment.SetEnvironmentVariable("GGS_LICENSE_PATH", tmpFile);
        try
        {
            var svc = new LicenseService();
            var (ok, msg) = await svc.ValidateAndSaveFromTextAsync("{ invalid json }");
            Assert.False(ok);
            Assert.Contains("Invalid", msg, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GGS_LICENSE_PATH", null);
            if (File.Exists(tmpFile)) File.Delete(tmpFile);
        }
    }
}
