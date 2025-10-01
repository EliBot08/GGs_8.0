#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace GGs.ErrorLogViewer.Models
{
    public class LogEntry : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isHighlighted;
        private bool _isBookmarked;
        private bool _isExpanded;

        public long Id { get; set; }
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Source { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string RawLine { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long LineNumber { get; set; }
        public string? Exception { get; set; }
        public string? StackTrace { get; set; }
        public string? Category { get; set; }
        public string? ThreadId { get; set; }
        public string? ProcessId { get; set; }
        public string? MachineName { get; set; }
        public string? UserName { get; set; }

        [JsonIgnore]
        public string LevelIcon => Level switch
        {
            LogLevel.Error => "❌",
            LogLevel.Warning => "⚠️",
            LogLevel.Information => "ℹ️",
            LogLevel.Debug => "🐛",
            LogLevel.Trace => "🔍",
            LogLevel.Critical => "💥",
            LogLevel.Success => "✅",
            _ => "📝"
        };

        [JsonIgnore]
        public string LevelColor => Level switch
        {
            LogLevel.Error => "#FF6B6B",
            LogLevel.Warning => "#FFB347",
            LogLevel.Information => "#4ECDC4",
            LogLevel.Debug => "#95A5A6",
            LogLevel.Trace => "#BDC3C7",
            LogLevel.Critical => "#E74C3C",
            LogLevel.Success => "#2ECC71",
            _ => "#34495E"
        };

        [JsonIgnore]
        public string FormattedTimestamp => Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");

        [JsonIgnore]
        public string CompactMessage
        {
            get
            {
                if (string.IsNullOrEmpty(Message))
                    return string.Empty;

                // Simplify technical messages for user-friendly view
                var simplified = Message;

                // Remove common technical prefixes
                if (simplified.StartsWith("System."))
                    simplified = simplified.Substring(7);
                
                if (simplified.StartsWith("Microsoft."))
                    simplified = simplified.Substring(10);

                // Truncate very long messages
                if (simplified.Length > 200)
                    simplified = simplified.Substring(0, 197) + "...";

                return simplified;
            }
        }

        [JsonIgnore]
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        [JsonIgnore]
        public bool IsHighlighted
        {
            get => _isHighlighted;
            set
            {
                if (_isHighlighted != value)
                {
                    _isHighlighted = value;
                    OnPropertyChanged(nameof(IsHighlighted));
                }
            }
        }

        [JsonIgnore]
        public bool IsBookmarked
        {
            get => _isBookmarked;
            set
            {
                if (_isBookmarked != value)
                {
                    _isBookmarked = value;
                    OnPropertyChanged(nameof(IsBookmarked));
                }
            }
        }

        [JsonIgnore]
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged(nameof(IsExpanded));
                }
            }
        }

        [JsonIgnore]
        public List<LogTag> Tags { get; set; } = new();

        [JsonIgnore]
        public bool HasMultipleLines => !string.IsNullOrEmpty(StackTrace) || Message.Contains('\n');

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString()
        {
            return $"{FormattedTimestamp} [{Level}] {Source}: {Message}";
        }
    }

    public enum LogLevel
    {
        All = -1,
        Trace = 0,
        Debug = 1,
        Information = 2,
        Warning = 3,
        Error = 4,
        Critical = 5,
        Success = 6
    }

    public class LogFilter
    {
        public bool ShowTrace { get; set; } = false;
        public bool ShowDebug { get; set; } = false;
        public bool ShowInformation { get; set; } = true;
        public bool ShowWarning { get; set; } = true;
        public bool ShowError { get; set; } = true;
        public bool ShowCritical { get; set; } = true;
        public bool ShowSuccess { get; set; } = true;
        
        public string SearchText { get; set; } = string.Empty;
        public string SourceFilter { get; set; } = string.Empty;
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        
        public bool IsLevelVisible(LogLevel level)
        {
            return level switch
            {
                LogLevel.Trace => ShowTrace,
                LogLevel.Debug => ShowDebug,
                LogLevel.Information => ShowInformation,
                LogLevel.Warning => ShowWarning,
                LogLevel.Error => ShowError,
                LogLevel.Critical => ShowCritical,
                LogLevel.Success => ShowSuccess,
                _ => true
            };
        }
    }
}