using System;
using System.Text.Json;
using System.IO;
using System.Text.RegularExpressions;

namespace GGs.Desktop.Services.ErrorLogViewer;

public sealed class LogParser
{
    private static readonly Regex DesktopPattern = new(
        pattern: @"^(?<ts>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3})\s+\[(?<level>[A-Z]+)\]\s+(?<source>[^:]+):\s+(?<message>.*)$",
        options: RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex LauncherPattern = new(
        pattern: @"^(?<ts>\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z)\s+\[(?<level>[A-Z]+)\]\s+(?<message>.*)$",
        options: RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public LogEntryRecord ParseLine(string filePath, string? line, int lineNumber)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return CreateFallback("INFO", filePath, "[Empty line]", string.Empty, lineNumber);
        }

        line = line.TrimEnd('\r', '\n');

        // Try structured JSON (Serilog-style or OTLP)
        if (line.StartsWith("{", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var json = JsonDocument.Parse(line);
                var root = json.RootElement;
                var timestamp = TryReadTimestamp(root) ?? DateTime.UtcNow;
                var level = (root.TryGetProperty("level", out var levelElement) ? levelElement.GetString() : null)
                            ?? (root.TryGetProperty("Level", out var levelElement2) ? levelElement2.GetString() : null)
                            ?? "INFO";
                var message = (root.TryGetProperty("message", out var messageElement) ? messageElement.GetString() : null)
                              ?? (root.TryGetProperty("Message", out var messageElement2) ? messageElement2.GetString() : null)
                              ?? "[No message]";
                var source = (root.TryGetProperty("source", out var sourceElement) ? sourceElement.GetString() : null)
                             ?? (root.TryGetProperty("Source", out var sourceElement2) ? sourceElement2.GetString() : null)
                             ?? InferSource(filePath);

                return new LogEntryRecord
                {
                    Timestamp = timestamp,
                    Level = NormalizeLevel(level),
                    Source = Sanitize(source),
                    Message = Sanitize(message),
                    Raw = line,
                    FilePath = filePath,
                    LineNumber = lineNumber
                };
            }
            catch (JsonException)
            {
                // Fall back to text parsing below
            }
        }

        var desktopMatch = DesktopPattern.Match(line);
        if (desktopMatch.Success)
        {
            DateTime.TryParse(desktopMatch.Groups["ts"].Value, out var timestamp);
            var level = NormalizeLevel(desktopMatch.Groups["level"].Value);
            var source = desktopMatch.Groups["source"].Value;
            var message = desktopMatch.Groups["message"].Value;

            return new LogEntryRecord
            {
                Timestamp = timestamp == default ? DateTime.UtcNow : DateTime.SpecifyKind(timestamp, DateTimeKind.Local),
                Level = level,
                Source = Sanitize(source),
                Message = Sanitize(message),
                Raw = line,
                FilePath = filePath,
                LineNumber = lineNumber
            };
        }

        var launcherMatch = LauncherPattern.Match(line);
        if (launcherMatch.Success)
        {
            DateTime.TryParse(launcherMatch.Groups["ts"].Value, out var timestamp);
            var level = NormalizeLevel(launcherMatch.Groups["level"].Value);
            var message = launcherMatch.Groups["message"].Value;

            return new LogEntryRecord
            {
                Timestamp = timestamp == default ? DateTime.UtcNow : DateTime.SpecifyKind(timestamp, DateTimeKind.Utc),
                Level = level,
                Source = Sanitize(InferSource(filePath)),
                Message = Sanitize(message),
                Raw = line,
                FilePath = filePath,
                LineNumber = lineNumber
            };
        }

        // Fallback raw record
        return CreateFallback("INFO", filePath, line, line, lineNumber);
    }

    private static string NormalizeLevel(string? level)
    {
        if (string.IsNullOrWhiteSpace(level))
        {
            return "INFO";
        }

        return level.Trim().ToUpperInvariant() switch
        {
            "ERR" => "ERROR",
            "WARN" => "WARNING",
            "INF" => "INFO",
            "DBG" => "DEBUG",
            "TRACE" => "TRACE",
            var other => other
        };
    }

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "[Unknown]";
        }

        return value.Length > 4096 ? value[..4096] : value;
    }

    private static DateTime? TryReadTimestamp(JsonElement root)
    {
        if (root.TryGetProperty("timestamp", out var timestampElement) && timestampElement.ValueKind == JsonValueKind.String &&
            DateTime.TryParse(timestampElement.GetString(), out var timestamp))
        {
            return DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);
        }

        if (root.TryGetProperty("Timestamp", out var timestampElement2) && timestampElement2.ValueKind == JsonValueKind.String &&
            DateTime.TryParse(timestampElement2.GetString(), out var timestamp2))
        {
            return DateTime.SpecifyKind(timestamp2, DateTimeKind.Utc);
        }

        return null;
    }

    private static string InferSource(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return "[Unknown]";
        }

        var fileName = Path.GetFileName(filePath);
        if (fileName.StartsWith("desktop", StringComparison.OrdinalIgnoreCase))
        {
            return "Desktop";
        }

        if (fileName.Contains("server", StringComparison.OrdinalIgnoreCase))
        {
            return "Server";
        }

        if (fileName.StartsWith("launch", StringComparison.OrdinalIgnoreCase))
        {
            return "Launcher";
        }

        return fileName;
    }

    private static LogEntryRecord CreateFallback(string level, string filePath, string message, string raw, int lineNumber)
        => new()
        {
            Timestamp = DateTime.UtcNow,
            Level = NormalizeLevel(level),
            Source = Sanitize(InferSource(filePath)),
            Message = Sanitize(message),
            Raw = raw ?? string.Empty,
            FilePath = string.IsNullOrWhiteSpace(filePath) ? "[Unknown]" : filePath,
            LineNumber = lineNumber
        };
}
