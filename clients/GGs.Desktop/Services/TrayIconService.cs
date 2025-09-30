using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;

namespace GGs.Desktop.Services;

public sealed class TrayIconService : IDisposable
{
    private static TrayIconService? _instance;
    public static TrayIconService Instance => _instance ??= new TrayIconService();

    private NotifyIcon? _icon;
    private bool _paused;

    private TrayIconService() { }

    public void Initialize()
    {
        if (_icon != null) return;
        _icon = new NotifyIcon
        {
            Icon = TryLoadCustomIcon() ?? SystemIcons.Application,
            Text = "GGs Desktop",
            Visible = true
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open GGs", null, (_, __) => ShowMainWindow());
        menu.Items.Add("License Diagnostics", null, (_, __) => ShowLicenseDiagnostics());
        menu.Items.Add(_paused ? "Resume Monitoring" : "Pause Monitoring", null, ToggleMonitoring);
        menu.Items.Add("Check for Updates", null, (_, __) => CheckForUpdates());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, __) => QuitApplication());
        _icon.ContextMenuStrip = menu;

        _icon.DoubleClick += (_, __) => ShowMainWindow();
        AppLogger.LogInfo("Tray icon initialized");
    }

    public void UpdateMenu()
    {
        if (_icon?.ContextMenuStrip == null) return;
        foreach (ToolStripItem item in _icon.ContextMenuStrip.Items)
        {
            if (item is ToolStripMenuItem mi && ((mi.Text?.Contains("Pause") == true) || (mi.Text?.Contains("Resume") == true)))
            {
                mi.Text = _paused ? "Resume Monitoring" : "Pause Monitoring";
            }
        }
    }

    public void MinimizeWindowToTray(Window w)
    {
        try
        {
            Initialize();
            w.Hide();
            var icon = _icon;
            if (icon == null)
            {
                AppLogger.LogWarn("Tray icon not initialized; cannot show balloon tip.");
                return;
            }
            icon.BalloonTipTitle = "GGs";
            icon.BalloonTipText = "Still running in the background";
            icon.ShowBalloonTip(2000);
            AppLogger.LogInfo("Window minimized to tray");
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Failed to minimize to tray", ex);
        }
    }

    public void ShowMainWindow()
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            try
            {
                var win = GetMainWindow();
                if (win == null)
                {
                    // Attempt to create a new main window if not existing
                    try { win = new Views.ModernMainWindow(); } catch { }
                }
                if (win != null)
                {
                    win.Show();
                    win.Activate();
                    AppLogger.LogInfo("Main window shown from tray");
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Failed to show main window from tray", ex);
            }
        });
    }

    public void ShowLicenseDiagnostics()
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            try
            {
                var w = new GGs.Desktop.Views.LicenseDiagnosticsWindow();
                w.Show();
                w.Activate();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Failed to open LicenseDiagnosticsWindow", ex);
            }
        });
    }

    private static Window? GetMainWindow()
    {
        var app = System.Windows.Application.Current;
        if (app == null) return null;
        foreach (Window w in app.Windows)
        {
            if (w is Views.ModernMainWindow) return w;
        }
        return null;
    }

    private void ToggleMonitoring(object? sender, EventArgs e)
    {
        _paused = !_paused;
        UpdateMenu();
        AppLogger.LogInfo(_paused ? "Monitoring paused" : "Monitoring resumed");
        // TODO: Integrate with actual monitoring service start/stop when implemented.
    }

    private async void CheckForUpdates()
    {
        try
        {
            AppLogger.LogInfo("User requested update check from tray");
            await UpdateCoordinator.Instance.CheckAndShowAsync(notifyIfUpToDate: true);
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Update check failed", ex);
            try { System.Windows.MessageBox.Show($"Update check failed: {ex.Message}", "GGs", MessageBoxButton.OK, MessageBoxImage.Error); } catch { }
        }
    }

    private void QuitApplication()
    {
        try
        {
            AppLogger.LogInfo("Quit requested from tray");
            Dispose();
            try { GGs.Desktop.App.IsExiting = true; } catch { }
            System.Windows.Application.Current?.Shutdown();
        }
        catch { }
    }

    public void Dispose()
    {
        try
        {
            if (_icon != null)
            {
                _icon.Visible = false;
                _icon.Dispose();
                _icon = null;
            }
        }
        catch { }
    }

    private static Icon? TryLoadCustomIcon()
    {
        try
        {
            var uri = IconService.FindIconPath();
            if (string.IsNullOrWhiteSpace(uri)) return null;
            var path = new Uri(uri).LocalPath;
            if (path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
            {
                return new Icon(path);
            }
        }
        catch { }
        return null;
    }
}
