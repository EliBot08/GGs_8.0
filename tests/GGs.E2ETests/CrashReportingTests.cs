using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using GGs.Desktop.Services;
using Xunit;

namespace GGs.E2ETests;

public class CrashReportingTests
{
    [Fact]
    public void CrashReporting_Writes_Scrubbed_Report_With_Attachments()
    {
        // Arrange: redirect logs & reports to a temp dir
        var temp = Path.Combine(Path.GetTempPath(), "ggs_test_crash_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        Environment.SetEnvironmentVariable("GGS_LOG_DIR", temp);

        try
        {
            AppLogger.ResetForTesting(temp);
            AppLogger.Initialize();
            SettingsService.CrashReportingEnabled = true;

            AppLogger.LogInfo("User email is test.user@example.com and token Bearer abc.def.ghi");

            CrashReportingService.Instance.Initialize();
            CrashReportingService.Instance.AddBreadcrumb("About to throw", "test");

            var ex = new InvalidOperationException("Boom from test.user@example.com with key 1234567890ABCDEF");
            CrashReportingService.Instance.CaptureException(ex, "Unit test exception");

            // Assert: report exists and is scrubbed
            var reportsRoot = Path.Combine(temp, "crash-reports");
            var today = DateTime.UtcNow.ToString("yyyyMMdd");
            var dayDir = Path.Combine(reportsRoot, today);
            Assert.True(Directory.Exists(dayDir));
            var file = Directory.GetFiles(dayDir, "crash_*.json").OrderByDescending(f => f).FirstOrDefault();
            Assert.False(string.IsNullOrWhiteSpace(file));
            var json = File.ReadAllText(file!);
            Assert.Contains("[REDACTED_EMAIL]", json);
            Assert.DoesNotContain("test.user@example.com", json);
            Assert.Contains("[REDACTED_KEY]", json);

            // Log tail present
            Assert.Contains("START", json); // from AppLogger
        }
        finally
        {
            Environment.SetEnvironmentVariable("GGS_LOG_DIR", null);
            try { Directory.Delete(temp, true); } catch { }
        }
    }
}

