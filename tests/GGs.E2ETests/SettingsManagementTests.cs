using System;
using System.IO;
using Xunit;
using GGs.Desktop.Services;
using GGs.Desktop.Configuration;

namespace GGs.E2ETests;

public class SettingsManagementTests
{
    [Fact]
    public void UserSettings_ValidationAndPersistence_RoundTrips()
    {
        var temp = Path.Combine(Path.GetTempPath(), "ggs_settings_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        Environment.SetEnvironmentVariable("GGS_DATA_DIR", temp);
        try
        {
            var mgr = new SettingsManager();
            var s = mgr.Load();
            s.ServerBaseUrl = "https://example.com";
            s.UpdateChannel = "beta";
            s.UpdateBandwidthLimitKBps = 1234;
            s.LaunchMinimized = true;
            s.StartWithWindows = false;
            s.CrashReportingEnabled = true;
            mgr.Save(s);

            var s2 = mgr.Load();
            Assert.Equal("https://example.com", s2.ServerBaseUrl);
            Assert.Equal("beta", s2.UpdateChannel);
            Assert.Equal(1234, s2.UpdateBandwidthLimitKBps);
            Assert.True(s2.LaunchMinimized);
            Assert.False(s2.StartWithWindows);
            Assert.True(s2.CrashReportingEnabled);

            // Export/Import
            var exportPath = Path.Combine(temp, "export.json");
            var (ok, err) = mgr.TryExport(exportPath);
            Assert.True(ok, err);
            var json = File.ReadAllText(exportPath);
            var imported = UserSettings.FromJson(json);
            Assert.Equal("https://example.com", imported.ServerBaseUrl);

            // Import should overwrite
            imported.ServerBaseUrl = "https://imported.test";
            File.WriteAllText(exportPath, UserSettings.ToJson(imported));
            var (ok2, err2) = mgr.TryImport(exportPath);
            Assert.True(ok2, err2);
            Assert.Equal("https://imported.test", mgr.Load().ServerBaseUrl);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GGS_DATA_DIR", null);
            try { Directory.Delete(temp, true); } catch { }
        }
    }

    [Fact]
    public void Secrets_Are_Stored_Protected()
    {
        var temp = Path.Combine(Path.GetTempPath(), "ggs_settings_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        Environment.SetEnvironmentVariable("GGS_DATA_DIR", temp);
        try
        {
            var mgr = new SettingsManager();
            mgr.SetCloudProfilesApiToken("super_secret_value");
            var rawSecrets = Directory.GetFiles(temp, "settings.secrets.bin", SearchOption.AllDirectories);
            Assert.NotEmpty(rawSecrets);
            // Not asserting content as it's DPAPI-protected
            Assert.Equal("super_secret_value", mgr.GetCloudProfilesApiToken());
        }
        finally
        {
            Environment.SetEnvironmentVariable("GGS_DATA_DIR", null);
            try { Directory.Delete(temp, true); } catch { }
        }
    }
}

