using Microsoft.Extensions.Logging;

namespace GGs.Desktop.Services
{
    public static class AppLogger
    {
        private static ILogger? _logger;

        public static void Initialize(ILogger logger)
        {
            _logger = logger;
        }

        public static void Initialize()
        {
            // Default initialization
        }

        public static void LogInfo(string message)
        {
            _logger?.LogInformation(message);
        }

        public static void LogError(string message)
        {
            _logger?.LogError(message);
        }

        public static void LogError(string message, Exception ex)
        {
            _logger?.LogError(ex, message);
        }

        public static void LogWarning(string message)
        {
            _logger?.LogWarning(message);
        }

        public static void LogWarn(string message)
        {
            _logger?.LogWarning(message);
        }

        public static void LogSuccess(string message)
        {
            _logger?.LogInformation($"SUCCESS: {message}");
        }

        public static void LogDebug(string message)
        {
            _logger?.LogDebug(message);
        }

        public static string LogFilePath { get; set; } = "app.log";

        public static void LogAppClosing()
        {
            _logger?.LogInformation("Application is closing");
        }
    }
}