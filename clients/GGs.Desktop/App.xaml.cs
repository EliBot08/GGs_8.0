using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using GGs.Desktop.Configuration;
using GGs.Desktop.Services;
using GGs.Desktop.Services.Logging;
using GGs.Desktop.Telemetry;
using GGs.Shared.Licensing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GGs.Desktop;

public partial class App : Application
{
    public static bool IsExiting { get; internal set; }
    public static IServiceProvider? ServiceProvider { get; private set; }

    private static Mutex? _singleInstanceMutex;
    private static bool _mutexCreated;

    private RollingFileLoggerProvider? _fileLoggerProvider;
    private StartupHealthService? _startupHealth;
    private string? _navigationTarget;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _navigationTarget = ParseNavigationTarget(e.Args);

        ConfigureLogging();
        AppLogger.LogInfo("Starting application bootstrap");

        RegisterGlobalExceptionHandlers();
        RegisterSystemEvents();

        if (!EnsureSingleInstance())
        {
            Shutdown();
            return;
        }

        _startupHealth = new StartupHealthService();
        _startupHealth.OnStartupBegin();

        ApplyThemeWithFallback();
        InitializeCrashReporting();

        InitializeMainWindow();
        StartBackgroundInitialization();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        IsExiting = true;

        try
        {
            _startupHealth?.MarkCleanExit();
        }
        catch (Exception ex)
        {
            AppLogger.LogWarn($"Failed to mark clean exit: {ex.Message}");
        }

        UnregisterSystemEvents();
        ReleaseSingleInstanceMutex();

        try
        {
            OpenTelemetryConfig.Shutdown();
        }
        catch (Exception ex)
        {
            AppLogger.LogWarn($"OpenTelemetry shutdown failed: {ex.Message}");
        }

        AppLogger.LogAppClosing();
        _fileLoggerProvider?.Dispose();

        base.OnExit(e);
    }

    private string? ParseNavigationTarget(string[] args)
    {
        foreach (var arg in args)
        {
            try
            {
                if (arg.StartsWith("nav=", StringComparison.OrdinalIgnoreCase))
                {
                    return arg.Split('=', 2)[1];
                }

                if (arg.StartsWith("/nav=", StringComparison.OrdinalIgnoreCase))
                {
                    return arg.Split('=', 2)[1];
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogWarn($"Failed to parse navigation argument '{arg}': {ex.Message}");
            }
        }

        return null;
    }

    private void ConfigureLogging()
    {
        try
        {
            var baseDirOverride = Environment.GetEnvironmentVariable("GGS_DATA_DIR");
            var baseDir = !string.IsNullOrWhiteSpace(baseDirOverride)
                ? baseDirOverride!
                : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            var logDirectory = Path.Combine(baseDir, "GGs", "Logs");
            Directory.CreateDirectory(logDirectory);

            var options = new RollingFileLoggerOptions
            {
                FileName = "desktop.log",
                MaxFileSizeBytes = 10 * 1024 * 1024,
                MaxRetainedFiles = 7,
                MinimumLevel = LogLevel.Information
            };

            _fileLoggerProvider = new RollingFileLoggerProvider(logDirectory, options);

            var services = new ServiceCollection();
            services.AddSingleton(_fileLoggerProvider);
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddProvider(_fileLoggerProvider);
                builder.AddDebug();
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            ServiceProvider = services.BuildServiceProvider();
            var loggerFactory = ServiceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("GGs.Desktop");
            AppLogger.Initialize(logger);
            AppLogger.ConfigureFallbackLogPath(Path.Combine(logDirectory, "desktop-fallback.log"));
        }
        catch (Exception ex)
        {
            AppLogger.Initialize();
            AppLogger.LogWarn($"Structured logging setup failed: {ex.Message}");
        }
    }

    private bool EnsureSingleInstance()
    {
        try
        {
            _singleInstanceMutex = new Mutex(initiallyOwned: true, "GGs.Desktop.Singleton.v5.0", out _mutexCreated);
            if (!_mutexCreated)
            {
                AppLogger.LogWarn("Another instance of GGs Desktop is already running");
                MessageBox.Show(
                    "GGs Desktop is already running. Check the taskbar or system tray to bring it to the foreground.",
                    "GGs Desktop",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return false;
            }

            AppLogger.LogInfo("Single instance mutex acquired successfully");
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.LogWarn($"Failed to acquire single instance mutex: {ex.Message}");
            return true; // Continue so the app is still usable even if mutex creation fails
        }
    }

    private void ReleaseSingleInstanceMutex()
    {
        if (_singleInstanceMutex == null)
        {
            return;
        }

        try
        {
            if (_mutexCreated)
            {
                _singleInstanceMutex.ReleaseMutex();
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogWarn($"Failed to release single instance mutex: {ex.Message}");
        }
        finally
        {
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
        }
    }

    private void ApplyThemeWithFallback()
    {
        try
        {
            var settings = new SettingsManager().Load();
            ThemeManagerService.Instance.ApplyAppearance(settings);
            ThemeManagerService.Instance.ApplyTheme();
            AppLogger.LogInfo("Theme resources applied successfully");
        }
        catch (Exception ex)
        {
            AppLogger.LogWarn($"Failed to apply theme resources: {ex.Message}");
        }
    }

    private void InitializeCrashReporting()
    {
        try
        {
            CrashReportingService.Instance.Initialize();
            CrashReportingService.Instance.AddBreadcrumb("App startup", "lifecycle");
        }
        catch (Exception ex)
        {
            AppLogger.LogWarn($"Crash reporting initialization failed: {ex.Message}");
        }
    }

    private void InitializeMainWindow()
    {
        Dispatcher.Invoke(() =>
        {
            ShutdownMode = ShutdownMode.OnMainWindowClose;

            try
            {
                var window = new Views.ModernMainWindow();
                MainWindow = window;
                window.Loaded += OnMainWindowLoaded;
                window.Show();
                window.Activate();
                window.Focus();
                AppLogger.LogInfo("Main window created and shown");
            }
            catch (Exception ex)
            {
                HandleGlobalException("Main window initialization", ex, notifyUser: true, userMessage: "We could not load the main dashboard UI. Running in limited recovery mode.");
                ShowFallbackShell();
            }
        }, DispatcherPriority.Send);
    }

    private void OnMainWindowLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not Views.ModernMainWindow window)
        {
            return;
        }

        window.Loaded -= OnMainWindowLoaded;

        if (!string.IsNullOrWhiteSpace(_navigationTarget))
        {
            SafeUiAction(() => window.NavigateTo(_navigationTarget!), "navigate to requested tab");
        }
    }

    private void ShowFallbackShell()
    {
        var fallback = new Window
        {
            Title = "GGs Desktop - Recovery Mode",
            Width = 640,
            Height = 400,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = new Grid
            {
                Margin = new Thickness(24),
                Children =
                {
                    new TextBlock
                    {
                        Text = "GGs Desktop is running in recovery mode. Critical UI components failed to load. Review the logs for details.",
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 14,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        TextAlignment = TextAlignment.Center
                    }
                }
            }
        };

        fallback.Show();
        fallback.Activate();
        fallback.Focus();
    }

    private void SafeUiAction(Action action, string context)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            HandleGlobalException(context, ex, notifyUser: true);
        }
    }

    private void StartBackgroundInitialization()
    {
        Task.Run(async () =>
        {
            try
            {
                using var activity = TelemetrySources.Startup.StartActivity("app.startup", ActivityKind.Internal);

                await InitializeTelemetryAsync().ConfigureAwait(false);
                await InitializeDeviceEnrollmentAsync().ConfigureAwait(false);
                await InitializeLicenseAndEntitlementsAsync().ConfigureAwait(false);
                await InitializeSettingsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                HandleGlobalException("Background initialization", ex, notifyUser: true, userMessage: "Background initialization failed. Some features may be limited until the next restart.");
            }
        });
    }

    private Task InitializeTelemetryAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                OpenTelemetryConfig.Initialize();
                AppLogger.LogInfo("OpenTelemetry initialized");
            }
            catch (Exception ex)
            {
                AppLogger.LogWarn($"OpenTelemetry initialization failed: {ex.Message}");
            }
        });
    }

    private async Task InitializeDeviceEnrollmentAsync()
    {
        try
        {
            await DeviceEnrollmentService.EnsureEnrolledAsync().ConfigureAwait(false);
            AppLogger.LogInfo("Device enrollment ensured");
        }
        catch (Exception ex)
        {
            HandleGlobalException("Device enrollment", ex, notifyUser: true, userMessage: "Device enrollment failed. Secure services may be unavailable.");
        }
    }

    private Task InitializeSettingsAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                SettingsService.LaunchMinimized = false;
                AppLogger.LogInfo("LaunchMinimized flag reset to false");
            }
            catch (Exception ex)
            {
                AppLogger.LogWarn($"Failed to update settings: {ex.Message}");
            }
        });
    }

    private Task InitializeLicenseAndEntitlementsAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                var licenseService = new LicenseService();
                var payload = licenseService.CurrentPayload;

                if (payload != null)
                {
                    AppLogger.LogInfo($"License detected for {payload.UserId} at tier {payload.Tier}");
                }
                else
                {
                    AppLogger.LogInfo("No license payload found; defaulting to Basic tier");
                }

                string[] roles = Array.Empty<string>();
                try
                {
                    roles = new GGs.Shared.Api.AuthService(new System.Net.Http.HttpClient()).CurrentRoles;
                }
                catch (Exception ex)
                {
                    AppLogger.LogWarn($"Failed to load current roles: {ex.Message}");
                }

                EntitlementsService.Initialize(payload?.Tier ?? GGs.Shared.Enums.LicenseTier.Basic, roles);
                AppLogger.LogInfo("Entitlements initialized");
            }
            catch (Exception ex)
            {
                AppLogger.LogWarn($"License or entitlements initialization failed: {ex.Message}");
            }
        });
    }

    private void RegisterGlobalExceptionHandlers()
    {
        try
        {
            DispatcherUnhandledException += (_, args) =>
            {
                HandleGlobalException("UI thread", args.Exception, notifyUser: true, userMessage: "Something went wrong, but we recovered. The issue has been logged.");
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                HandleGlobalException("Background thread", args.ExceptionObject as Exception, notifyUser: true, userMessage: "A background operation failed. Features may be partially degraded.");
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                HandleGlobalException("Task scheduler", args.Exception, notifyUser: false);
                args.SetObserved();
            };
        }
        catch (Exception ex)
        {
            AppLogger.LogWarn($"Failed to register global exception handlers: {ex.Message}");
        }
    }

    private void HandleGlobalException(string context, Exception? exception, bool notifyUser, string? userMessage = null)
    {
        var safeContext = string.IsNullOrWhiteSpace(context) ? "Unknown context" : context;
        var safeMessage = exception?.Message ?? "[Unknown error]";

        if (exception != null)
        {
            AppLogger.LogCritical($"Unhandled exception in {safeContext}: {safeMessage}", exception);
        }
        else
        {
            AppLogger.LogCritical($"Unhandled exception in {safeContext}: {safeMessage}", new Exception(safeMessage));
        }

        if (exception is System.Windows.Markup.XamlParseException xamlEx)
        {
            AppLogger.LogCritical($"XAML parse failure details -> Line: {xamlEx.LineNumber}, Position: {xamlEx.LinePosition}, Uri: {xamlEx.BaseUri}", xamlEx);
            foreach (DictionaryEntry entry in xamlEx.Data)
            {
                AppLogger.LogCritical($"XAML data[{entry.Key}] = {entry.Value}", xamlEx.InnerException ?? xamlEx);
            }
        }

        try
        {
            CrashReportingService.Instance.AddBreadcrumb(safeContext, "exception");
            if (exception != null)
            {
                CrashReportingService.Instance.CaptureException(exception, safeContext);
            }
        }
        catch
        {
            // Ignore crash reporting failures
        }

        if (notifyUser)
        {
            var toastMessage = string.IsNullOrWhiteSpace(userMessage) ? $"{safeContext}: {safeMessage}" : userMessage;
            PostToast(NotificationType.Error, toastMessage);
        }
    }

    private void PostToast(NotificationType type, string message)
    {
        void Show()
        {
            var safeMessage = string.IsNullOrWhiteSpace(message) ? "An unexpected error occurred." : message;
            NotificationCenter.Add(type, safeMessage, showToast: true);
        }

        if (Dispatcher.CheckAccess())
        {
            Show();
        }
        else
        {
            Dispatcher.BeginInvoke((Action)Show, DispatcherPriority.Background);
        }
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

    private void UnregisterSystemEvents()
    {
        try
        {
            Microsoft.Win32.SystemEvents.SessionEnding -= OnSessionEnding;
            Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        }
        catch (Exception ex)
        {
            AppLogger.LogWarn($"Unable to unregister system events: {ex.Message}");
        }
    }

    private void OnSessionEnding(object? sender, Microsoft.Win32.SessionEndingEventArgs e)
    {
        AppLogger.LogInfo($"Session ending: {e.Reason}");
    }

    private void OnPowerModeChanged(object? sender, Microsoft.Win32.PowerModeChangedEventArgs e)
    {
        AppLogger.LogInfo($"Power mode changed: {e.Mode}");
    }
}

