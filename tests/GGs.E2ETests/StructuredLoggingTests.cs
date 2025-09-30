using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using GGs.Desktop.Services;
using Xunit;

namespace GGs.E2ETests;

public class StructuredLoggingTests
{
    [Fact]
    public void AppLogger_Should_Write_JSON_And_Rotate_With_Compression()
    {
        // Arrange
        var temp = Path.Combine(Path.GetTempPath(), "ggs_struct_logs_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);

        // Configure logging
        Environment.SetEnvironmentVariable("GGS_LOG_DIR", temp);
        Environment.SetEnvironmentVariable("GGS_LOG_JSON_ENABLED", "true");
        Environment.SetEnvironmentVariable("GGS_LOG_TXT_ENABLED", "true");
        Environment.SetEnvironmentVariable("GGS_LOG_ROLL_DAILY", "false");
        Environment.SetEnvironmentVariable("GGS_LOG_MAX_FILES", "3");
        Environment.SetEnvironmentVariable("GGS_LOG_COMPRESS_OLD", "true");
        Environment.SetEnvironmentVariable("GGS_LOG_MAX_MB", "1"); // 1 MB

        try
        {
            // Fresh init
            AppLogger.ResetForTesting(temp);
            AppLogger.Initialize();

            // 2 MB message to quickly exceed 1 MB
            var big = new string('A', 2 * 1024 * 1024);

            // First write grows file above threshold
            AppLogger.LogInfo("first small");
            AppLogger.LogInfo(big);
            // Second write should trigger rotation
            AppLogger.LogInfo("after big causes rotate");

            // Assert JSON file exists
            var jsonPath = Path.Combine(temp, "desktop.jsonl");
            Assert.True(File.Exists(jsonPath));

            // Assert at least one compressed archive exists (either jsonl or log)
            var gzArchives = Directory.EnumerateFiles(temp, "*.gz").ToList();
            Assert.True(gzArchives.Count >= 1);

            // Validate at least one JSON line is valid
            var firstJsonLine = File.ReadLines(jsonPath).FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
            Assert.False(string.IsNullOrWhiteSpace(firstJsonLine));
            using var doc = JsonDocument.Parse(firstJsonLine!);
            var root = doc.RootElement;
            Assert.True(root.TryGetProperty("Level", out _) || root.TryGetProperty("level", out _));
            Assert.True(root.TryGetProperty("Message", out _) || root.TryGetProperty("message", out _));
            Assert.True(root.TryGetProperty("Timestamp", out _) || root.TryGetProperty("timestamp", out _));

            // Validate we can read a gz archive (best-effort)
            var anyGz = gzArchives.First();
            using var fs = new FileStream(anyGz, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var gz = new GZipStream(fs, CompressionMode.Decompress);
            using var sr = new StreamReader(gz);
            var firstArchivedLine = sr.ReadLine();
            Assert.False(string.IsNullOrWhiteSpace(firstArchivedLine));
        }
        finally
        {
            // Cleanup env and temp dir
            Environment.SetEnvironmentVariable("GGS_LOG_DIR", null);
            Environment.SetEnvironmentVariable("GGS_LOG_JSON_ENABLED", null);
            Environment.SetEnvironmentVariable("GGS_LOG_TXT_ENABLED", null);
            Environment.SetEnvironmentVariable("GGS_LOG_ROLL_DAILY", null);
            Environment.SetEnvironmentVariable("GGS_LOG_MAX_FILES", null);
            Environment.SetEnvironmentVariable("GGS_LOG_COMPRESS_OLD", null);
            Environment.SetEnvironmentVariable("GGS_LOG_MAX_MB", null);
            try { Directory.Delete(temp, true); } catch { }
        }
    }
}

