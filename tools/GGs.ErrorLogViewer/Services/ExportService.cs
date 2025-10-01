#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using GGs.ErrorLogViewer.Models;
using Microsoft.Extensions.Logging;

namespace GGs.ErrorLogViewer.Services
{
    public interface IExportService
    {
        Task<bool> ExportToCsvAsync(IEnumerable<LogEntry> logEntries, string filePath, bool includeDetails = true);
        Task<bool> ExportToJsonAsync(IEnumerable<LogEntry> logEntries, string filePath, bool prettyFormat = true);
        Task<bool> ExportToXmlAsync(IEnumerable<LogEntry> logEntries, string filePath);
        Task<bool> ExportToHtmlAsync(IEnumerable<LogEntry> logEntries, string filePath, bool darkTheme = true);
        Task<bool> ExportToTextAsync(IEnumerable<LogEntry> logEntries, string filePath, bool compactMode = false);
        string GetDefaultFileName(ExportFormat format);
        string GetFileFilter(ExportFormat format);
    }

    public enum ExportFormat
    {
        Csv,
        Json,
        Xml,
        Html,
        Text
    }

    public class ExportService : IExportService
    {
        private readonly ILogger<ExportService> _logger;

        public ExportService(ILogger<ExportService> logger)
        {
            _logger = logger;
        }

        public async Task<bool> ExportToCsvAsync(IEnumerable<LogEntry> logEntries, string filePath, bool includeDetails = true)
        {
            try
            {
                var csv = new StringBuilder();
                
                // Header
                if (includeDetails)
                {
                    csv.AppendLine("Timestamp,Level,Source,Message,Exception,StackTrace,Properties");
                }
                else
                {
                    csv.AppendLine("Timestamp,Level,Source,Message");
                }

                // Data rows
                foreach (var entry in logEntries)
                {
                    var timestamp = EscapeCsvField(entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                    var level = EscapeCsvField(entry.Level.ToString());
                    var source = EscapeCsvField(entry.Source ?? "");
                    var message = EscapeCsvField(entry.Message ?? "");

                    if (includeDetails)
                    {
                        var exception = EscapeCsvField(entry.Exception ?? "");
                        var stackTrace = EscapeCsvField(entry.StackTrace ?? "");
                        var properties = EscapeCsvField(GetEntryPropertiesString(entry));

                        csv.AppendLine($"{timestamp},{level},{source},{message},{exception},{stackTrace},{properties}");
                    }
                    else
                    {
                        csv.AppendLine($"{timestamp},{level},{source},{message}");
                    }
                }

                await File.WriteAllTextAsync(filePath, csv.ToString(), Encoding.UTF8);
                _logger.LogInformation("Successfully exported {Count} log entries to CSV: {FilePath}", 
                    logEntries.Count(), filePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export to CSV: {FilePath}", filePath);
                return false;
            }
        }

        public async Task<bool> ExportToJsonAsync(IEnumerable<LogEntry> logEntries, string filePath, bool prettyFormat = true)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = prettyFormat,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var exportData = new
                {
                    ExportInfo = new
                    {
                        Timestamp = DateTime.UtcNow,
                        TotalEntries = logEntries.Count(),
                        ExportedBy = "GGs ErrorLogViewer",
                        Version = "1.0.0"
                    },
                    LogEntries = logEntries.Select(entry => new
                    {
                        entry.Timestamp,
                        Level = entry.Level.ToString(),
                        entry.Source,
                        entry.Message,
                        entry.Exception,
                        entry.StackTrace,
                        entry.ThreadId,
                        entry.ProcessId,
                        entry.MachineName,
                        entry.UserName,
                        CompactMessage = entry.CompactMessage,
                        FormattedTimestamp = entry.FormattedTimestamp
                    })
                };

                var json = JsonSerializer.Serialize(exportData, options);
                await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
                
                _logger.LogInformation("Successfully exported {Count} log entries to JSON: {FilePath}", 
                    logEntries.Count(), filePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export to JSON: {FilePath}", filePath);
                return false;
            }
        }

        public async Task<bool> ExportToXmlAsync(IEnumerable<LogEntry> logEntries, string filePath)
        {
            try
            {
                var root = new XElement("LogExport",
                    new XAttribute("timestamp", DateTime.UtcNow),
                    new XAttribute("totalEntries", logEntries.Count()),
                    new XAttribute("exportedBy", "GGs ErrorLogViewer"),
                    
                    new XElement("LogEntries",
                        logEntries.Select(entry => new XElement("LogEntry",
                            new XAttribute("timestamp", entry.Timestamp),
                            new XAttribute("level", entry.Level.ToString()),
                            new XElement("Source", entry.Source ?? ""),
                            new XElement("Message", new XCData(entry.Message ?? "")),
                            entry.Exception != null ? new XElement("Exception", new XCData(entry.Exception)) : null,
                            entry.StackTrace != null ? new XElement("StackTrace", new XCData(entry.StackTrace)) : null,
                            GetEntryPropertiesXml(entry)
                        ))
                    )
                );

                await File.WriteAllTextAsync(filePath, root.ToString(), Encoding.UTF8);
                
                _logger.LogInformation("Successfully exported {Count} log entries to XML: {FilePath}", 
                    logEntries.Count(), filePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export to XML: {FilePath}", filePath);
                return false;
            }
        }

        public async Task<bool> ExportToHtmlAsync(IEnumerable<LogEntry> logEntries, string filePath, bool darkTheme = true)
        {
            try
            {
                var html = new StringBuilder();
                
                // HTML structure with embedded CSS
                html.AppendLine("<!DOCTYPE html>");
                html.AppendLine("<html lang=\"en\">");
                html.AppendLine("<head>");
                html.AppendLine("    <meta charset=\"UTF-8\">");
                html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
                html.AppendLine("    <title>GGs Error Log Export</title>");
                html.AppendLine("    <style>");
                
                // CSS styles
                if (darkTheme)
                {
                    html.AppendLine(GetDarkThemeCss());
                }
                else
                {
                    html.AppendLine(GetLightThemeCss());
                }
                
                html.AppendLine("    </style>");
                html.AppendLine("</head>");
                html.AppendLine("<body>");
                
                // Header
                html.AppendLine("    <div class=\"header\">");
                html.AppendLine("        <h1>GGs Error Log Export</h1>");
                html.AppendLine($"        <p>Exported on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");
                html.AppendLine($"        <p>Total Entries: {logEntries.Count()}</p>");
                html.AppendLine("    </div>");
                
                // Log entries
                html.AppendLine("    <div class=\"log-container\">");
                
                foreach (var entry in logEntries)
                {
                    var levelClass = GetLevelCssClass(entry.Level);
                    html.AppendLine($"        <div class=\"log-entry {levelClass}\">");
                    html.AppendLine($"            <div class=\"log-header\">");
                    html.AppendLine($"                <span class=\"timestamp\">{entry.FormattedTimestamp}</span>");
                    html.AppendLine($"                <span class=\"level level-{entry.Level.ToString().ToLower()}\">{entry.Level}</span>");
                    html.AppendLine($"                <span class=\"source\">{System.Web.HttpUtility.HtmlEncode(entry.Source ?? "")}</span>");
                    html.AppendLine($"            </div>");
                    html.AppendLine($"            <div class=\"message\">{System.Web.HttpUtility.HtmlEncode(entry.Message ?? "")}</div>");
                    
                    if (!string.IsNullOrEmpty(entry.Exception))
                    {
                        html.AppendLine($"            <div class=\"exception\">");
                        html.AppendLine($"                <strong>Exception:</strong><br>");
                        html.AppendLine($"                <pre>{System.Web.HttpUtility.HtmlEncode(entry.Exception)}</pre>");
                        html.AppendLine($"            </div>");
                    }
                    
                    if (!string.IsNullOrEmpty(entry.StackTrace))
                    {
                        html.AppendLine($"            <div class=\"stacktrace\">");
                        html.AppendLine($"                <strong>Stack Trace:</strong><br>");
                        html.AppendLine($"                <pre>{System.Web.HttpUtility.HtmlEncode(entry.StackTrace)}</pre>");
                        html.AppendLine($"            </div>");
                    }
                    
                    html.AppendLine($"        </div>");
                }
                
                html.AppendLine("    </div>");
                html.AppendLine("</body>");
                html.AppendLine("</html>");

                await File.WriteAllTextAsync(filePath, html.ToString(), Encoding.UTF8);
                
                _logger.LogInformation("Successfully exported {Count} log entries to HTML: {FilePath}", 
                    logEntries.Count(), filePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export to HTML: {FilePath}", filePath);
                return false;
            }
        }

        public async Task<bool> ExportToTextAsync(IEnumerable<LogEntry> logEntries, string filePath, bool compactMode = false)
        {
            try
            {
                var text = new StringBuilder();
                
                // Header
                text.AppendLine("=".PadRight(80, '='));
                text.AppendLine("GGs Error Log Export");
                text.AppendLine($"Exported on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                text.AppendLine($"Total Entries: {logEntries.Count()}");
                text.AppendLine($"Format: {(compactMode ? "Compact" : "Detailed")}");
                text.AppendLine("=".PadRight(80, '='));
                text.AppendLine();

                foreach (var entry in logEntries)
                {
                    if (compactMode)
                    {
                        // Compact format: one line per entry
                        text.AppendLine($"{entry.FormattedTimestamp} [{entry.Level}] {entry.Source}: {entry.CompactMessage}");
                    }
                    else
                    {
                        // Detailed format
                        text.AppendLine($"Timestamp: {entry.FormattedTimestamp}");
                        text.AppendLine($"Level:     {entry.Level}");
                        text.AppendLine($"Source:    {entry.Source ?? "Unknown"}");
                        text.AppendLine($"Message:   {entry.Message ?? ""}");
                        
                        if (!string.IsNullOrEmpty(entry.Exception))
                        {
                            text.AppendLine($"Exception: {entry.Exception}");
                        }
                        
                        if (!string.IsNullOrEmpty(entry.StackTrace))
                        {
                            text.AppendLine("Stack Trace:");
                            text.AppendLine(entry.StackTrace);
                        }
                        
                        // Additional properties
                        if (!string.IsNullOrEmpty(entry.ThreadId))
                            text.AppendLine($"Thread ID: {entry.ThreadId}");
                        if (!string.IsNullOrEmpty(entry.ProcessId))
                            text.AppendLine($"Process ID: {entry.ProcessId}");
                        if (!string.IsNullOrEmpty(entry.MachineName))
                            text.AppendLine($"Machine: {entry.MachineName}");
                        if (!string.IsNullOrEmpty(entry.UserName))
                            text.AppendLine($"User: {entry.UserName}");
                        
                        text.AppendLine("-".PadRight(80, '-'));
                    }
                }

                await File.WriteAllTextAsync(filePath, text.ToString(), Encoding.UTF8);
                
                _logger.LogInformation("Successfully exported {Count} log entries to Text: {FilePath}", 
                    logEntries.Count(), filePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export to Text: {FilePath}", filePath);
                return false;
            }
        }

        public string GetDefaultFileName(ExportFormat format)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            return format switch
            {
                ExportFormat.Csv => $"ggs_logs_{timestamp}.csv",
                ExportFormat.Json => $"ggs_logs_{timestamp}.json",
                ExportFormat.Xml => $"ggs_logs_{timestamp}.xml",
                ExportFormat.Html => $"ggs_logs_{timestamp}.html",
                ExportFormat.Text => $"ggs_logs_{timestamp}.txt",
                _ => $"ggs_logs_{timestamp}.txt"
            };
        }

        public string GetFileFilter(ExportFormat format)
        {
            return format switch
            {
                ExportFormat.Csv => "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                ExportFormat.Json => "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                ExportFormat.Xml => "XML Files (*.xml)|*.xml|All Files (*.*)|*.*",
                ExportFormat.Html => "HTML Files (*.html)|*.html|All Files (*.*)|*.*",
                ExportFormat.Text => "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                _ => "All Files (*.*)|*.*"
            };
        }

        private static string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return "\"\"";

            if (field.Contains("\"") || field.Contains(",") || field.Contains("\n") || field.Contains("\r"))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }

            return field;
        }

        private static string GetLevelCssClass(Models.LogLevel level)
        {
            return level switch
            {
                Models.LogLevel.Critical => "critical",
                Models.LogLevel.Error => "error",
                Models.LogLevel.Warning => "warning",
                Models.LogLevel.Information => "info",
                Models.LogLevel.Debug => "debug",
                Models.LogLevel.Trace => "trace",
                _ => "info"
            };
        }

        private static string GetDarkThemeCss()
        {
            return @"
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background-color: #1e1e1e;
            color: #d4d4d4;
            margin: 0;
            padding: 20px;
            line-height: 1.6;
        }
        .header {
            background-color: #2d2d30;
            padding: 20px;
            border-radius: 8px;
            margin-bottom: 20px;
            border-left: 4px solid #007acc;
        }
        .header h1 {
            margin: 0 0 10px 0;
            color: #ffffff;
        }
        .log-container {
            max-width: 100%;
        }
        .log-entry {
            background-color: #252526;
            margin-bottom: 10px;
            padding: 15px;
            border-radius: 6px;
            border-left: 4px solid #3c3c3c;
        }
        .log-entry.critical { border-left-color: #f44747; }
        .log-entry.error { border-left-color: #f44747; }
        .log-entry.warning { border-left-color: #ffcc02; }
        .log-entry.info { border-left-color: #007acc; }
        .log-entry.debug { border-left-color: #608b4e; }
        .log-entry.trace { border-left-color: #9cdcfe; }
        .log-header {
            display: flex;
            gap: 15px;
            margin-bottom: 10px;
            font-size: 0.9em;
        }
        .timestamp {
            color: #9cdcfe;
            font-family: 'Consolas', monospace;
        }
        .level {
            padding: 2px 8px;
            border-radius: 4px;
            font-weight: bold;
            font-size: 0.8em;
        }
        .level-critical { background-color: #f44747; color: white; }
        .level-error { background-color: #f44747; color: white; }
        .level-warning { background-color: #ffcc02; color: black; }
        .level-information { background-color: #007acc; color: white; }
        .level-debug { background-color: #608b4e; color: white; }
        .level-trace { background-color: #9cdcfe; color: black; }
        .source {
            color: #dcdcaa;
            font-weight: 500;
        }
        .message {
            color: #d4d4d4;
            margin-bottom: 10px;
            word-wrap: break-word;
        }
        .exception, .stacktrace {
            background-color: #1e1e1e;
            padding: 10px;
            border-radius: 4px;
            margin-top: 10px;
        }
        .exception strong, .stacktrace strong {
            color: #f44747;
        }
        pre {
            margin: 5px 0 0 0;
            font-family: 'Consolas', monospace;
            font-size: 0.85em;
            white-space: pre-wrap;
            word-wrap: break-word;
        }";
        }

        private static string GetLightThemeCss()
        {
            return @"
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background-color: #ffffff;
            color: #333333;
            margin: 0;
            padding: 20px;
            line-height: 1.6;
        }
        .header {
            background-color: #f8f9fa;
            padding: 20px;
            border-radius: 8px;
            margin-bottom: 20px;
            border-left: 4px solid #0078d4;
            border: 1px solid #e1e5e9;
        }
        .header h1 {
            margin: 0 0 10px 0;
            color: #323130;
        }
        .log-container {
            max-width: 100%;
        }
        .log-entry {
            background-color: #fafafa;
            margin-bottom: 10px;
            padding: 15px;
            border-radius: 6px;
            border-left: 4px solid #e1e5e9;
            border: 1px solid #e1e5e9;
        }
        .log-entry.critical { border-left-color: #d13438; }
        .log-entry.error { border-left-color: #d13438; }
        .log-entry.warning { border-left-color: #ff8c00; }
        .log-entry.info { border-left-color: #0078d4; }
        .log-entry.debug { border-left-color: #107c10; }
        .log-entry.trace { border-left-color: #5c2d91; }
        .log-header {
            display: flex;
            gap: 15px;
            margin-bottom: 10px;
            font-size: 0.9em;
        }
        .timestamp {
            color: #5c2d91;
            font-family: 'Consolas', monospace;
        }
        .level {
            padding: 2px 8px;
            border-radius: 4px;
            font-weight: bold;
            font-size: 0.8em;
        }
        .level-critical { background-color: #d13438; color: white; }
        .level-error { background-color: #d13438; color: white; }
        .level-warning { background-color: #ff8c00; color: white; }
        .level-information { background-color: #0078d4; color: white; }
        .level-debug { background-color: #107c10; color: white; }
        .level-trace { background-color: #5c2d91; color: white; }
        .source {
            color: #323130;
            font-weight: 500;
        }
        .message {
            color: #323130;
            margin-bottom: 10px;
            word-wrap: break-word;
        }
        .exception, .stacktrace {
            background-color: #f3f2f1;
            padding: 10px;
            border-radius: 4px;
            margin-top: 10px;
            border: 1px solid #e1e5e9;
        }
        .exception strong, .stacktrace strong {
            color: #d13438;
        }
        pre {
            margin: 5px 0 0 0;
            font-family: 'Consolas', monospace;
            font-size: 0.85em;
            white-space: pre-wrap;
            word-wrap: break-word;
        }";
        }

        private string GetEntryPropertiesString(LogEntry entry)
        {
            var properties = new List<string>();
            
            if (!string.IsNullOrEmpty(entry.ThreadId))
                properties.Add($"ThreadId={entry.ThreadId}");
            if (!string.IsNullOrEmpty(entry.ProcessId))
                properties.Add($"ProcessId={entry.ProcessId}");
            if (!string.IsNullOrEmpty(entry.MachineName))
                properties.Add($"MachineName={entry.MachineName}");
            if (!string.IsNullOrEmpty(entry.UserName))
                properties.Add($"UserName={entry.UserName}");
            if (!string.IsNullOrEmpty(entry.Category))
                properties.Add($"Category={entry.Category}");
                
            return string.Join("; ", properties);
        }

        private XElement? GetEntryPropertiesXml(LogEntry entry)
        {
            var properties = new List<XElement>();
            
            if (!string.IsNullOrEmpty(entry.ThreadId))
                properties.Add(new XElement("Property", new XAttribute("key", "ThreadId"), new XAttribute("value", entry.ThreadId)));
            if (!string.IsNullOrEmpty(entry.ProcessId))
                properties.Add(new XElement("Property", new XAttribute("key", "ProcessId"), new XAttribute("value", entry.ProcessId)));
            if (!string.IsNullOrEmpty(entry.MachineName))
                properties.Add(new XElement("Property", new XAttribute("key", "MachineName"), new XAttribute("value", entry.MachineName)));
            if (!string.IsNullOrEmpty(entry.UserName))
                properties.Add(new XElement("Property", new XAttribute("key", "UserName"), new XAttribute("value", entry.UserName)));
            if (!string.IsNullOrEmpty(entry.Category))
                properties.Add(new XElement("Property", new XAttribute("key", "Category"), new XAttribute("value", entry.Category)));
                
            return properties.Any() ? new XElement("Properties", properties) : null;
        }
    }
}