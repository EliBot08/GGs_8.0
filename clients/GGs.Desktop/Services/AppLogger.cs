using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace GGs.Desktop.Services;

public static class AppLogger
{
    private static readonly object Sync = new();
    private static ILogger? _logger;
    private static string? _fallbackLogPath;

    public static void Initialize(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public static void Initialize()
    {
        try
        {
            var defaultDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GGs", "Logs");
            var defaultPath = Path.Combine(defaultDir, "desktop.log");
            ConfigureFallbackLogPath(defaultPath);
        }
        catch
        {
            // Ignore environment access errors in test contexts
        }
    }

    public static void ConfigureFallbackLogPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
            _fallbackLogPath = path;
        }
        catch
        {
            // Ignore IO failures; logging will fall back to in-memory logger only
        }
    }

    public static string? FallbackLogPath => _fallbackLogPath;

    public static void LogInfo(string message) => Log(LogLevel.Information, message, null);
    public static void LogDebug(string message) => Log(LogLevel.Debug, message, null);
    public static void LogWarning(string message) => Log(LogLevel.Warning, message, null);
    public static void LogWarn(string message) => Log(LogLevel.Warning, message, null);
    public static void LogError(string message) => Log(LogLevel.Error, message, null);
    public static void LogError(string message, Exception exception) => Log(LogLevel.Error, message, exception);
    public static void LogSuccess(string message) => Log(LogLevel.Information, $"SUCCESS: {message}", null);
    public static void LogCritical(string message, Exception exception) => Log(LogLevel.Critical, message, exception);
    public static void LogAppClosing() => Log(LogLevel.Information, "Application is closing", null);

    private static void Log(LogLevel level, string message, Exception? exception)
    {
        var safeMessage = string.IsNullOrWhiteSpace(message) ? "[no message]" : message;

        try
        {
            _logger?.Log(level, exception, safeMessage);
        }
        catch
        {
            // Never let downstream loggers throw up the stack
        }

        WriteFallback(level, safeMessage, exception);
    }

    private static void WriteFallback(LogLevel level, string message, Exception? exception)
    {
        var path = _fallbackLogPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var builder = new StringBuilder();
            builder.Append(DateTime.UtcNow.ToString("o"))
                   .Append(' ')
                   .Append('[').Append(level).Append(']')
                   .Append(' ')
                   .Append(message);

            if (exception != null)
            {
                builder.AppendLine();
                builder.Append(exception);
            }

            lock (Sync)
            {
                File.AppendAllText(path, builder.ToString() + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // Swallow all fallback write errors
        }
    }
}
