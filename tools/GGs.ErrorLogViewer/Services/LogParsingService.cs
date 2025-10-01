#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using GGs.ErrorLogViewer.Models;

namespace GGs.ErrorLogViewer.Services
{
    public interface ILogParsingService
    {
        LogEntry? ParseLogLine(string line, string filePath, long lineNumber);
        string SimplifyMessage(string message);
        string ExtractExceptionType(string message);
    }

    public class LogParsingService : ILogParsingService
    {
        private readonly ILogger<LogParsingService> _logger;

        // Enhanced regex patterns for different log formats
        private static readonly Regex GGsDesktopPattern = new(
            @"^(?<level>START|OK|INFO|WARN|DEBUG|ERROR|TRACE|CRITICAL)\s+(?<timestamp>\d{4}-\d{2}-\d{2}[T\s]\d{2}:\d{2}:\d{2}(?:\.\d{3})?(?:Z|[+-]\d{2}:\d{2})?)\s+(?<emoji>\p{So}|\S+)?\s*(?<message>.*)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex LauncherPattern = new(
            @"^\[(?<timestamp>[^\]]+)\]\s+\[(?<level>INFO|WARNING|ERROR|SUCCESS|DEBUG|TRACE|CRITICAL)\]\s+(?<message>.*)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex SerilogPattern = new(
            @"^(?<timestamp>\d{4}-\d{2}-\d{2}\s\d{2}:\d{2}:\d{2}\.\d{3}\s[+-]\d{2}:\d{2})\s\[(?<level>[^\]]+)\]\s(?<message>.*)$",
            RegexOptions.Compiled);

        private static readonly Regex GenericPattern = new(
            @"^(?<timestamp>\d{4}[-/]\d{2}[-/]\d{2}[T\s]\d{2}:\d{2}:\d{2}(?:\.\d{3})?(?:Z|[+-]\d{2}:\d{2})?)\s*(?<level>TRACE|DEBUG|INFO|INFORMATION|WARN|WARNING|ERROR|FATAL|CRITICAL)?\s*(?<message>.*)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ExceptionPattern = new(
            @"(?<type>\w+(?:\.\w+)*Exception):\s*(?<message>.*?)(?:\s+at\s+(?<stacktrace>.*))?$",
            RegexOptions.Compiled | RegexOptions.Singleline);

        public LogParsingService(ILogger<LogParsingService> logger)
        {
            _logger = logger;
        }

        public LogEntry? ParseLogLine(string line, string filePath, long lineNumber)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;

            try
            {
                // Try JSON format first (structured logging)
                if (line.TrimStart().StartsWith("{"))
                {
                    var jsonEntry = TryParseJsonLog(line, filePath, lineNumber);
                    if (jsonEntry != null)
                        return jsonEntry;
                }

                // Try various text formats
                var textEntry = TryParseTextLog(line, filePath, lineNumber);
                if (textEntry != null)
                    return textEntry;

                // Fallback: create a basic entry
                return CreateFallbackEntry(line, filePath, lineNumber);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing log line from {FilePath}:{LineNumber}", filePath, lineNumber);
                return CreateErrorEntry(line, filePath, lineNumber, ex.Message);
            }
        }

        private LogEntry? TryParseJsonLog(string line, string filePath, long lineNumber)
        {
            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;

                var entry = new LogEntry
                {
                    FilePath = filePath,
                    LineNumber = lineNumber,
                    RawLine = line,
                    Source = InferSource(filePath)
                };

                // Extract timestamp
                if (TryGetJsonProperty(root, "Timestamp", "timestamp", "@timestamp", "time") is { } timestampElement)
                {
                    if (DateTime.TryParse(timestampElement.GetString(), out var timestamp))
                        entry.Timestamp = timestamp;
                    else
                        entry.Timestamp = DateTime.Now;
                }
                else
                {
                    entry.Timestamp = DateTime.Now;
                }

                // Extract level
                if (TryGetJsonProperty(root, "Level", "level", "severity", "LogLevel") is { } levelElement)
                {
                    entry.Level = ParseLogLevel(levelElement.GetString() ?? "Information");
                }
                else
                {
                    entry.Level = Models.LogLevel.Information;
                }

                // Extract message
                if (TryGetJsonProperty(root, "Message", "message", "msg", "text") is { } messageElement)
                {
                    entry.Message = messageElement.GetString() ?? string.Empty;
                }

                // Extract additional properties
                if (TryGetJsonProperty(root, "Category", "category", "logger", "source") is { } categoryElement)
                {
                    entry.Category = categoryElement.GetString();
                }

                if (TryGetJsonProperty(root, "ThreadId", "threadId", "thread") is { } threadElement)
                {
                    entry.ThreadId = threadElement.GetString();
                }

                if (TryGetJsonProperty(root, "ProcessId", "processId", "process", "pid") is { } processElement)
                {
                    entry.ProcessId = processElement.GetString();
                }

                // Extract exception information
                if (TryGetJsonProperty(root, "Exception", "exception", "error") is { } exceptionElement)
                {
                    if (exceptionElement.ValueKind == JsonValueKind.Object)
                    {
                        if (TryGetJsonProperty(exceptionElement, "Type", "type", "ExceptionType") is { } typeElement)
                        {
                            entry.Exception = typeElement.GetString();
                        }
                        if (TryGetJsonProperty(exceptionElement, "StackTrace", "stackTrace", "stack") is { } stackElement)
                        {
                            entry.StackTrace = stackElement.GetString();
                        }
                    }
                    else
                    {
                        entry.Exception = exceptionElement.GetString();
                    }
                }

                return entry;
            }
            catch
            {
                return null;
            }
        }

        private LogEntry? TryParseTextLog(string line, string filePath, long lineNumber)
        {
            // Try GGs Desktop format
            var match = GGsDesktopPattern.Match(line);
            if (match.Success)
            {
                return new LogEntry
                {
                    FilePath = filePath,
                    LineNumber = lineNumber,
                    RawLine = line,
                    Timestamp = ParseTimestamp(match.Groups["timestamp"].Value),
                    Level = ParseLogLevel(match.Groups["level"].Value),
                    Source = InferSource(filePath),
                    Message = match.Groups["message"].Value.Trim()
                };
            }

            // Try Launcher format
            match = LauncherPattern.Match(line);
            if (match.Success)
            {
                var level = match.Groups["level"].Value switch
                {
                    "SUCCESS" => Models.LogLevel.Success,
                    "WARNING" => Models.LogLevel.Warning,
                    _ => ParseLogLevel(match.Groups["level"].Value)
                };

                return new LogEntry
                {
                    FilePath = filePath,
                    LineNumber = lineNumber,
                    RawLine = line,
                    Timestamp = ParseTimestamp(match.Groups["timestamp"].Value),
                    Level = level,
                    Source = InferSource(filePath),
                    Message = match.Groups["message"].Value.Trim()
                };
            }

            // Try Serilog format
            match = SerilogPattern.Match(line);
            if (match.Success)
            {
                return new LogEntry
                {
                    FilePath = filePath,
                    LineNumber = lineNumber,
                    RawLine = line,
                    Timestamp = ParseTimestamp(match.Groups["timestamp"].Value),
                    Level = ParseLogLevel(match.Groups["level"].Value),
                    Source = InferSource(filePath),
                    Message = match.Groups["message"].Value.Trim()
                };
            }

            // Try generic format
            match = GenericPattern.Match(line);
            if (match.Success)
            {
                return new LogEntry
                {
                    FilePath = filePath,
                    LineNumber = lineNumber,
                    RawLine = line,
                    Timestamp = ParseTimestamp(match.Groups["timestamp"].Value),
                    Level = ParseLogLevel(match.Groups["level"].Value),
                    Source = InferSource(filePath),
                    Message = match.Groups["message"].Value.Trim()
                };
            }

            return null;
        }

        private LogEntry CreateFallbackEntry(string line, string filePath, long lineNumber)
        {
            var entry = new LogEntry
            {
                FilePath = filePath,
                LineNumber = lineNumber,
                RawLine = line,
                Timestamp = DateTime.Now,
                Level = Models.LogLevel.Information,
                Source = InferSource(filePath),
                Message = line.Trim()
            };

            // Try to extract exception information from the message
            var exceptionMatch = ExceptionPattern.Match(line);
            if (exceptionMatch.Success)
            {
                entry.Level = Models.LogLevel.Error;
                entry.Exception = exceptionMatch.Groups["type"].Value;
                if (exceptionMatch.Groups["stacktrace"].Success)
                {
                    entry.StackTrace = exceptionMatch.Groups["stacktrace"].Value;
                }
            }

            return entry;
        }

        private LogEntry CreateErrorEntry(string line, string filePath, long lineNumber, string error)
        {
            return new LogEntry
            {
                FilePath = filePath,
                LineNumber = lineNumber,
                RawLine = line,
                Timestamp = DateTime.Now,
                Level = Models.LogLevel.Warning,
                Source = "Parser",
                Message = $"Failed to parse log line: {error}. Raw: {line.Substring(0, Math.Min(100, line.Length))}"
            };
        }

        private static JsonElement? TryGetJsonProperty(JsonElement element, params string[] propertyNames)
        {
            foreach (var name in propertyNames)
            {
                if (element.TryGetProperty(name, out var property))
                    return property;
            }
            return null;
        }

        private static DateTime ParseTimestamp(string timestampStr)
        {
            if (string.IsNullOrWhiteSpace(timestampStr))
                return DateTime.Now;

            // Try various timestamp formats
            var formats = new[]
            {
                "yyyy-MM-dd HH:mm:ss.fff",
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-ddTHH:mm:ss.fffZ",
                "yyyy-MM-ddTHH:mm:ssZ",
                "yyyy-MM-ddTHH:mm:ss.fff",
                "yyyy-MM-ddTHH:mm:ss",
                "yyyy-MM-dd HH:mm:ss.fff zzz",
                "yyyy-MM-dd HH:mm:ss zzz",
                "MM/dd/yyyy HH:mm:ss",
                "dd/MM/yyyy HH:mm:ss"
            };

            foreach (var format in formats)
            {
                if (DateTime.TryParseExact(timestampStr, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
                    return result;
            }

            // Fallback to general parsing
            if (DateTime.TryParse(timestampStr, out var fallbackResult))
                return fallbackResult;

            return DateTime.Now;
        }

        private static Models.LogLevel ParseLogLevel(string? levelStr)
        {
            if (string.IsNullOrWhiteSpace(levelStr))
                return Models.LogLevel.Information;

            return levelStr.ToUpperInvariant() switch
            {
                "TRACE" or "VERBOSE" => Models.LogLevel.Trace,
                "DEBUG" or "DBG" => Models.LogLevel.Debug,
                "INFO" or "INFORMATION" or "START" => Models.LogLevel.Information,
                "WARN" or "WARNING" => Models.LogLevel.Warning,
                "ERROR" or "ERR" => Models.LogLevel.Error,
                "FATAL" or "CRITICAL" or "CRIT" => Models.LogLevel.Critical,
                "OK" or "SUCCESS" => Models.LogLevel.Success,
                _ => Models.LogLevel.Information
            };
        }

        private static string InferSource(string filePath)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            
            if (fileName.Contains("desktop", StringComparison.OrdinalIgnoreCase))
                return "Desktop";
            if (fileName.Contains("server", StringComparison.OrdinalIgnoreCase))
                return "Server";
            if (fileName.Contains("launcher", StringComparison.OrdinalIgnoreCase))
                return "Launcher";
            if (fileName.Contains("agent", StringComparison.OrdinalIgnoreCase))
                return "Agent";
            if (fileName.Contains("errorlogviewer", StringComparison.OrdinalIgnoreCase))
                return "LogViewer";

            return fileName;
        }

        public string SimplifyMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return string.Empty;

            var simplified = message;

            // Remove common technical prefixes
            simplified = simplified
                .Replace("System.", "")
                .Replace("Microsoft.", "")
                .Replace("GGs.Desktop.", "")
                .Replace("GGs.Shared.", "");

            // Simplify common error patterns
            simplified = Regex.Replace(simplified, @"at\s+[\w\.]+\([^)]*\)\s+in\s+[^:]+:\d+", " (in code)", RegexOptions.IgnoreCase);
            simplified = Regex.Replace(simplified, @"Exception:\s*", "", RegexOptions.IgnoreCase);

            // Clean up whitespace
            simplified = Regex.Replace(simplified, @"\s+", " ").Trim();

            return simplified;
        }

        public string ExtractExceptionType(string message)
        {
            var match = ExceptionPattern.Match(message);
            if (match.Success)
            {
                var type = match.Groups["type"].Value;
                // Remove namespace prefixes for readability
                var lastDot = type.LastIndexOf('.');
                return lastDot > 0 ? type.Substring(lastDot + 1) : type;
            }

            return string.Empty;
        }
    }
}