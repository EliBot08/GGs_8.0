#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GGs.ErrorLogViewer.Models;
using Microsoft.Extensions.Logging;

namespace GGs.ErrorLogViewer.Services
{
    public interface IExternalLogSourceService
    {
        Task<List<LogEntry>> ImportFromWindowsEventLogAsync(string logName = "Application", 
            DateTime? since = null, int maxEntries = 1000);
        Task<List<LogEntry>> ImportFromSyslogAsync(string filePath);
        Task<List<LogEntry>> ParseCustomFormatAsync(string filePath, string formatRegex);
        List<string> GetAvailableWindowsEventLogs();
    }

    public class ExternalLogSourceService : IExternalLogSourceService
    {
        private readonly ILogger<ExternalLogSourceService> _logger;
        private readonly ILogParsingService _parsingService;
        private long _nextLogId = 1000000; // Start from high number to avoid conflicts

        public ExternalLogSourceService(
            ILogger<ExternalLogSourceService> logger,
            ILogParsingService parsingService)
        {
            _logger = logger;
            _parsingService = parsingService;
        }

        public async Task<List<LogEntry>> ImportFromWindowsEventLogAsync(string logName = "Application",
            DateTime? since = null, int maxEntries = 1000)
        {
            var logs = new List<LogEntry>();

            try
            {
                _logger.LogInformation("Importing from Windows Event Log: {LogName}", logName);

                await Task.Run(() =>
                {
                    using var eventLog = new EventLog(logName);
                    var cutoff = since ?? DateTime.Now.AddDays(-7);
                    var entries = eventLog.Entries
                        .Cast<EventLogEntry>()
                        .Where(e => e.TimeGenerated >= cutoff)
                        .OrderByDescending(e => e.TimeGenerated)
                        .Take(maxEntries);

                    foreach (var entry in entries)
                    {
                        var logEntry = new LogEntry
                        {
                            Id = _nextLogId++,
                            Timestamp = entry.TimeGenerated,
                            Level = ConvertEventLogLevel(entry.EntryType),
                            Source = $"EventLog:{entry.Source}",
                            Message = entry.Message,
                            Category = entry.Category,
                            MachineName = entry.MachineName,
                            UserName = entry.UserName,
                            RawLine = $"[{entry.TimeGenerated:yyyy-MM-dd HH:mm:ss}] [{entry.EntryType}] {entry.Source}: {entry.Message}",
                            FilePath = logName,
                            LineNumber = entry.Index
                        };

                        logs.Add(logEntry);
                    }
                });

                _logger.LogInformation("Imported {Count} entries from Windows Event Log", logs.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import from Windows Event Log: {LogName}", logName);
            }

            return logs;
        }

        public async Task<List<LogEntry>> ImportFromSyslogAsync(string filePath)
        {
            var logs = new List<LogEntry>();

            try
            {
                _logger.LogInformation("Importing from syslog file: {FilePath}", filePath);

                var lines = await System.IO.File.ReadAllLinesAsync(filePath);
                var lineNumber = 0;

                // Common syslog format: <priority>timestamp hostname tag: message
                var syslogRegex = new Regex(
                    @"^(?:<\d+>)?(\w+\s+\d+\s+\d+:\d+:\d+)\s+(\S+)\s+(\S+?):\s*(.*)$",
                    RegexOptions.Compiled);

                foreach (var line in lines)
                {
                    lineNumber++;
                    var match = syslogRegex.Match(line);

                    if (match.Success)
                    {
                        DateTime timestamp;
                        try
                        {
                            // Parse syslog timestamp (e.g., "Jan 15 10:30:45")
                            var timeStr = match.Groups[1].Value;
                            timestamp = DateTime.Parse($"{DateTime.Now.Year} {timeStr}");
                        }
                        catch
                        {
                            timestamp = DateTime.Now;
                        }

                        var logEntry = new LogEntry
                        {
                            Id = _nextLogId++,
                            Timestamp = timestamp,
                            Level = InferLogLevelFromMessage(match.Groups[4].Value),
                            Source = $"Syslog:{match.Groups[3].Value}",
                            Message = match.Groups[4].Value,
                            MachineName = match.Groups[2].Value,
                            RawLine = line,
                            FilePath = filePath,
                            LineNumber = lineNumber
                        };

                        logs.Add(logEntry);
                    }
                    else
                    {
                        // Try basic parsing
                        var parsed = _parsingService.ParseLogLine(line, filePath, lineNumber);
                        if (parsed != null)
                        {
                            parsed.Id = _nextLogId++;
                            parsed.Source = "Syslog:Unknown";
                            logs.Add(parsed);
                        }
                    }
                }

                _logger.LogInformation("Imported {Count} entries from syslog file", logs.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import from syslog: {FilePath}", filePath);
            }

            return logs;
        }

        public async Task<List<LogEntry>> ParseCustomFormatAsync(string filePath, string formatRegex)
        {
            var logs = new List<LogEntry>();

            try
            {
                _logger.LogInformation("Parsing custom format from: {FilePath}", filePath);

                var lines = await System.IO.File.ReadAllLinesAsync(filePath);
                var regex = new Regex(formatRegex, RegexOptions.Compiled | RegexOptions.Multiline);
                var lineNumber = 0;

                foreach (var line in lines)
                {
                    lineNumber++;
                    var match = regex.Match(line);

                    if (match.Success)
                    {
                        var logEntry = new LogEntry
                        {
                            Id = _nextLogId++,
                            Timestamp = TryParseTimestamp(match.Groups["timestamp"]?.Value),
                            Level = ParseLogLevel(match.Groups["level"]?.Value),
                            Source = match.Groups["source"]?.Value ?? "Custom",
                            Message = match.Groups["message"]?.Value ?? line,
                            RawLine = line,
                            FilePath = filePath,
                            LineNumber = lineNumber
                        };

                        logs.Add(logEntry);
                    }
                }

                _logger.LogInformation("Parsed {Count} entries with custom format", logs.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse custom format: {FilePath}", filePath);
            }

            return logs;
        }

        public List<string> GetAvailableWindowsEventLogs()
        {
            try
            {
                return EventLog.GetEventLogs()
                    .Select(log => log.Log)
                    .OrderBy(name => name)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get available Windows Event Logs");
                return new List<string> { "Application", "System", "Security" };
            }
        }

        // Helper methods

        private LogLevel ConvertEventLogLevel(EventLogEntryType entryType)
        {
            return entryType switch
            {
                EventLogEntryType.Error => LogLevel.Error,
                EventLogEntryType.Warning => LogLevel.Warning,
                EventLogEntryType.Information => LogLevel.Information,
                EventLogEntryType.SuccessAudit => LogLevel.Success,
                EventLogEntryType.FailureAudit => LogLevel.Error,
                _ => LogLevel.Information
            };
        }

        private LogLevel InferLogLevelFromMessage(string message)
        {
            var lower = message.ToLowerInvariant();

            if (lower.Contains("critical") || lower.Contains("fatal"))
                return LogLevel.Critical;
            if (lower.Contains("error") || lower.Contains("exception"))
                return LogLevel.Error;
            if (lower.Contains("warning") || lower.Contains("warn"))
                return LogLevel.Warning;
            if (lower.Contains("debug"))
                return LogLevel.Debug;
            if (lower.Contains("trace"))
                return LogLevel.Trace;

            return LogLevel.Information;
        }

        private DateTime TryParseTimestamp(string? timestampStr)
        {
            if (string.IsNullOrEmpty(timestampStr))
                return DateTime.Now;

            if (DateTime.TryParse(timestampStr, out var result))
                return result;

            return DateTime.Now;
        }

        private LogLevel ParseLogLevel(string? levelStr)
        {
            if (string.IsNullOrEmpty(levelStr))
                return LogLevel.Information;

            if (Enum.TryParse<LogLevel>(levelStr, true, out var result))
                return result;

            return InferLogLevelFromMessage(levelStr);
        }
    }
}
