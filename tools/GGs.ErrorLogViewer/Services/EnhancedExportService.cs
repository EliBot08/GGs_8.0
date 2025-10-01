#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GGs.ErrorLogViewer.Models;
using Microsoft.Extensions.Logging;

namespace GGs.ErrorLogViewer.Services
{
    public interface IEnhancedExportService : IExportService
    {
        Task<bool> ExportToPdfAsync(IEnumerable<LogEntry> logEntries, string filePath, 
            string title = "Error Log Report", bool includeCharts = true);
        Task<bool> ExportToMarkdownAsync(IEnumerable<LogEntry> logEntries, string filePath);
        Task<bool> ExportWithTemplateAsync(IEnumerable<LogEntry> logEntries, string filePath, 
            string templatePath, ExportFormat format);
        Task<bool> ExportLast24HoursReportAsync(IEnumerable<LogEntry> logEntries, string filePath);
    }

    public class EnhancedExportService : ExportService, IEnhancedExportService
    {
        private readonly IAnalyticsEngine _analytics;
        private readonly ILogger<EnhancedExportService> _enhancedLogger;

        public EnhancedExportService(
            ILogger<EnhancedExportService> logger,
            IAnalyticsEngine analytics) : base(logger)
        {
            _enhancedLogger = logger;
            _analytics = analytics;
        }

        public async Task<bool> ExportToPdfAsync(IEnumerable<LogEntry> logEntries, string filePath,
            string title = "Error Log Report", bool includeCharts = true)
        {
            try
            {
                var entries = logEntries.ToList();
                var stats = _analytics.GetStatistics(entries);
                
                // Generate HTML first, then convert to PDF
                var html = GeneratePdfHtml(entries, stats, title, includeCharts);
                
                // For now, save as HTML (full PDF generation would require additional libraries like iTextSharp)
                // In production, this would use a PDF generation library
                var htmlPath = Path.ChangeExtension(filePath, ".html");
                await File.WriteAllTextAsync(htmlPath, html, Encoding.UTF8);
                
                _enhancedLogger.LogInformation("PDF report exported to HTML: {FilePath}", htmlPath);
                _enhancedLogger.LogWarning("Full PDF generation requires additional library - exported as HTML for now");
                
                return true;
            }
            catch (Exception ex)
            {
                _enhancedLogger.LogError(ex, "Failed to export PDF report: {FilePath}", filePath);
                return false;
            }
        }

        public async Task<bool> ExportToMarkdownAsync(IEnumerable<LogEntry> logEntries, string filePath)
        {
            try
            {
                var entries = logEntries.ToList();
                var stats = _analytics.GetStatistics(entries);
                var md = new StringBuilder();

                // Header
                md.AppendLine("# Error Log Report");
                md.AppendLine();
                md.AppendLine($"**Generated:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                md.AppendLine();

                // Statistics
                md.AppendLine("## Summary Statistics");
                md.AppendLine();
                md.AppendLine($"- **Total Logs:** {stats.TotalLogs:N0}");
                md.AppendLine($"- **Errors:** {stats.ErrorCount:N0} ({stats.ErrorRate:F1}%)");
                md.AppendLine($"- **Warnings:** {stats.WarningCount:N0} ({stats.WarningRate:F1}%)");
                md.AppendLine($"- **Critical:** {stats.CriticalCount:N0}");
                md.AppendLine($"- **Info:** {stats.InfoCount:N0}");
                md.AppendLine($"- **Time Range:** {stats.OldestLog:yyyy-MM-dd HH:mm} to {stats.NewestLog:yyyy-MM-dd HH:mm}");
                md.AppendLine($"- **Health Score:** {stats.HealthScore:F1}/100");
                md.AppendLine();

                // Top Errors
                var topErrors = _analytics.GetTopErrors(entries, 10);
                if (topErrors.Any())
                {
                    md.AppendLine("## Top Errors");
                    md.AppendLine();
                    md.AppendLine("| Error | Count |");
                    md.AppendLine("|-------|-------|");
                    foreach (var error in topErrors)
                    {
                        md.AppendLine($"| {EscapeMarkdown(error.Key)} | {error.Value} |");
                    }
                    md.AppendLine();
                }

                // Recent Critical Logs
                var criticalLogs = entries
                    .Where(e => e.Level == LogLevel.Critical || e.Level == LogLevel.Error)
                    .OrderByDescending(e => e.Timestamp)
                    .Take(20)
                    .ToList();

                if (criticalLogs.Any())
                {
                    md.AppendLine("## Recent Critical/Error Logs");
                    md.AppendLine();
                    foreach (var log in criticalLogs)
                    {
                        md.AppendLine($"### [{log.Level}] {log.FormattedTimestamp}");
                        md.AppendLine();
                        md.AppendLine($"**Source:** {log.Source}");
                        md.AppendLine();
                        md.AppendLine($"**Message:** {EscapeMarkdown(log.Message)}");
                        md.AppendLine();
                        if (!string.IsNullOrEmpty(log.Exception))
                        {
                            md.AppendLine("```");
                            md.AppendLine(log.Exception);
                            md.AppendLine("```");
                            md.AppendLine();
                        }
                    }
                }

                await File.WriteAllTextAsync(filePath, md.ToString(), Encoding.UTF8);
                _enhancedLogger.LogInformation("Exported {Count} log entries to Markdown: {FilePath}",
                    entries.Count, filePath);
                return true;
            }
            catch (Exception ex)
            {
                _enhancedLogger.LogError(ex, "Failed to export to Markdown: {FilePath}", filePath);
                return false;
            }
        }

        public async Task<bool> ExportWithTemplateAsync(IEnumerable<LogEntry> logEntries, string filePath,
            string templatePath, ExportFormat format)
        {
            try
            {
                if (!File.Exists(templatePath))
                {
                    _enhancedLogger.LogError("Template file not found: {TemplatePath}", templatePath);
                    return false;
                }

                var template = await File.ReadAllTextAsync(templatePath);
                var entries = logEntries.ToList();
                var stats = _analytics.GetStatistics(entries);

                // Replace template variables
                var output = template
                    .Replace("{{TOTAL_LOGS}}", stats.TotalLogs.ToString())
                    .Replace("{{ERROR_COUNT}}", stats.ErrorCount.ToString())
                    .Replace("{{WARNING_COUNT}}", stats.WarningCount.ToString())
                    .Replace("{{CRITICAL_COUNT}}", stats.CriticalCount.ToString())
                    .Replace("{{HEALTH_SCORE}}", stats.HealthScore.ToString("F1"))
                    .Replace("{{DATE}}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                    .Replace("{{TIME_RANGE}}", $"{stats.OldestLog:yyyy-MM-dd} to {stats.NewestLog:yyyy-MM-dd}");

                await File.WriteAllTextAsync(filePath, output, Encoding.UTF8);
                _enhancedLogger.LogInformation("Exported using template: {FilePath}", filePath);
                return true;
            }
            catch (Exception ex)
            {
                _enhancedLogger.LogError(ex, "Failed to export with template: {FilePath}", filePath);
                return false;
            }
        }

        public async Task<bool> ExportLast24HoursReportAsync(IEnumerable<LogEntry> logEntries, string filePath)
        {
            try
            {
                var cutoff = DateTime.Now.AddHours(-24);
                var recent = logEntries.Where(e => e.Timestamp >= cutoff).ToList();

                if (!recent.Any())
                {
                    _enhancedLogger.LogWarning("No logs found in the last 24 hours");
                    return false;
                }

                return await ExportToPdfAsync(recent, filePath, "Last 24 Hours Report", includeCharts: true);
            }
            catch (Exception ex)
            {
                _enhancedLogger.LogError(ex, "Failed to export 24-hour report: {FilePath}", filePath);
                return false;
            }
        }

        private string GeneratePdfHtml(List<LogEntry> entries, LogStatistics stats, string title, bool includeCharts)
        {
            var html = new StringBuilder();
            
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html><head>");
            html.AppendLine($"<title>{title}</title>");
            html.AppendLine("<style>");
            html.AppendLine(@"
                body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 40px; background: #f5f5f5; }
                .header { background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); 
                         color: white; padding: 30px; border-radius: 10px; margin-bottom: 30px; }
                .header h1 { margin: 0; font-size: 32px; }
                .header p { margin: 10px 0 0 0; opacity: 0.9; }
                .stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); 
                        gap: 20px; margin-bottom: 30px; }
                .stat-card { background: white; padding: 20px; border-radius: 8px; 
                           box-shadow: 0 2px 4px rgba(0,0,0,0.1); }
                .stat-card h3 { margin: 0 0 10px 0; color: #666; font-size: 14px; text-transform: uppercase; }
                .stat-card .value { font-size: 32px; font-weight: bold; color: #333; }
                .stat-card.error .value { color: #e74c3c; }
                .stat-card.warning .value { color: #f39c12; }
                .stat-card.success .value { color: #27ae60; }
                .log-table { background: white; border-radius: 8px; overflow: hidden; 
                           box-shadow: 0 2px 4px rgba(0,0,0,0.1); }
                .log-table table { width: 100%; border-collapse: collapse; }
                .log-table th { background: #667eea; color: white; padding: 15px; text-align: left; }
                .log-table td { padding: 12px 15px; border-bottom: 1px solid #eee; }
                .log-table tr:hover { background: #f8f9fa; }
                .level { display: inline-block; padding: 4px 12px; border-radius: 12px; 
                        font-size: 12px; font-weight: bold; }
                .level-error { background: #fee; color: #c00; }
                .level-warning { background: #ffc; color: #c60; }
                .level-info { background: #eff; color: #06c; }
                .level-critical { background: #f0e; color: #90c; }
                @media print { body { margin: 0; background: white; } }
            ");
            html.AppendLine("</style>");
            html.AppendLine("</head><body>");

            // Header
            html.AppendLine("<div class='header'>");
            html.AppendLine($"<h1>{title}</h1>");
            html.AppendLine($"<p>Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss} | Total Logs: {stats.TotalLogs:N0}</p>");
            html.AppendLine("</div>");

            // Statistics Cards
            html.AppendLine("<div class='stats'>");
            html.AppendLine($"<div class='stat-card error'><h3>Errors</h3><div class='value'>{stats.ErrorCount:N0}</div></div>");
            html.AppendLine($"<div class='stat-card warning'><h3>Warnings</h3><div class='value'>{stats.WarningCount:N0}</div></div>");
            html.AppendLine($"<div class='stat-card'><h3>Critical</h3><div class='value'>{stats.CriticalCount:N0}</div></div>");
            html.AppendLine($"<div class='stat-card success'><h3>Health Score</h3><div class='value'>{stats.HealthScore:F0}</div></div>");
            html.AppendLine("</div>");

            // Recent Critical/Error Logs
            var criticalLogs = entries
                .Where(e => e.Level == LogLevel.Critical || e.Level == LogLevel.Error)
                .OrderByDescending(e => e.Timestamp)
                .Take(50)
                .ToList();

            if (criticalLogs.Any())
            {
                html.AppendLine("<div class='log-table'>");
                html.AppendLine("<table>");
                html.AppendLine("<thead><tr><th>Timestamp</th><th>Level</th><th>Source</th><th>Message</th></tr></thead>");
                html.AppendLine("<tbody>");

                foreach (var log in criticalLogs)
                {
                    var levelClass = log.Level == LogLevel.Critical ? "level-critical" :
                                   log.Level == LogLevel.Error ? "level-error" :
                                   log.Level == LogLevel.Warning ? "level-warning" : "level-info";
                    
                    html.AppendLine("<tr>");
                    html.AppendLine($"<td>{log.FormattedTimestamp}</td>");
                    html.AppendLine($"<td><span class='level {levelClass}'>{log.Level}</span></td>");
                    html.AppendLine($"<td>{System.Web.HttpUtility.HtmlEncode(log.Source)}</td>");
                    html.AppendLine($"<td>{System.Web.HttpUtility.HtmlEncode(log.Message.Length > 100 ? log.Message.Substring(0, 100) + "..." : log.Message)}</td>");
                    html.AppendLine("</tr>");
                }

                html.AppendLine("</tbody></table>");
                html.AppendLine("</div>");
            }

            html.AppendLine("</body></html>");
            return html.ToString();
        }

        private string EscapeMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");
        }
    }
}
