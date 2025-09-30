using System;

namespace GGs.ErrorLogViewer.Models
{
    public class PerformanceMetrics
    {
        public DateTime Timestamp { get; set; }
        public double CpuUsage { get; set; }
        public double MemoryUsageMB { get; set; }
        public double DiskReadKBps { get; set; }
        public double DiskWriteKBps { get; set; }
        public int LogsPerSecond { get; set; }
        public int TotalLogsProcessed { get; set; }
        public TimeSpan Uptime { get; set; }
    }

    public class LogStatistics
    {
        public int TotalLogs { get; set; }
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public int InfoCount { get; set; }
        public int CriticalCount { get; set; }
        public int SuccessCount { get; set; }
        public DateTime OldestLog { get; set; }
        public DateTime NewestLog { get; set; }
        public TimeSpan TimeSpan { get; set; }
        
        public double ErrorRate => TotalLogs > 0 ? (ErrorCount * 100.0 / TotalLogs) : 0;
        public double WarningRate => TotalLogs > 0 ? (WarningCount * 100.0 / TotalLogs) : 0;
        public double HealthScore => CalculateHealthScore();

        private double CalculateHealthScore()
        {
            if (TotalLogs == 0) return 100;
            
            double score = 100;
            score -= (CriticalCount * 10);
            score -= (ErrorCount * 2);
            score -= (WarningCount * 0.5);
            
            return Math.Max(0, Math.Min(100, score));
        }
    }

    public class AlertRule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Pattern { get; set; } = string.Empty;
        public LogLevel Severity { get; set; }
        public int Threshold { get; set; } = 1;
        public TimeSpan TimeWindow { get; set; } = TimeSpan.FromMinutes(5);
        public bool IsEnabled { get; set; } = true;
        public bool UseRegex { get; set; } = false;
        public DateTime? LastTriggered { get; set; }
        public int TriggerCount { get; set; }
    }

    public class LogAlert
    {
        public DateTime Timestamp { get; set; }
        public string AlertName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int Count { get; set; }
        public LogLevel Severity { get; set; }
        public bool IsAcknowledged { get; set; }
    }
}
