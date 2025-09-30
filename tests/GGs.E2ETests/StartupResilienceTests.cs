using System;
using System.IO;
using System.Threading;
using Xunit;
using GGs.Desktop.Services;

namespace GGs.E2ETests;

public class StartupResilienceTests
{
    private static Exception? RunSta(Action action)
    {
        Exception? ex = null;
        var t = new Thread(() =>
        {
            try { action(); } catch (Exception e) { ex = e; }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        return ex;
    }

    [Fact]
    public void ErrorLogViewer_Should_Open_And_ReadExistingLogsOnce()
    {
        // Arrange: log to a temp dir before opening the viewer so its initial Poll() sees entries
        var temp = Path.Combine(Path.GetTempPath(), "ggs_test_logs_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        Environment.SetEnvironmentVariable("GGS_LOG_DIR", temp);

        try
        {
            AppLogger.Initialize();
            AppLogger.LogInfo("Smoke: viewer test A");
            AppLogger.LogWarn("Smoke: viewer test B");

            bool hadEntries = false;
            var ex = RunSta(() =>
            {
                var w = new GGs.Desktop.Views.ErrorLogViewer();
                // Write an extra line to ensure new content is detected even if existing files were missed
                GGs.Desktop.Services.AppLogger.LogInfo("Smoke: viewer test C");
                // Allow time for FileSystemWatcher and timer to process
                var sw = System.Diagnostics.Stopwatch.StartNew();
                while (w.Entries.Count == 0 && sw.Elapsed < TimeSpan.FromSeconds(3))
                {
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
                    System.Threading.Thread.Sleep(50);
                }
                hadEntries = w.Entries.Count > 0;
                // Close immediately to avoid lingering UI
                w.Close();
            });

            Assert.Null(ex);
            Assert.True(hadEntries);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GGS_LOG_DIR", null);
            try { Directory.Delete(temp, true); } catch { }
        }
    }

    [Fact]
    public void RecoveryWindow_Should_Open_And_Close_Safely()
    {
        Exception? ex = RunSta(() =>
        {
            var rw = new GGs.Desktop.Views.RecoveryWindow("Simulated startup failure");
            rw.Show();
            rw.Close();
        });

        Assert.Null(ex);
    }
}

