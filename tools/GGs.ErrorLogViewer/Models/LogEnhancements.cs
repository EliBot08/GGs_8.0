#nullable enable
using System;
using System.Collections.Generic;

namespace GGs.ErrorLogViewer.Models
{
    /// <summary>
    /// Bookmark for quick navigation to important log entries
    /// </summary>
    public class LogBookmark
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public long LogEntryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? Color { get; set; } // Hex color for visual distinction
    }

    /// <summary>
    /// Tag for categorizing log entries
    /// </summary>
    public class LogTag
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string? Color { get; set; } // Hex color
        public string? Icon { get; set; } // Icon identifier
    }

    /// <summary>
    /// Association between log entries and tags
    /// </summary>
    public class LogEntryTag
    {
        public long LogEntryId { get; set; }
        public string TagId { get; set; } = string.Empty;
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Smart alert configuration for pattern detection
    /// </summary>
    public class SmartAlert
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Pattern { get; set; } = string.Empty;
        public bool UseRegex { get; set; }
        public LogLevel MinimumLevel { get; set; } = LogLevel.Warning;
        public int ThresholdCount { get; set; } = 5;
        public TimeSpan ThresholdWindow { get; set; } = TimeSpan.FromMinutes(5);
        public bool IsEnabled { get; set; } = true;
        public DateTime? LastTriggered { get; set; }
        public int TriggerCount { get; set; }
        public AlertAction Action { get; set; } = AlertAction.Highlight;
    }

    public enum AlertAction
    {
        Highlight,
        Notify,
        HighlightAndNotify,
        LogToFile
    }

    /// <summary>
    /// Log comparison result for side-by-side view
    /// </summary>
    public class LogComparisonResult
    {
        public List<LogEntry> LeftOnly { get; set; } = new();
        public List<LogEntry> RightOnly { get; set; } = new();
        public List<(LogEntry Left, LogEntry Right)> Common { get; set; } = new();
        public List<(LogEntry Left, LogEntry Right)> Similar { get; set; } = new();
        public ComparisonStatistics Statistics { get; set; } = new();
    }

    public class ComparisonStatistics
    {
        public int TotalLeft { get; set; }
        public int TotalRight { get; set; }
        public int UniqueLeft { get; set; }
        public int UniqueRight { get; set; }
        public int Identical { get; set; }
        public int Similar { get; set; }
        public double SimilarityPercentage { get; set; }
    }

    /// <summary>
    /// Analytics data point for charting
    /// </summary>
    public class LogDataPoint
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string? Label { get; set; }
        public LogLevel? Level { get; set; }
    }

    /// <summary>
    /// Error clustering result for pattern analysis
    /// </summary>
    public class ErrorCluster
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Pattern { get; set; } = string.Empty;
        public int OccurrenceCount { get; set; }
        public List<LogEntry> Examples { get; set; } = new();
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public string? SuggestedRootCause { get; set; }
        public double Confidence { get; set; } // 0-1
    }

    /// <summary>
    /// Session state for crash recovery
    /// </summary>
    public class SessionState
    {
        public string? LogDirectory { get; set; }
        public long? SelectedLogEntryId { get; set; }
        public int ScrollPosition { get; set; }
        public string? SearchText { get; set; }
        public LogLevel? FilterLevel { get; set; }
        public string? FilterSource { get; set; }
        public List<string> BookmarkIds { get; set; } = new();
        public DateTime SavedAt { get; set; } = DateTime.UtcNow;
        public string? ActiveView { get; set; } // "Logs", "Analytics", "Compare", etc.
    }

    /// <summary>
    /// Log retention policy configuration
    /// </summary>
    public class RetentionPolicy
    {
        public bool IsEnabled { get; set; }
        public int RetentionDays { get; set; } = 30;
        public bool AutoClean { get; set; }
        public bool RequireConfirmation { get; set; } = true;
        public DateTime? LastCleanup { get; set; }
        public long MaxLogSizeMB { get; set; } = 1000;
        public bool CompressOldLogs { get; set; }
        public int CompressAfterDays { get; set; } = 7;
    }

    /// <summary>
    /// Log source configuration for external imports
    /// </summary>
    public class LogSourceConfig
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public LogSourceType Type { get; set; }
        public string? ConnectionString { get; set; }
        public string? FilePath { get; set; }
        public Dictionary<string, string> Properties { get; set; } = new();
        public bool IsEnabled { get; set; } = true;
        public DateTime? LastSync { get; set; }
    }

    public enum LogSourceType
    {
        LocalFile,
        WindowsEventViewer,
        Syslog,
        CloudWatch,
        AzureMonitor,
        Splunk,
        Elasticsearch
    }

    /// <summary>
    /// UI panel configuration for customizable layout
    /// </summary>
    public class PanelConfiguration
    {
        public string PanelId { get; set; } = string.Empty;
        public bool IsVisible { get; set; } = true;
        public double Width { get; set; } = double.NaN;
        public double Height { get; set; } = double.NaN;
        public PanelPosition Position { get; set; } = PanelPosition.Main;
        public bool IsDocked { get; set; } = true;
    }

    public enum PanelPosition
    {
        Main,
        Left,
        Right,
        Bottom,
        Floating
    }
}
