using System;
using Microsoft.Extensions.Logging;

namespace GGs.Desktop.Telemetry;

public static class TelemetryLogBridge
{
    private static ILogger? _logger;

    public static void Initialize(ILoggerFactory? factory)
    {
        try { _logger = factory?.CreateLogger("GGs.Desktop"); } catch { }
    }

    public static void Info(string message)
    {
        try { _logger?.LogInformation("{Message}", message); } catch { }
    }

    public static void Debug(string message)
    {
        try { _logger?.LogDebug("{Message}", message); } catch { }
    }

    public static void Warn(string message)
    {
        try { _logger?.LogWarning("{Message}", message); } catch { }
    }

    public static void Error(string message, Exception? ex = null)
    {
        try { _logger?.LogError(ex, "{Message}", message); } catch { }
    }
}
