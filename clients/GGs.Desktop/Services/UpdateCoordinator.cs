using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace GGs.Desktop.Services;

public sealed class UpdateCoordinator
{
    private static UpdateCoordinator? _instance;
    public static UpdateCoordinator Instance => _instance ??= new UpdateCoordinator();

    private bool _checking;
    private GGs.Desktop.Views.UpdateWindow? _window;

    private UpdateCoordinator() { }

    public async Task CheckAndShowAsync(bool notifyIfUpToDate = false, CancellationToken ct = default)
    {
        if (_checking) return;
        _checking = true;
        try
        {
            var svc = new AutoUpdateService();
            var info = await svc.CheckForUpdatesAsync(ct);
            if (info == null)
            {
                if (notifyIfUpToDate)
                {
                    try { MessageBox.Show("You are up to date.", "GGs", MessageBoxButton.OK, MessageBoxImage.Information); } catch { }
                }
                return;
            }

            Application.Current?.Dispatcher.Invoke(() =>
            {
                try
                {
                    if (_window == null || !_window.IsVisible)
                    {
                        _window = new GGs.Desktop.Views.UpdateWindow(info);
                        _window.Closed += (_, __) => { try { _window = null; } catch { } };
                        _window.Show();
                        _window.Activate();
                    }
                    else
                    {
                        _window.Activate();
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("Failed to show UpdateWindow", ex);
                    try { MessageBox.Show($"Failed to open update window: {ex.Message}", "GGs", MessageBoxButton.OK, MessageBoxImage.Error); } catch { }
                }
            });
        }
        catch (Exception ex)
        {
            AppLogger.LogError("UpdateCoordinator check failed", ex);
            if (notifyIfUpToDate)
            {
                try { MessageBox.Show($"Update check failed: {ex.Message}", "GGs", MessageBoxButton.OK, MessageBoxImage.Error); } catch { }
            }
        }
        finally
        {
            _checking = false;
        }
    }
}

