using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using GGs.Desktop.Services;
using GGs.Shared.Licensing;
using GGs.Desktop.Telemetry;
using System.Diagnostics;
using GGs.Desktop.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Windows.Controls;

namespace GGs.Desktop;

public partial class App : System.Windows.Application
{
    public static bool IsExiting { get; set; }
    private static System.Threading.Mutex? _singleInstanceMutex;
    public static IServiceProvider? ServiceProvider { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Deep-link navigation: parse args like /nav=Network or nav=Network
        string? navArg = null;
        try
        {
            foreach (var arg in e.Args)
            {
                if (arg.StartsWith("nav=", StringComparison.OrdinalIgnoreCase) || arg.StartsWith("/nav=", StringComparison.OrdinalIgnoreCase))
                {
                    navArg = arg.Split('=')[1];
                    break;
                }
            }
        }
        catch { }

        // Initialize OpenTelemetry early for startup tracing/metrics/logs
        try { OpenTelemetryConfig.Initialize(); } catch { }
        using var startupActivity = TelemetrySources.Startup.StartActivity("app.startup", ActivityKind.Internal);

        // Initialize service provider
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().AddDebug());
        ServiceProvider = services.BuildServiceProvider();

        // Initialize AppLogger with a generic logger
        var loggerFactory = ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("GGs.Desktop");
        AppLogger.Initialize(logger);

        // Single-instance guard
        bool createdNew = false;
        _singleInstanceMutex = new System.Threading.Mutex(true, "GGs.Desktop.Singleton", out createdNew);
        if (!createdNew)
        {
try { System.Windows.MessageBox.Show("GGs is already running in the background.", "GGs", MessageBoxButton.OK, MessageBoxImage.Information); } catch { }
System.Windows.Application.Current?.Shutdown();
            return;
        }

        // Initialize friendly file logger and global exception handlers
        AppLogger.Initialize();
        AppLogger.LogInfo("Starting application bootstrap 🧩");
        RegisterGlobalExceptionHandlers();
        RegisterSystemEvents();

        // Apply persisted appearance (theme, accents, font scale) early
        try { 
            var us = new SettingsManager().Load(); 
            ThemeManagerService.Instance.ApplyAppearance(us); 
            AppLogger.LogInfo("Theme resources applied successfully");
        } catch (Exception ex) {
            AppLogger.LogWarn($"Could not apply theme resources: {ex.Message}");
        }

        // Ensure theme resources are loaded before creating windows
        try
        {
            // Force theme resource loading
            var themeService = ThemeManagerService.Instance;
            themeService.ApplyTheme();
            AppLogger.LogInfo("Theme resources initialized");
        }
        catch (Exception ex)
        {
            AppLogger.LogWarn($"Could not initialize theme resources: {ex.Message}");
        }

        // Initialize crash reporting (opt-in)
        try { CrashReportingService.Instance.Initialize(); CrashReportingService.Instance.AddBreadcrumb("App startup", "lifecycle"); } catch { }

        // Startup health tracking
        var health = new StartupHealthService();
        health.OnStartupBegin();
        // Temporarily disable crash-loop detection to fix startup issues
        // var crashLoop = health.IsCrashLoop(thresholdCount: 3, windowSeconds: 60);
        // if (crashLoop)
        // {
        //     AppLogger.LogWarn("Crash-loop detected (>=3 unclean startups in 60s). Entering minimal mode with RecoveryWindow only.");
        //     try { Views.RecoveryWindow rw = new("Crash-loop detected. Running in minimal mode."); rw.Show(); } catch { }
        //     return;
        // }

        // Device enrollment (client certificate + server registration)
        try { DeviceEnrollmentService.EnsureEnrolledAsync().GetAwaiter().GetResult(); AppLogger.LogInfo("Device enrollment ensured."); } catch { }

        // Initialize licensing and entitlements
        var licenseSvc = new Services.LicenseService();
        var licensed = licenseSvc.CurrentPayload != null;
        try { EntitlementsService.Initialize(licenseSvc.CurrentPayload?.Tier ?? GGs.Shared.Enums.LicenseTier.Basic, new GGs.Shared.Api.AuthService(new System.Net.Http.HttpClient()).CurrentRoles); } catch { }

        // Force LaunchMinimized to false to ensure window is always visible
        try
        {
            SettingsService.LaunchMinimized = false;
            AppLogger.LogInfo("Forced LaunchMinimized to false");
        }
        catch (Exception ex)
        {
            AppLogger.LogWarn($"Could not force LaunchMinimized setting: {ex.Message}");
        }

        // Always show the main window - never start minimized to tray
        // Open the ErrorLogViewer early so it captures everything live (singleton)
        try
        {
            // Check if ErrorLogViewer is already running to prevent duplication
            var existingProcesses = System.Diagnostics.Process.GetProcessesByName("GGs.ErrorLogViewer");
            if (existingProcesses.Length == 0)
            {
                var logViewer = new Views.ErrorLogViewer();
                logViewer.Show();
                AppLogger.LogInfo("ErrorLogViewer opened successfully");
            }
            else
            {
                AppLogger.LogInfo($"ErrorLogViewer already running ({existingProcesses.Length} instances), skipping launch");
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogWarn($"Could not open ErrorLogViewer window automatically: {ex.Message}");
        }

        // Try to create a simple test window first
        System.Windows.Window? testWindow = null;
        try
        {
            AppLogger.LogInfo("Creating simple test window...");
            testWindow = new System.Windows.Window
            {
                Title = "GGs Test Window",
                Width = 400,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = System.Windows.Media.Brushes.DarkBlue
            };
            
            var textBlock = new System.Windows.Controls.TextBlock
            {
                Text = "GGs Desktop Application\n\nIf you can see this window, the basic WPF functionality is working.\n\nThis is a test window to verify display capabilities.",
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 16,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            
            testWindow.Content = textBlock;
            testWindow.Show();
            testWindow.Activate();
            testWindow.Focus();
            AppLogger.LogSuccess("Test window created and shown successfully");
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Failed to create test window", ex);
        }

        // Always show the main window - simplified approach
        Views.ModernMainWindow? created = null;
        try
        {
            AppLogger.LogInfo("Creating ModernMainWindow...");
            created = new Views.ModernMainWindow();
            AppLogger.LogInfo("ModernMainWindow created successfully");
            
            // Simple, direct window showing
            created.Show();
            created.WindowState = WindowState.Normal;
            created.Visibility = Visibility.Visible;
            created.ShowInTaskbar = true;
            created.Activate();
            created.Focus();
            
            if (!string.IsNullOrWhiteSpace(navArg)) created.NavigateTo(navArg);
            AppLogger.LogSuccess("Main window shown and activated 🎉");
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Failed to create or show ModernMainWindow", ex);
            
            // Try to create a simple fallback window
            try
            {
                AppLogger.LogInfo("Attempting to create fallback window...");
                var fallbackWindow = new System.Windows.Window
                {
                    Title = "GGs - Gaming Optimization Suite",
                    Width = 800,
                    Height = 600,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Background = System.Windows.Media.Brushes.DarkBlue
                };
                
                var textBlock = new System.Windows.Controls.TextBlock
                {
                    Text = "GGs Desktop Application\n\nIf you can see this window, the main application is working but there was an issue with the modern UI.\n\nPlease check the logs for more details.",
                    Foreground = System.Windows.Media.Brushes.White,
                    FontSize = 16,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                
                fallbackWindow.Content = textBlock;
                fallbackWindow.Show();
                fallbackWindow.Activate();
                fallbackWindow.Focus();
                AppLogger.LogSuccess("Fallback window created and shown");
            }
            catch (Exception fallbackEx)
            {
                AppLogger.LogError("Failed to create fallback window", fallbackEx);
                SafeShowRecoveryWindow($"Both main and fallback windows failed: {ex.Message}");
            }
        }
        
        // Initialize tray for background behavior
        TrayIconService.Instance.Initialize();
        // Start background license revalidation when licensed
        try { Services.LicenseRevalidationService.Instance.Start(); } catch { }
        // Start game detection/optimization service
        try { new Services.GameDetectionService().Start(); } catch { }
        // Mark readiness once tray and main are up
        try { new StartupHealthService().MarkReady(); } catch { }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { new StartupHealthService().MarkCleanExit(); } catch { }
        try
        {
            Microsoft.Win32.SystemEvents.SessionEnding -= OnSessionEnding;
            Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            AppLogger.LogInfo("Application exiting... 👋");
        }
        finally { AppLogger.LogAppClosing(); }
        try { OpenTelemetryConfig.Shutdown(); } catch { }
        base.OnExit(e);
    }

    private void RegisterGlobalExceptionHandlers()
    {
        // UI thread exceptions
        this.DispatcherUnhandledException += (sender, args) =>
        {
            try
            {
                AppLogger.LogError("CRITICAL ERROR: Dispatcher Unhandled Exception", args.Exception);
                try { CrashReportingService.Instance.AddBreadcrumb("UI exception", "exception", "error"); CrashReportingService.Instance.CaptureException(args.Exception, "DispatcherUnhandledException"); } catch { }
                
                // Use the improved SafeShowRecoveryWindow method
                SafeShowRecoveryWindow($"Dispatcher Unhandled Exception: {args.Exception?.Message}");
                
                args.Handled = true; // Keep app running
            }
            catch (Exception ex)
            {
                // If even the error handling fails, try to log to file directly
                try
                {
                    var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GGs", "Logs", "critical_error.log");
                    Directory.CreateDirectory(Path.GetDirectoryName(logPath));
                    File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - CRITICAL: Dispatcher exception handling failed: {ex.Message}\n");
                }
                catch
                {
                    // If even file logging fails, we can't do anything more
                }
            }
        };

        // Non-UI thread exceptions
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            try
            {
                var ex = args.ExceptionObject as Exception;
                AppLogger.LogError("CRITICAL ERROR: Application startup failed", ex);
                try { CrashReportingService.Instance.AddBreadcrumb("Background exception", "exception", "error"); CrashReportingService.Instance.CaptureException(ex, "UnhandledException"); } catch { }
            }
            catch
            {
                // If logging fails, we can't do much more
            }
        };

        // Task exceptions
        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            try
            {
                AppLogger.LogError("A task failed in the background. We noted it and carry on.", args.Exception);
                try { CrashReportingService.Instance.AddBreadcrumb("Task exception", "exception", "warning"); CrashReportingService.Instance.CaptureException(args.Exception, "UnobservedTaskException"); } catch { }
                args.SetObserved();
            }
            catch
            {
                // If logging fails, just observe the exception
                args.SetObserved();
            }
        };
    }

    private void RegisterSystemEvents()
    {
        try
        {
            Microsoft.Win32.SystemEvents.SessionEnding += OnSessionEnding;
            Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;
        }
        catch (Exception ex)
        {
            AppLogger.LogWarn($"Unable to register system events: {ex.Message}");
        }
    }

    private void OnSessionEnding(object? sender, Microsoft.Win32.SessionEndingEventArgs e)
    {
        try { AppLogger.LogInfo($"Session ending: Reason={e.Reason}"); } catch { }
    }

    private void OnPowerModeChanged(object? sender, Microsoft.Win32.PowerModeChangedEventArgs e)
    {
        try { AppLogger.LogInfo($"Power mode changed: {e.Mode}"); } catch { }
    }

    private bool TryShowWithRetry(Func<Window> factory, string windowName, int retries = 3)
    {
        for (int attempt = 1; attempt <= retries; attempt++)
        {
            try
            {
                AppLogger.LogInfo($"Opening {windowName} (attempt {attempt}/{retries})...");
                var win = factory();
                win.Show();
                AppLogger.LogSuccess($"{windowName} opened ✅");
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"Failed to open {windowName} on attempt {attempt}", ex);
                // Short backoff, continue trying
                System.Threading.Thread.Sleep(300);
            }
        }
        return false;
    }

    private void SafeShowRecoveryWindow(string message)
    {
        try
        {
            // Ensure we're on the UI thread
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(() => SafeShowRecoveryWindow(message));
                return;
            }

            // Create a simple error window without complex dependencies
            var errorWindow = new System.Windows.Window
            {
                Title = "GGs - Error Recovery",
                Width = 500,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = System.Windows.Media.Brushes.DarkRed,
                ResizeMode = ResizeMode.NoResize
            };

            var stackPanel = new System.Windows.Controls.StackPanel
            {
                Margin = new Thickness(20, 20, 20, 20)
            };

            var titleText = new System.Windows.Controls.TextBlock
            {
                Text = "⚠️ Critical Error Occurred",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.White,
                Margin = new Thickness(0, 0, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var messageText = new System.Windows.Controls.TextBlock
            {
                Text = message ?? "An unknown error occurred",
                FontSize = 14,
                Foreground = System.Windows.Media.Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 20)
            };

            var buttonPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var retryButton = new System.Windows.Controls.Button
            {
                Content = "Retry Application",
                Padding = new Thickness(15, 8, 15, 8),
                Margin = new Thickness(5, 5, 5, 5),
                Background = System.Windows.Media.Brushes.DarkBlue,
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(1, 1, 1, 1),
                BorderBrush = System.Windows.Media.Brushes.White
            };

            var closeButton = new System.Windows.Controls.Button
            {
                Content = "Close",
                Padding = new Thickness(15, 8, 15, 8),
                Margin = new Thickness(5, 5, 5, 5),
                Background = System.Windows.Media.Brushes.DarkGray,
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(1, 1, 1, 1),
                BorderBrush = System.Windows.Media.Brushes.White
            };

            retryButton.Click += (s, e) =>
            {
                try
                {
                    errorWindow.Close();
                    // Try to restart the main window
                    var mainWindow = new Views.ModernMainWindow();
                    mainWindow.Show();
                }
                catch (Exception restartEx)
                {
                    AppLogger.LogError("Failed to restart main window", restartEx);
                }
            };

            closeButton.Click += (s, e) => errorWindow.Close();

            buttonPanel.Children.Add(retryButton);
            buttonPanel.Children.Add(closeButton);

            stackPanel.Children.Add(titleText);
            stackPanel.Children.Add(messageText);
            stackPanel.Children.Add(buttonPanel);

            errorWindow.Content = stackPanel;
            errorWindow.Show();

            AppLogger.LogWarn("Recovery window opened to keep the app running and logs flowing.");
        }
        catch (Exception ex)
        {
            // As an absolute last resort: try to log to file directly
            try
            {
                var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GGs", "Logs", "critical_error.log");
                Directory.CreateDirectory(Path.GetDirectoryName(logPath));
                File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - CRITICAL: RecoveryWindow failed: {ex.Message}\n");
            }
            catch
            {
                // If even file logging fails, we can't do anything more
            }
        }
    }
}

