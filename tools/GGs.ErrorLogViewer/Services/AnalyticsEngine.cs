#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GGs.ErrorLogViewer.Models;
using Microsoft.Extensions.Logging;

namespace GGs.ErrorLogViewer.Services
{
    public interface IAnalyticsEngine
    {
        // Statistics
        LogStatistics GetStatistics(IEnumerable<LogEntry> logs);
        Dictionary<DateTime, int> GetLogTrend(IEnumerable<LogEntry> logs, TimeSpan interval);
        Dictionary<Models.LogLevel, int> GetLogDistribution(IEnumerable<LogEntry> logs);
        Dictionary<string, int> GetTopErrors(IEnumerable<LogEntry> logs, int count = 10);
        Dictionary<string, int> GetTopSources(IEnumerable<LogEntry> logs, int count = 10);
        
        // Advanced analytics
        List<LogDataPoint> GetTimeSeriesData(IEnumerable<LogEntry> logs, TimeSpan bucketSize);
        List<ErrorCluster> AnalyzeErrorPatterns(IEnumerable<LogEntry> logs, int minOccurrences = 3);
        Dictionary<int, int> GetHourlyHeatmap(IEnumerable<LogEntry> logs);
        List<(string Pattern, double Frequency)> GetCommonPatterns(IEnumerable<LogEntry> logs, int topN = 10);
        
        // Anomaly detection
        List<LogEntry> FindAnomalies(IEnumerable<LogEntry> logs);
        double CalculateAnomalyScore(LogEntry entry, IEnumerable<LogEntry> context);
    }

    public class AnalyticsEngine : IAnalyticsEngine
    {
        private readonly ILogger<AnalyticsEngine> _logger;

        public AnalyticsEngine(ILogger<AnalyticsEngine> logger)
        {
            _logger = logger;
        }

        public LogStatistics GetStatistics(IEnumerable<LogEntry> logs)
        {
            var logList = logs.ToList();
            
            if (!logList.Any())
            {
                return new LogStatistics();
            }

            var stats = new LogStatistics
            {
                TotalLogs = logList.Count,
                ErrorCount = logList.Count(l => l.Level == Models.LogLevel.Error),
                WarningCount = logList.Count(l => l.Level == Models.LogLevel.Warning),
                InfoCount = logList.Count(l => l.Level == Models.LogLevel.Information),
                CriticalCount = logList.Count(l => l.Level == Models.LogLevel.Critical),
                SuccessCount = logList.Count(l => l.Level == Models.LogLevel.Success),
                OldestLog = logList.Min(l => l.Timestamp),
                NewestLog = logList.Max(l => l.Timestamp)
            };

            stats.TimeSpan = stats.NewestLog - stats.OldestLog;

            return stats;
        }

        public Dictionary<DateTime, int> GetLogTrend(IEnumerable<LogEntry> logs, TimeSpan interval)
        {
            var logList = logs.ToList();
            if (!logList.Any()) return new Dictionary<DateTime, int>();

            var minTime = logList.Min(l => l.Timestamp);
            var maxTime = logList.Max(l => l.Timestamp);
            
            var result = new Dictionary<DateTime, int>();
            var currentTime = new DateTime(minTime.Year, minTime.Month, minTime.Day, minTime.Hour, 0, 0);

            while (currentTime <= maxTime)
            {
                var nextTime = currentTime + interval;
                var count = logList.Count(l => l.Timestamp >= currentTime && l.Timestamp < nextTime);
                result[currentTime] = count;
                currentTime = nextTime;
            }

            return result;
        }

        public Dictionary<Models.LogLevel, int> GetLogDistribution(IEnumerable<LogEntry> logs)
        {
            return logs
                .GroupBy(l => l.Level)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        public Dictionary<string, int> GetTopErrors(IEnumerable<LogEntry> logs, int count = 10)
        {
            return logs
                .Where(l => l.Level == Models.LogLevel.Error || l.Level == Models.LogLevel.Critical)
                .GroupBy(l => SimplifyErrorMessage(l.Message))
                .OrderByDescending(g => g.Count())
                .Take(count)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        public Dictionary<string, int> GetTopSources(IEnumerable<LogEntry> logs, int count = 10)
        {
            return logs
                .Where(l => !string.IsNullOrEmpty(l.Source))
                .GroupBy(l => l.Source)
                .OrderByDescending(g => g.Count())
                .Take(count)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        public List<LogDataPoint> GetTimeSeriesData(IEnumerable<LogEntry> logs, TimeSpan bucketSize)
        {
            var logList = logs.ToList();
            if (!logList.Any()) return new List<LogDataPoint>();

            var grouped = logList
                .GroupBy(l => new DateTime(
                    (l.Timestamp.Ticks / bucketSize.Ticks) * bucketSize.Ticks))
                .OrderBy(g => g.Key);

            return grouped.Select(g => new LogDataPoint
            {
                Timestamp = g.Key,
                Value = g.Count(),
                Label = g.Key.ToString("HH:mm")
            }).ToList();
        }

        public List<ErrorCluster> AnalyzeErrorPatterns(IEnumerable<LogEntry> logs, int minOccurrences = 3)
        {
            var errors = logs
                .Where(l => l.Level == Models.LogLevel.Error || l.Level == Models.LogLevel.Critical)
                .ToList();

            if (!errors.Any()) return new List<ErrorCluster>();

            // Group by simplified message pattern
            var clusters = errors
                .GroupBy(e => ExtractErrorPattern(e.Message))
                .Where(g => g.Count() >= minOccurrences)
                .Select(g => new ErrorCluster
                {
                    Pattern = g.Key,
                    OccurrenceCount = g.Count(),
                    Examples = g.Take(5).ToList(),
                    FirstSeen = g.Min(e => e.Timestamp),
                    LastSeen = g.Max(e => e.Timestamp),
                    SuggestedRootCause = SuggestRootCause(g.Key, g.ToList()),
                    Confidence = CalculateConfidence(g.ToList())
                })
                .OrderByDescending(c => c.OccurrenceCount)
                .ToList();

            _logger.LogInformation("Identified {ClusterCount} error clusters from {ErrorCount} errors",
                clusters.Count, errors.Count);

            return clusters;
        }

        public Dictionary<int, int> GetHourlyHeatmap(IEnumerable<LogEntry> logs)
        {
            var heatmap = new Dictionary<int, int>();
            for (int hour = 0; hour < 24; hour++)
            {
                heatmap[hour] = 0;
            }

            foreach (var log in logs)
            {
                heatmap[log.Timestamp.Hour]++;
            }

            return heatmap;
        }

        public List<(string Pattern, double Frequency)> GetCommonPatterns(IEnumerable<LogEntry> logs, int topN = 10)
        {
            var patterns = logs
                .Select(l => ExtractKeywords(l.Message))
                .SelectMany(keywords => keywords)
                .GroupBy(k => k)
                .OrderByDescending(g => g.Count())
                .Take(topN)
                .Select(g => (g.Key, (double)g.Count() / logs.Count()))
                .ToList();

            return patterns;
        }

        public List<LogEntry> FindAnomalies(IEnumerable<LogEntry> logs)
        {
            var logList = logs.ToList();
            if (logList.Count < 10) return new List<LogEntry>();

            var anomalies = new List<LogEntry>();

            foreach (var entry in logList)
            {
                var score = CalculateAnomalyScore(entry, logList);
                if (score > 0.7) // Threshold for anomaly
                {
                    anomalies.Add(entry);
                }
            }

            _logger.LogInformation("Detected {AnomalyCount} anomalies from {TotalCount} logs",
                anomalies.Count, logList.Count);

            return anomalies;
        }

        public double CalculateAnomalyScore(LogEntry entry, IEnumerable<LogEntry> context)
        {
            var contextList = context.ToList();
            double score = 0;

            // Check if level is unusual
            var levelFrequency = contextList.Count(l => l.Level == entry.Level) / (double)contextList.Count;
            if (levelFrequency < 0.05) score += 0.3;

            // Check if source is unusual
            var sourceFrequency = contextList.Count(l => l.Source == entry.Source) / (double)contextList.Count;
            if (sourceFrequency < 0.02) score += 0.2;

            // Check if message pattern is unique
            var pattern = ExtractErrorPattern(entry.Message);
            var patternFrequency = contextList.Count(l => ExtractErrorPattern(l.Message) == pattern) / (double)contextList.Count;
            if (patternFrequency < 0.01) score += 0.5;

            return Math.Min(score, 1.0);
        }

        // Helper methods

        private string SimplifyErrorMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return string.Empty;

            // Remove timestamps, IDs, paths
            message = Regex.Replace(message, @"\d{4}-\d{2}-\d{2}", "[DATE]");
            message = Regex.Replace(message, @"\d{2}:\d{2}:\d{2}", "[TIME]");
            message = Regex.Replace(message, @"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b", "[GUID]");
            message = Regex.Replace(message, @"\b\d+\b", "[NUM]");
            message = Regex.Replace(message, @"[A-Z]:\\[\w\\]+", "[PATH]");
            
            // Take first 100 characters
            if (message.Length > 100)
                message = message.Substring(0, 100);

            return message.Trim();
        }

        private string ExtractErrorPattern(string message)
        {
            if (string.IsNullOrEmpty(message)) return string.Empty;

            // Extract exception type if present
            var exceptionMatch = Regex.Match(message, @"(\w+Exception)");
            if (exceptionMatch.Success)
            {
                return exceptionMatch.Groups[1].Value;
            }

            // Extract first meaningful words
            var words = message.Split(new[] { ' ', '.', ':', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3 && !int.TryParse(w, out _))
                .Take(3);

            return string.Join(" ", words);
        }

        private List<string> ExtractKeywords(string message)
        {
            if (string.IsNullOrEmpty(message)) return new List<string>();

            // Extract meaningful words (3+ characters, not numbers)
            var words = Regex.Matches(message, @"\b[a-zA-Z]{3,}\b")
                .Cast<Match>()
                .Select(m => m.Value.ToLowerInvariant())
                .Where(w => !IsCommonWord(w))
                .Distinct()
                .ToList();

            return words;
        }

        private bool IsCommonWord(string word)
        {
            var commonWords = new HashSet<string> { "the", "and", "for", "with", "from", "this", "that", "was", "has", "been" };
            return commonWords.Contains(word);
        }

        private string? SuggestRootCause(string pattern, List<LogEntry> entries)
        {
            // Simple heuristic-based root cause suggestions
            if (pattern.Contains("null", StringComparison.OrdinalIgnoreCase))
                return "Possible null reference - check object initialization";

            if (pattern.Contains("connection", StringComparison.OrdinalIgnoreCase))
                return "Network or database connectivity issue - verify connection strings and network";

            if (pattern.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                return "Operation timeout - consider increasing timeout values or optimizing queries";

            if (pattern.Contains("access", StringComparison.OrdinalIgnoreCase) || 
                pattern.Contains("permission", StringComparison.OrdinalIgnoreCase))
                return "Permission issue - verify file/database/API access rights";

            if (pattern.Contains("memory", StringComparison.OrdinalIgnoreCase))
                return "Memory issue - check for memory leaks or increase available memory";

            if (entries.Count > 100)
                return "High frequency error - may indicate a systematic issue requiring immediate attention";

            return "Review error context and related logs for more information";
        }

        private double CalculateConfidence(List<LogEntry> clusterEntries)
        {
            // Confidence based on consistency of pattern
            if (clusterEntries.Count < 3) return 0.3;
            if (clusterEntries.Count < 10) return 0.5;
            if (clusterEntries.Count < 50) return 0.7;
            return 0.9;
        }
    }
}
