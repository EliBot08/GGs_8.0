#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using GGs.ErrorLogViewer.Services;
using GGs.ErrorLogViewer.ViewModels;
using GGs.ErrorLogViewer.Views;
using Serilog;

namespace GGs.ErrorLogViewer
{
    public partial class App : Application
    {
        private IHost? _host;
        private IEarlyLoggingService? _earlyLoggingService;
        private string? _commandLineLogDirectory;
        private bool _autoStart = true;
        private static Mutex? _mutex;
        private static bool _mutexCreated = false;

        public IServiceProvider ServiceProvider => _host?.Services ?? throw new InvalidOperationException("Host not initialized");

        protected override void OnStartup(StartupEventArgs e)
        {
            // Single instance check with proper mutex handling
            try
            {
                _mutex = new Mutex(true, "{8F6F0AC4-B9A1-45fd-A8CF-72F04E6BDE8F-v2}", out _mutexCreated);
                if (!_mutexCreated)
                {
                    MessageBox.Show(
                        "GGs ErrorLogViewer is already running.\n\nPlease check your taskbar.",
                        "Already Running",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    Shutdown();
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to create single instance mutex: {ex.Message}\n\nContinuing anyway...",
                    "Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            
            try
            {
                // Parse command line arguments
                ParseCommandLineArguments(e.Args);

                // Configure early logging before anything else
                ConfigureEarlyLogging();

                // Build configuration
                var configuration = BuildConfiguration();

                // Initialize early logging service FIRST
                _earlyLoggingService = new EarlyLoggingService(configuration);
                _earlyLoggingService.StartCapturing();

                // Log application startup
                _earlyLoggingService.LogApplicationEvent("Application", "ErrorLogViewer starting up", new
                {
                    Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
                    CommandLineArgs = e.Args,
                    WorkingDirectory = Environment.CurrentDirectory,
                    ProcessId = Environment.ProcessId,
                    CommandLineLogDirectory = _commandLineLogDirectory,
                    AutoStart = _autoStart
                });

                // Create and configure host
                _host = CreateHost(configuration);

                _host.Start();

                _earlyLoggingService.LogApplicationEvent("Application", "Host started successfully");

                // Show main window
                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                
                // Apply command line settings to EnhancedMainViewModel
                var viewModel = _host.Services.GetRequiredService<EnhancedMainViewModel>();
                if (!string.IsNullOrEmpty(_commandLineLogDirectory))
                {
                    viewModel.SetLogDirectory(_commandLineLogDirectory);
                }
                else
                {
                    // Use default log directory from configuration
                    var defaultLogDir = configuration["ErrorLogViewer:DefaultLogDirectory"];
                    if (!string.IsNullOrEmpty(defaultLogDir))
                    {
                        viewModel.SetLogDirectory(defaultLogDir);
                    }
                }
                
                if (_autoStart)
                {
                    viewModel.AutoStartMonitoring();
                }
                
                // Set the main window as the application's main window
                MainWindow = mainWindow;
                mainWindow.Show();

                _earlyLoggingService.LogApplicationEvent("Application", "Main window displayed");

                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                try
                {
                    _earlyLoggingService?.LogApplicationEvent("Application", "Startup failed", new { Exception = ex.ToString() });
                }
                catch
                {
                    // If early logging fails, write to console
                    Console.WriteLine($"CRITICAL: ErrorLogViewer startup failed: {ex}");
                }
                
                MessageBox.Show($"Failed to start ErrorLogViewer: {ex.Message}\n\nDetails: {ex}", "Startup Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        private void ParseCommandLineArguments(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "--log-dir":
                    case "-d":
                        if (i + 1 < args.Length)
                        {
                            _commandLineLogDirectory = args[++i];
                        }
                        break;
                    case "--no-auto-start":
                    case "-n":
                        _autoStart = false;
                        break;
                    case "--help":
                    case "-h":
                    case "/?":
                        ShowHelp();
                        Shutdown(0);
                        return;
                }
            }
        }

        private void ShowHelp()
        {
            var helpText = @"GGs ErrorLogViewer - Enterprise Log Monitoring Tool

Usage: GGs.ErrorLogViewer.exe [options]

Options:
  --log-dir, -d <directory>    Specify the log directory to monitor
  --no-auto-start, -n          Don't automatically start monitoring on startup
  --help, -h, /?               Show this help message

Examples:
  GGs.ErrorLogViewer.exe --log-dir ""C:\Logs\GGs""
  GGs.ErrorLogViewer.exe -d ""C:\Logs"" --no-auto-start
";
            MessageBox.Show(helpText, "ErrorLogViewer Help", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _earlyLoggingService?.LogApplicationEvent("Application", "Application shutting down", new { ExitCode = e.ApplicationExitCode });

                _host?.StopAsync(TimeSpan.FromSeconds(5)).Wait();
                _host?.Dispose();

                _earlyLoggingService?.LogApplicationEvent("Application", "Host stopped");
                _earlyLoggingService?.StopCapturing();
                _earlyLoggingService?.Dispose();

                Log.CloseAndFlush();
            }
            catch (Exception ex)
            {
                // Log to event log as fallback
                try
                {
                    System.Diagnostics.EventLog.WriteEntry("GGs.ErrorLogViewer", 
                        $"Error during shutdown: {ex}", System.Diagnostics.EventLogEntryType.Error);
                }
                catch
                {
                    // If all else fails, write to console
                    Console.WriteLine($"CRITICAL: Error during shutdown: {ex}");
                }
            }
            finally
            {
                // Release mutex to allow future instances
                try
                {
                    if (_mutexCreated && _mutex != null)
                    {
                        _mutex.ReleaseMutex();
                        _mutex.Dispose();
                        _mutex = null;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to release mutex: {ex.Message}");
                }
                
                base.OnExit(e);
            }
        }

        private static void ConfigureEarlyLogging()
        {
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                "GGs", "Logs");
            
            try
            {
                Directory.CreateDirectory(logDirectory);
            }
            catch (Exception)
            {
                // Fallback to temp directory if LocalApplicationData fails
                logDirectory = Path.Combine(Path.GetTempPath(), "GGs", "Logs");
                Directory.CreateDirectory(logDirectory);
            }

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    Path.Combine(logDirectory, "errorlogviewer-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.Console()
                .CreateLogger();

            Log.Information("Early logging configured for ErrorLogViewer");
        }

        private IConfiguration BuildConfiguration()
        {
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", 
                    optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            // Set default log directory if not specified
            var defaultLogDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                "GGs", "Logs");

            // Add command line overrides
            if (!string.IsNullOrEmpty(_commandLineLogDirectory))
            {
                configBuilder.AddInMemoryCollection(new[]
                {
                    new KeyValuePair<string, string?>("ErrorLogViewer:DefaultLogDirectory", _commandLineLogDirectory)
                });
            }
            else
            {
                configBuilder.AddInMemoryCollection(new[]
                {
                    new KeyValuePair<string, string?>("ErrorLogViewer:DefaultLogDirectory", defaultLogDir)
                });
            }

            if (!_autoStart)
            {
                configBuilder.AddInMemoryCollection(new[]
                {
                    new KeyValuePair<string, string?>("ErrorLogViewer:AutoStartWithGGs", "false")
                });
            }

            return configBuilder.Build();
        }

        private IHost CreateHost(IConfiguration configuration)
        {
            return Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration(builder =>
                {
                    builder.Sources.Clear();
                    builder.AddConfiguration(configuration);
                })
                .ConfigureServices((context, services) =>
                {
                    // Register configuration
                    services.AddSingleton(configuration);

                    // Register logging
                    services.AddLogging(builder =>
                    {
                        builder.ClearProviders();
                        builder.AddSerilog();
                    });

                    // Register early logging service as singleton (already initialized)
                    services.AddSingleton<IEarlyLoggingService>(_earlyLoggingService!);

                    // Register core services
                    services.AddSingleton<ILogMonitoringService, LogMonitoringService>();
                    services.AddSingleton<ILogParsingService, LogParsingService>();
                    services.AddSingleton<IThemeService, ThemeService>();
                    services.AddSingleton<IPerformanceMonitoringService, PerformanceMonitoringService>();
                    services.AddSingleton<IAlertService, AlertService>();
                    services.AddSingleton<IAnalyticsService, AnalyticsService>();
                    
                    // Register enhanced professional services
                    services.AddSingleton<IBookmarkService, BookmarkService>();
                    services.AddSingleton<ISmartAlertService, SmartAlertService>();
                    services.AddSingleton<IAnalyticsEngine, AnalyticsEngine>();
                    services.AddSingleton<ISessionStateService, SessionStateService>();
                    services.AddSingleton<IExportService, ExportService>();
                    services.AddSingleton<IEnhancedExportService, EnhancedExportService>();
                    services.AddSingleton<IExternalLogSourceService, ExternalLogSourceService>();
                    services.AddSingleton<ILogComparisonService, LogComparisonService>();
                    services.AddSingleton<IRetentionPolicyService, RetentionPolicyService>();
                    
                    // Phase 9 - Performance services
                    services.AddSingleton<ILogCachingService, LogCachingService>();
                    services.AddSingleton<IPerformanceAnalyzer, PerformanceAnalyzer>();
                    
                    // Register background services
                    services.AddHostedService<SessionStateService>();
                    services.AddHostedService<RetentionPolicyService>();

                    // Register ViewModels
                    services.AddTransient<MainViewModel>();
                    services.AddTransient<EnhancedMainViewModel>();
                    services.AddTransient<MainWindow>();
                })
                .UseSerilog()
                .Build();
        }
    }
}