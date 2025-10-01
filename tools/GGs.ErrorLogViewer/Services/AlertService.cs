#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using GGs.ErrorLogViewer.Models;

namespace GGs.ErrorLogViewer.Services
{
    public interface IAlertService
    {
        ObservableCollection<AlertRule> AlertRules { get; }
        ObservableCollection<LogAlert> ActiveAlerts { get; }
        event EventHandler<LogAlert>? AlertTriggered;
        
        void AddRule(AlertRule rule);
        void RemoveRule(string ruleId);
        void UpdateRule(AlertRule rule);
        void ProcessLogEntry(LogEntry entry);
        void AcknowledgeAlert(LogAlert alert);
        void ClearAcknowledgedAlerts();
    }

    public class AlertService : IAlertService
    {
        private readonly ObservableCollection<AlertRule> _alertRules;
        private readonly ObservableCollection<LogAlert> _activeAlerts;
        private readonly Dictionary<string, List<DateTime>> _triggerHistory;

        public ObservableCollection<AlertRule> AlertRules => _alertRules;
        public ObservableCollection<LogAlert> ActiveAlerts => _activeAlerts;
        public event EventHandler<LogAlert>? AlertTriggered;

        public AlertService()
        {
            _alertRules = new ObservableCollection<AlertRule>();
            _activeAlerts = new ObservableCollection<LogAlert>();
            _triggerHistory = new Dictionary<string, List<DateTime>>();

            InitializeDefaultRules();
        }

        private void InitializeDefaultRules()
        {
            // Critical Errors
            _alertRules.Add(new AlertRule
            {
                Name = "Critical Errors",
                Pattern = "",
                Severity = LogLevel.Critical,
                Threshold = 1,
                TimeWindow = TimeSpan.FromMinutes(1),
                IsEnabled = true
            });

            // Repeated Errors
            _alertRules.Add(new AlertRule
            {
                Name = "Repeated Errors (5+ in 5min)",
                Pattern = "",
                Severity = LogLevel.Error,
                Threshold = 5,
                TimeWindow = TimeSpan.FromMinutes(5),
                IsEnabled = true
            });

            // Exception Flood
            _alertRules.Add(new AlertRule
            {
                Name = "Exception Flood",
                Pattern = "exception",
                Severity = LogLevel.Error,
                Threshold = 10,
                TimeWindow = TimeSpan.FromMinutes(1),
                IsEnabled = true,
                UseRegex = false
            });

            // Out of Memory
            _alertRules.Add(new AlertRule
            {
                Name = "Out of Memory",
                Pattern = "(OutOfMemoryException|out of memory|memory.*exceeded)",
                Severity = LogLevel.Error,
                Threshold = 1,
                TimeWindow = TimeSpan.FromMinutes(1),
                IsEnabled = true,
                UseRegex = true
            });

            // Database Connection Issues
            _alertRules.Add(new AlertRule
            {
                Name = "Database Connection Failed",
                Pattern = "(connection.*failed|database.*error|sql.*exception)",
                Severity = LogLevel.Error,
                Threshold = 3,
                TimeWindow = TimeSpan.FromMinutes(5),
                IsEnabled = true,
                UseRegex = true
            });
        }

        public void AddRule(AlertRule rule)
        {
            _alertRules.Add(rule);
        }

        public void RemoveRule(string ruleId)
        {
            var rule = _alertRules.FirstOrDefault(r => r.Id == ruleId);
            if (rule != null)
            {
                _alertRules.Remove(rule);
                _triggerHistory.Remove(ruleId);
            }
        }

        public void UpdateRule(AlertRule rule)
        {
            var existing = _alertRules.FirstOrDefault(r => r.Id == rule.Id);
            if (existing != null)
            {
                var index = _alertRules.IndexOf(existing);
                _alertRules[index] = rule;
            }
        }

        public void ProcessLogEntry(LogEntry entry)
        {
            foreach (var rule in _alertRules.Where(r => r.IsEnabled))
            {
                if (ShouldTriggerAlert(rule, entry))
                {
                    TriggerAlert(rule, entry);
                }
            }
        }

        private bool ShouldTriggerAlert(AlertRule rule, LogEntry entry)
        {
            // Check severity match
            if (rule.Severity != LogLevel.All && entry.Level != rule.Severity)
                return false;

            // Check pattern match
            if (!string.IsNullOrEmpty(rule.Pattern))
            {
                try
                {
                    if (rule.UseRegex)
                    {
                        if (!Regex.IsMatch(entry.Message ?? string.Empty, rule.Pattern, RegexOptions.IgnoreCase))
                            return false;
                    }
                    else
                    {
                        if (!entry.Message?.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase) ?? true)
                            return false;
                    }
                }
                catch
                {
                    return false;
                }
            }

            // Check threshold within time window
            if (!_triggerHistory.ContainsKey(rule.Id))
            {
                _triggerHistory[rule.Id] = new List<DateTime>();
            }

            var history = _triggerHistory[rule.Id];
            var cutoff = DateTime.Now - rule.TimeWindow;
            
            // Remove old entries
            history.RemoveAll(t => t < cutoff);
            
            // Add current trigger
            history.Add(DateTime.Now);

            // Check if threshold exceeded
            return history.Count >= rule.Threshold;
        }

        private void TriggerAlert(AlertRule rule, LogEntry entry)
        {
            var alert = new LogAlert
            {
                Timestamp = DateTime.Now,
                AlertName = rule.Name,
                Message = $"{rule.Name}: {entry.Message}",
                Count = _triggerHistory[rule.Id].Count,
                Severity = entry.Level,
                IsAcknowledged = false
            };

            rule.LastTriggered = DateTime.Now;
            rule.TriggerCount++;

            _activeAlerts.Insert(0, alert);
            AlertTriggered?.Invoke(this, alert);

            // Keep only last 100 alerts
            while (_activeAlerts.Count > 100)
            {
                _activeAlerts.RemoveAt(_activeAlerts.Count - 1);
            }
        }

        public void AcknowledgeAlert(LogAlert alert)
        {
            alert.IsAcknowledged = true;
        }

        public void ClearAcknowledgedAlerts()
        {
            var acknowledged = _activeAlerts.Where(a => a.IsAcknowledged).ToList();
            foreach (var alert in acknowledged)
            {
                _activeAlerts.Remove(alert);
            }
        }
    }
}
