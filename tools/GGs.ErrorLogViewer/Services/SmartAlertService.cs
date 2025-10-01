#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GGs.ErrorLogViewer.Models;
using Microsoft.Extensions.Logging;

namespace GGs.ErrorLogViewer.Services
{
    public interface ISmartAlertService
    {
        ObservableCollection<SmartAlert> Alerts { get; }
        ObservableCollection<LogAlert> TriggeredAlerts { get; }
        
        event EventHandler<LogAlert>? AlertTriggered;
        
        SmartAlert AddAlert(string name, string pattern, bool useRegex = false, 
            Models.LogLevel minimumLevel = Models.LogLevel.Warning, int threshold = 5, TimeSpan? window = null);
        void RemoveAlert(string alertId);
        void UpdateAlert(SmartAlert alert);
        void EnableAlert(string alertId);
        void DisableAlert(string alertId);
        void ProcessLogEntry(LogEntry entry);
        void ClearTriggeredAlerts();
        void AcknowledgeAlert(string alertId);
    }

    public class SmartAlertService : ISmartAlertService
    {
        private readonly ILogger<SmartAlertService> _logger;
        private readonly Dictionary<string, List<(DateTime Timestamp, LogEntry Entry)>> _alertHistory = new();
        private readonly Dictionary<string, DateTime> _lastNotificationTime = new();
        private readonly TimeSpan _minNotificationInterval = TimeSpan.FromMinutes(1);

        public ObservableCollection<SmartAlert> Alerts { get; } = new();
        public ObservableCollection<LogAlert> TriggeredAlerts { get; } = new();

        public event EventHandler<LogAlert>? AlertTriggered;

        public SmartAlertService(ILogger<SmartAlertService> logger)
        {
            _logger = logger;
            InitializeDefaultAlerts();
        }

        private void InitializeDefaultAlerts()
        {
            // Create useful default alerts
            AddAlert("Repeated Exceptions", "exception|error", useRegex: true, 
                minimumLevel: Models.LogLevel.Error, threshold: 5, window: TimeSpan.FromMinutes(5));
            
            AddAlert("Critical System Errors", "critical|fatal|crash", useRegex: true,
                minimumLevel: Models.LogLevel.Critical, threshold: 1, window: TimeSpan.FromMinutes(1));
            
            AddAlert("Authentication Failures", "authentication|login.*failed|unauthorized", useRegex: true,
                minimumLevel: Models.LogLevel.Warning, threshold: 10, window: TimeSpan.FromMinutes(5));
            
            AddAlert("Database Connection Issues", "database.*connection|sql.*timeout|deadlock", useRegex: true,
                minimumLevel: Models.LogLevel.Error, threshold: 3, window: TimeSpan.FromMinutes(2));
        }

        public SmartAlert AddAlert(string name, string pattern, bool useRegex = false,
            Models.LogLevel minimumLevel = Models.LogLevel.Warning, int threshold = 5, TimeSpan? window = null)
        {
            var alert = new SmartAlert
            {
                Name = name,
                Pattern = pattern,
                UseRegex = useRegex,
                MinimumLevel = minimumLevel,
                ThresholdCount = threshold,
                ThresholdWindow = window ?? TimeSpan.FromMinutes(5),
                IsEnabled = true
            };

            Alerts.Add(alert);
            _alertHistory[alert.Id] = new List<(DateTime, LogEntry)>();
            
            _logger.LogInformation("Added smart alert '{Name}' with pattern '{Pattern}'", name, pattern);
            return alert;
        }

        public void RemoveAlert(string alertId)
        {
            var alert = Alerts.FirstOrDefault(a => a.Id == alertId);
            if (alert != null)
            {
                Alerts.Remove(alert);
                _alertHistory.Remove(alertId);
                _lastNotificationTime.Remove(alertId);
                _logger.LogInformation("Removed alert '{Name}'", alert.Name);
            }
        }

        public void UpdateAlert(SmartAlert alert)
        {
            var existing = Alerts.FirstOrDefault(a => a.Id == alert.Id);
            if (existing != null)
            {
                var index = Alerts.IndexOf(existing);
                Alerts[index] = alert;
                _logger.LogDebug("Updated alert '{Name}'", alert.Name);
            }
        }

        public void EnableAlert(string alertId)
        {
            var alert = Alerts.FirstOrDefault(a => a.Id == alertId);
            if (alert != null)
            {
                alert.IsEnabled = true;
                _logger.LogInformation("Enabled alert '{Name}'", alert.Name);
            }
        }

        public void DisableAlert(string alertId)
        {
            var alert = Alerts.FirstOrDefault(a => a.Id == alertId);
            if (alert != null)
            {
                alert.IsEnabled = false;
                _logger.LogInformation("Disabled alert '{Name}'", alert.Name);
            }
        }

        public void ProcessLogEntry(LogEntry entry)
        {
            foreach (var alert in Alerts.Where(a => a.IsEnabled))
            {
                // Check if entry meets minimum level
                if (entry.Level < alert.MinimumLevel)
                    continue;

                // Check if pattern matches
                bool matches = alert.UseRegex
                    ? Regex.IsMatch(entry.Message, alert.Pattern, RegexOptions.IgnoreCase)
                    : entry.Message.Contains(alert.Pattern, StringComparison.OrdinalIgnoreCase);

                if (!matches)
                    continue;

                // Add to history
                if (!_alertHistory.ContainsKey(alert.Id))
                {
                    _alertHistory[alert.Id] = new List<(DateTime, LogEntry)>();
                }

                _alertHistory[alert.Id].Add((DateTime.UtcNow, entry));

                // Clean old history outside the time window
                var cutoff = DateTime.UtcNow - alert.ThresholdWindow;
                _alertHistory[alert.Id].RemoveAll(h => h.Timestamp < cutoff);

                // Check if threshold is met
                if (_alertHistory[alert.Id].Count >= alert.ThresholdCount)
                {
                    TriggerAlert(alert, _alertHistory[alert.Id].Select(h => h.Entry).ToList());
                }
            }
        }

        private void TriggerAlert(SmartAlert alert, List<LogEntry> matchingEntries)
        {
            // Check if we should throttle notifications
            if (_lastNotificationTime.ContainsKey(alert.Id))
            {
                var timeSinceLastNotification = DateTime.UtcNow - _lastNotificationTime[alert.Id];
                if (timeSinceLastNotification < _minNotificationInterval)
                {
                    _logger.LogDebug("Alert '{Name}' throttled - last notification was {Time} ago",
                        alert.Name, timeSinceLastNotification);
                    return;
                }
            }

            var logAlert = new LogAlert
            {
                Timestamp = DateTime.UtcNow,
                AlertName = alert.Name,
                Message = $"Alert '{alert.Name}' triggered: {matchingEntries.Count} matching entries in {alert.ThresholdWindow.TotalMinutes:F1} minutes",
                Count = matchingEntries.Count,
                Severity = alert.MinimumLevel,
                IsAcknowledged = false
            };

            TriggeredAlerts.Insert(0, logAlert); // Add to beginning for newest first

            // Update alert statistics
            alert.LastTriggered = DateTime.UtcNow;
            alert.TriggerCount++;

            // Store notification time
            _lastNotificationTime[alert.Id] = DateTime.UtcNow;

            // Trigger event
            AlertTriggered?.Invoke(this, logAlert);

            _logger.LogWarning("Alert triggered: {AlertName} - {Count} occurrences", alert.Name, matchingEntries.Count);

            // Perform alert action
            PerformAlertAction(alert, logAlert, matchingEntries);
        }

        private void PerformAlertAction(SmartAlert alert, LogAlert logAlert, List<LogEntry> matchingEntries)
        {
            switch (alert.Action)
            {
                case AlertAction.Highlight:
                    // Highlight matching entries
                    foreach (var entry in matchingEntries)
                    {
                        entry.IsHighlighted = true;
                    }
                    break;

                case AlertAction.Notify:
                    // Notification is handled by the event
                    break;

                case AlertAction.HighlightAndNotify:
                    foreach (var entry in matchingEntries)
                    {
                        entry.IsHighlighted = true;
                    }
                    break;

                case AlertAction.LogToFile:
                    LogAlertToFile(logAlert, matchingEntries);
                    break;
            }
        }

        private void LogAlertToFile(LogAlert alert, List<LogEntry> matchingEntries)
        {
            try
            {
                var logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "GGs", "ErrorLogViewer", "Alerts");
                
                Directory.CreateDirectory(logDir);
                
                var fileName = $"alert_{DateTime.Now:yyyyMMdd_HHmmss}.log";
                var filePath = Path.Combine(logDir, fileName);

                var lines = new List<string>
                {
                    $"Alert: {alert.AlertName}",
                    $"Timestamp: {alert.Timestamp:yyyy-MM-dd HH:mm:ss}",
                    $"Message: {alert.Message}",
                    $"Count: {alert.Count}",
                    $"Severity: {alert.Severity}",
                    "",
                    "Matching Log Entries:",
                    "-----------------------------------"
                };

                foreach (var entry in matchingEntries)
                {
                    lines.Add($"[{entry.FormattedTimestamp}] [{entry.Level}] {entry.Source}: {entry.Message}");
                }

                System.IO.File.WriteAllLines(filePath, lines);
                _logger.LogInformation("Alert logged to file: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log alert to file");
            }
        }

        public void ClearTriggeredAlerts()
        {
            TriggeredAlerts.Clear();
            _logger.LogInformation("Cleared all triggered alerts");
        }

        public void AcknowledgeAlert(string alertId)
        {
            var alert = TriggeredAlerts.FirstOrDefault(a => a.AlertName == alertId);
            if (alert != null)
            {
                alert.IsAcknowledged = true;
                _logger.LogDebug("Acknowledged alert '{AlertName}'", alertId);
            }
        }
    }
}
