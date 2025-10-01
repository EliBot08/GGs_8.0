#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace GGs.ErrorLogViewer.Services
{
    public interface IEarlyLoggingService : IDisposable
    {
        void Initialize();
        void StartCapturing();
        void StopCapturing();
        void LogApplicationEvent(string eventType, string message, object? data = null);
        void LogProcessEvent(string processName, string eventType, string message);
        void LogFileSystemEvent(string path, string eventType, string details);
        void LogNetworkEvent(string endpoint, string eventType, string details);
        void LogPerformanceMetric(string metric, double value, string unit);
    }

    public class EarlyLoggingService : IEarlyLoggingService, IDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly string _logDirectory;
        private readonly string _earlyLogFile;
        private readonly string _applicationLogFile;
        private readonly string _performanceLogFile;
        private readonly ConcurrentQueue<LogEvent> _earlyLogQueue;
        private readonly Timer _flushTimer;
        private readonly object _lockObject = new();
        private bool _isCapturing;
        private bool _disposed;

        // Serilog loggers for different purposes
        private Serilog.ILogger? _earlyLogger;
        private Serilog.ILogger? _applicationLogger;
        private Serilog.ILogger? _performanceLogger;

        // Process monitoring
        private readonly ConcurrentDictionary<int, ProcessInfo> _monitoredProcesses;

        public EarlyLoggingService(IConfiguration configuration)
        {
            _configuration = configuration;
            _earlyLogQueue = new ConcurrentQueue<LogEvent>();
            _monitoredProcesses = new ConcurrentDictionary<int, ProcessInfo>();

            // Set up log directory
            _logDirectory = _configuration["Logging:DefaultDirectory"] ?? 
                           Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GGs", "Logs");
            
            Directory.CreateDirectory(_logDirectory);

            // Set up log files with timestamps
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _earlyLogFile = Path.Combine(_logDirectory, $"early_capture_{timestamp}.log");
            _applicationLogFile = Path.Combine(_logDirectory, $"application_{timestamp}.log");
            _performanceLogFile = Path.Combine(_logDirectory, $"performance_{timestamp}.log");

            // Create flush timer (flush every 5 seconds)
            _flushTimer = new Timer(FlushLogs, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

            Initialize();
        }

        public void Initialize()
        {
            try
            {
                // Configure early capture logger (captures everything before main app starts)
                _earlyLogger = new LoggerConfiguration()
                    .MinimumLevel.Verbose()
                    .WriteTo.File(
                        _earlyLogFile,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}",
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 30,
                        buffered: false, // Immediate write for early capture
                        shared: true)
                    .WriteTo.File(
                        new JsonFormatter(),
                        Path.ChangeExtension(_earlyLogFile, ".json"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 30,
                        buffered: false,
                        shared: true)
                    .CreateLogger();

                // Configure application logger (for main application events)
                _applicationLogger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.File(
                        _applicationLogFile,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}",
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 30,
                        shared: true)
                    .WriteTo.File(
                        new JsonFormatter(),
                        Path.ChangeExtension(_applicationLogFile, ".json"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 30,
                        shared: true)
                    .CreateLogger();

                // Configure performance logger (for metrics and performance data)
                _performanceLogger = new LoggerConfiguration()
                    .MinimumLevel.Information()
                    .WriteTo.File(
                        new JsonFormatter(),
                        _performanceLogFile,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 30,
                        shared: true)
                    .CreateLogger();

                LogApplicationEvent("EarlyLogging", "Early logging service initialized", new
                {
                    LogDirectory = _logDirectory,
                    EarlyLogFile = _earlyLogFile,
                    ApplicationLogFile = _applicationLogFile,
                    PerformanceLogFile = _performanceLogFile,
                    ProcessId = Environment.ProcessId,
                    MachineName = Environment.MachineName,
                    UserName = Environment.UserName,
                    OSVersion = Environment.OSVersion.ToString(),
                    CLRVersion = Environment.Version.ToString(),
                    WorkingDirectory = Environment.CurrentDirectory
                });

                // Start monitoring system events
                StartSystemMonitoring();
            }
            catch (Exception ex)
            {
                // Fallback logging to Windows Event Log if file logging fails
                try
                {
                    using var eventLog = new EventLog("Application");
                    eventLog.Source = "GGs.ErrorLogViewer";
                    eventLog.WriteEntry($"Failed to initialize early logging: {ex}", EventLogEntryType.Error);
                }
                catch
                {
                    // If all else fails, write to console
                    Console.WriteLine($"CRITICAL: Failed to initialize early logging: {ex}");
                }
            }
        }

        public void StartCapturing()
        {
            lock (_lockObject)
            {
                if (_isCapturing) return;

                _isCapturing = true;
                LogApplicationEvent("EarlyLogging", "Started comprehensive log capturing", new
                {
                    Timestamp = DateTime.UtcNow,
                    CaptureLevel = "Verbose",
                    Features = new[] { "ProcessMonitoring", "FileSystemEvents", "NetworkEvents", "PerformanceMetrics" }
                });

                // Hook into global exception handlers
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
                TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

                // Start monitoring GGs processes
                StartGGsProcessMonitoring();
            }
        }

        public void StopCapturing()
        {
            lock (_lockObject)
            {
                if (!_isCapturing) return;

                _isCapturing = false;
                LogApplicationEvent("EarlyLogging", "Stopped log capturing");

                // Unhook exception handlers
                AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
                TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;

                // Final flush
                FlushLogs(null);
            }
        }

        public void LogApplicationEvent(string eventType, string message, object? data = null)
        {
            try
            {
                var logEvent = new LogEvent
                {
                    Timestamp = DateTime.UtcNow,
                    Level = LogEventLevel.Information,
                    MessageTemplate = "{EventType}: {Message}",
                    Properties = new Dictionary<string, object?>
                    {
                        ["EventType"] = eventType,
                        ["Message"] = message,
                        ["ProcessId"] = Environment.ProcessId,
                        ["ThreadId"] = Thread.CurrentThread.ManagedThreadId,
                        ["Data"] = data
                    }
                };

                _earlyLogQueue.Enqueue(logEvent);
                _applicationLogger?.Information("{EventType}: {Message} {@Data}", eventType, message, data);
            }
            catch (Exception ex)
            {
                // Fallback logging
                Console.WriteLine($"Failed to log application event: {ex}");
            }
        }

        public void LogProcessEvent(string processName, string eventType, string message)
        {
            try
            {
                _applicationLogger?.Information("Process {ProcessName} {EventType}: {Message}", 
                    processName, eventType, message);

                LogPerformanceMetric($"Process.{processName}.{eventType}", 1, "count");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to log process event: {ex}");
            }
        }

        public void LogFileSystemEvent(string path, string eventType, string details)
        {
            try
            {
                _applicationLogger?.Debug("FileSystem {EventType} at {Path}: {Details}", 
                    eventType, path, details);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to log file system event: {ex}");
            }
        }

        public void LogNetworkEvent(string endpoint, string eventType, string details)
        {
            try
            {
                _applicationLogger?.Information("Network {EventType} to {Endpoint}: {Details}", 
                    eventType, endpoint, details);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to log network event: {ex}");
            }
        }

        public void LogPerformanceMetric(string metric, double value, string unit)
        {
            try
            {
                var performanceData = new
                {
                    Timestamp = DateTime.UtcNow,
                    Metric = metric,
                    Value = value,
                    Unit = unit,
                    ProcessId = Environment.ProcessId,
                    WorkingSet = Environment.WorkingSet,
                    GCMemory = GC.GetTotalMemory(false)
                };

                _performanceLogger?.Information("Performance metric: {Metric} = {Value} {Unit} {@Data}", 
                    metric, value, unit, performanceData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to log performance metric: {ex}");
            }
        }

        private void StartSystemMonitoring()
        {
            Task.Run(async () =>
            {
                while (!_disposed)
                {
                    try
                    {
                        // Log system metrics every 30 seconds
                        LogPerformanceMetric("System.Memory.WorkingSet", Environment.WorkingSet, "bytes");
                        LogPerformanceMetric("System.Memory.GC", GC.GetTotalMemory(false), "bytes");
                        LogPerformanceMetric("System.Threads.Count", Process.GetCurrentProcess().Threads.Count, "count");

                        await Task.Delay(TimeSpan.FromSeconds(30));
                    }
                    catch (Exception ex)
                    {
                        _earlyLogger?.Error(ex, "Error in system monitoring");
                        await Task.Delay(TimeSpan.FromMinutes(1)); // Back off on error
                    }
                }
            });
        }

        private void StartGGsProcessMonitoring()
        {
            Task.Run(async () =>
            {
                while (_isCapturing && !_disposed)
                {
                    try
                    {
                        // Monitor for GGs-related processes
                        var processes = Process.GetProcesses();
                        foreach (var process in processes)
                        {
                            try
                            {
                                if (IsGGsRelatedProcess(process.ProcessName))
                                {
                                    var processId = process.Id;
                                    if (!_monitoredProcesses.ContainsKey(processId))
                                    {
                                        var processInfo = new ProcessInfo
                                        {
                                            Id = processId,
                                            Name = process.ProcessName,
                                            StartTime = process.StartTime,
                                            FirstSeen = DateTime.UtcNow
                                        };

                                        _monitoredProcesses[processId] = processInfo;
                                        LogProcessEvent(process.ProcessName, "Started", $"PID: {processId}");
                                    }
                                }
                            }
                            catch
                            {
                                // Ignore access denied errors for system processes
                            }
                        }

                        // Check for terminated processes
                        var currentProcessIds = new HashSet<int>();
                        foreach (var process in processes)
                        {
                            currentProcessIds.Add(process.Id);
                        }

                        var terminatedProcesses = new List<int>();
                        foreach (var kvp in _monitoredProcesses)
                        {
                            if (!currentProcessIds.Contains(kvp.Key))
                            {
                                terminatedProcesses.Add(kvp.Key);
                                LogProcessEvent(kvp.Value.Name, "Terminated", $"PID: {kvp.Key}, Runtime: {DateTime.UtcNow - kvp.Value.FirstSeen}");
                            }
                        }

                        foreach (var pid in terminatedProcesses)
                        {
                            _monitoredProcesses.TryRemove(pid, out _);
                        }

                        await Task.Delay(TimeSpan.FromSeconds(5));
                    }
                    catch (Exception ex)
                    {
                        _earlyLogger?.Error(ex, "Error in GGs process monitoring");
                        await Task.Delay(TimeSpan.FromSeconds(30));
                    }
                }
            });
        }

        private static bool IsGGsRelatedProcess(string processName)
        {
            var lowerName = processName.ToLowerInvariant();
            return lowerName.Contains("ggs") || 
                   lowerName.Contains("launcher") || 
                   lowerName.Contains("errorlogviewer") ||
                   lowerName.Contains("agent");
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                _earlyLogger?.Fatal(e.ExceptionObject as Exception, 
                    "Unhandled exception in AppDomain. IsTerminating: {IsTerminating}", e.IsTerminating);
                
                FlushLogs(null); // Immediate flush on critical error
            }
            catch
            {
                // Last resort
                Console.WriteLine($"CRITICAL UNHANDLED EXCEPTION: {e.ExceptionObject}");
            }
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            try
            {
                _earlyLogger?.Error(e.Exception, "Unobserved task exception");
                e.SetObserved(); // Prevent process termination
            }
            catch
            {
                Console.WriteLine($"UNOBSERVED TASK EXCEPTION: {e.Exception}");
            }
        }

        private void FlushLogs(object? state)
        {
            try
            {
                // Process queued early log events
                var processedCount = 0;
                while (_earlyLogQueue.TryDequeue(out var logEvent) && processedCount < 1000)
                {
                    _earlyLogger?.Write(logEvent.Level, logEvent.MessageTemplate, logEvent.Properties.Values.ToArray());
                    processedCount++;
                }

                // Force flush all loggers
                (_earlyLogger as Serilog.Core.Logger)?.Dispose();
                (_applicationLogger as Serilog.Core.Logger)?.Dispose();
                (_performanceLogger as Serilog.Core.Logger)?.Dispose();

                // Recreate loggers for continued logging
                if (!_disposed)
                {
                    Initialize();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error flushing logs: {ex}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            StopCapturing();

            _flushTimer?.Dispose();
            
            try
            {
                (_earlyLogger as IDisposable)?.Dispose();
                (_applicationLogger as IDisposable)?.Dispose();
                (_performanceLogger as IDisposable)?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disposing loggers: {ex}");
            }
        }

        private class LogEvent
        {
            public DateTime Timestamp { get; set; }
            public LogEventLevel Level { get; set; }
            public string MessageTemplate { get; set; } = string.Empty;
            public Dictionary<string, object?> Properties { get; set; } = new();
        }

        private class ProcessInfo
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public DateTime StartTime { get; set; }
            public DateTime FirstSeen { get; set; }
        }
    }
}