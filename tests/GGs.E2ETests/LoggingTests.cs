using System;
using System.IO;
using System.Threading.Tasks;
using GGs.Desktop.Services;
using Xunit;

namespace GGs.E2ETests;

public class LoggingTests
{
    [Fact]
    public void AppLogger_ShouldWrite_To_Custom_Log_Dir()
    {
        // Arrange
        var temp = Path.Combine(Path.GetTempPath(), "ggs_test_logs_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        Environment.SetEnvironmentVariable("GGS_LOG_DIR", temp);

        try
        {
            // Act (force logger to use our temp dir regardless of previous tests)
            AppLogger.ResetForTesting(temp);
            AppLogger.Initialize();
            AppLogger.LogInfo("Test info log entry");
            AppLogger.LogWarn("Test warning entry");
            AppLogger.LogAppClosing();

            // Assert
            var logPath = Path.Combine(temp, "desktop.log");
            Assert.True(File.Exists(logPath));
            var content = File.ReadAllText(logPath);
            Assert.Contains("Test info log entry", content);
            Assert.Contains("Test warning entry", content);
            Assert.Contains("STOP", content);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GGS_LOG_DIR", null);
            try { Directory.Delete(temp, true); } catch { }
        }
    }
}

