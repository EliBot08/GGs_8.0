using System;
using System.Collections.Generic;
using System.Linq;
using GGs.ErrorLogViewer.Models;

namespace GGs.ErrorLogViewer.Services
{
    public interface IAnalyticsService
    {
        LogStatistics GetStatistics(IEnumerable<LogEntry> logs);
        Dictionary<DateTime, int> GetLogTrend(IEnumerable<LogEntry> logs, TimeSpan interval);
        Dictionary<LogLevel, int> GetLogDistribution(IEnumerable<LogEntry> logs);
        Dictionary<string, int> GetTopErrors(IEnumerable<LogEntry> logs, int count = 10);
        Dictionary<string, int> GetTopSources(IEnumerable<LogEntry> logs, int count = 10);
        List<LogEntry> FindAnomalies(IEnumerable<LogEntry> logs);
    }

    public class AnalyticsService : IAnalyticsService
    {
        public LogStatistics GetStatistics(IEnumerable<LogEntry> logs)
        {
            var logList = logs.ToList();
            
            if (!logList.Any())
            {
                return new LogStatistics
                {
                    TotalLogs = 0,
                    OldestLog = DateTime.Now,
                    NewestLog = DateTime.Now,
                    TimeSpan = TimeSpan.Zero
                };
            }

            var oldest = logList.Min(l => l.Timestamp);
            var newest = logList.Max(l => l.Timestamp);

            return new LogStatistics
            {
                TotalLogs = logList.Count,
                ErrorCount = logList.Count(l => l.Level == LogLevel.Error),
                WarningCount = logList.Count(l => l.Level == LogLevel.Warning),
                InfoCount = logList.Count(l => l.Level == LogLevel.Information),
                CriticalCount = logList.Count(l => l.Level == LogLevel.Critical),
                SuccessCount = logList.Count(l => l.Level == LogLevel.Success),
                OldestLog = oldest,
                NewestLog = newest,
                TimeSpan = newest - oldest
            };
        }

        public Dictionary<DateTime, int> GetLogTrend(IEnumerable<LogEntry> logs, TimeSpan interval)
        {
            var logList = logs.ToList();
            if (!logList.Any()) return new Dictionary<DateTime, int>();

            var oldest = logList.Min(l => l.Timestamp);
            var newest = logList.Max(l => l.Timestamp);
            
            var trend = new Dictionary<DateTime, int>();
            var current = oldest;

            while (current <= newest)
            {
                var nextInterval = current.Add(interval);
                var count = logList.Count(l => l.Timestamp >= current && l.Timestamp < nextInterval);
                trend[current] = count;
                current = nextInterval;
            }

            return trend;
        }

        public Dictionary<LogLevel, int> GetLogDistribution(IEnumerable<LogEntry> logs)
        {
            var distribution = new Dictionary<LogLevel, int>();
            
            foreach (LogLevel level in Enum.GetValues(typeof(LogLevel)))
            {
                if (level != LogLevel.All)
                {
                    distribution[level] = logs.Count(l => l.Level == level);
                }
            }

            return distribution.OrderByDescending(kvp => kvp.Value).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public Dictionary<string, int> GetTopErrors(IEnumerable<LogEntry> logs, int count = 10)
        {
            return logs
                .Where(l => l.Level == LogLevel.Error || l.Level == LogLevel.Critical)
                .GroupBy(l => TruncateMessage(l.Message ?? string.Empty, 100))
                .OrderByDescending(g => g.Count())
                .Take(count)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        public Dictionary<string, int> GetTopSources(IEnumerable<LogEntry> logs, int count = 10)
        {
            return logs
                .GroupBy(l => l.Source ?? "Unknown")
                .OrderByDescending(g => g.Count())
                .Take(count)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        public List<LogEntry> FindAnomalies(IEnumerable<LogEntry> logs)
        {
            var logList = logs.ToList();
            var anomalies = new List<LogEntry>();

            // Find burst errors (more than 5 errors in 1 minute)
            var errorBursts = logList
                .Where(l => l.Level == LogLevel.Error || l.Level == LogLevel.Critical)
                .GroupBy(l => new { l.Timestamp.Year, l.Timestamp.Month, l.Timestamp.Day, l.Timestamp.Hour, l.Timestamp.Minute })
                .Where(g => g.Count() > 5)
                .SelectMany(g => g);

            anomalies.AddRange(errorBursts);

            // Find duplicate critical errors
            var duplicateCritical = logList
                .Where(l => l.Level == LogLevel.Critical)
                .GroupBy(l => l.Message)
                .Where(g => g.Count() > 1)
                .SelectMany(g => g.Skip(1)); // Skip first occurrence

            anomalies.AddRange(duplicateCritical);

            return anomalies.Distinct().ToList();
        }

        private string TruncateMessage(string message, int maxLength)
        {
            if (string.IsNullOrEmpty(message)) return string.Empty;
            
            if (message.Length <= maxLength)
                return message;

            return message.Substring(0, maxLength) + "...";
        }
    }
}
