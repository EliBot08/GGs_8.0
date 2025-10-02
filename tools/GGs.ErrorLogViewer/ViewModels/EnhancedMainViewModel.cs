#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GGs.ErrorLogViewer.Models;
using GGs.ErrorLogViewer.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace GGs.ErrorLogViewer.ViewModels
{
    /// <summary>
    /// Enhanced view model extending MainViewModel with analytics, bookmarks, alerts, and advanced exports.
    /// </summary>
    public partial class EnhancedMainViewModel : MainViewModel
    {
        private readonly IBookmarkService _bookmarkService;
        private readonly ISmartAlertService _alertService;
        private readonly IAnalyticsEngine _analyticsEngine;
        private readonly ISessionStateService _sessionStateService;
        private readonly IEnhancedExportService _enhancedExportService;
        private readonly IExternalLogSourceService _externalLogSourceService;
        private readonly ILogger<EnhancedMainViewModel> _logger;
        private readonly ObservableCollection<KeyValuePair<string, int>> _logLevelDistribution = new();
        private readonly ObservableCollection<KeyValuePair<string, int>> _topSources = new();
        private readonly AsyncRelayCommand _refreshAnalyticsCommand;
        private readonly AsyncRelayCommand _analyzeErrorPatternsCommand;
        private readonly AsyncRelayCommand _findAnomaliesCommand;
        private readonly AsyncRelayCommand _exportAnalyticsCommand;
        private readonly AsyncRelayCommand _exportPdfCommand;
        private readonly AsyncRelayCommand _exportLast24HoursCommand;
        private readonly AsyncRelayCommand _exportMarkdownCommand;
        private readonly AsyncRelayCommand _importWindowsEventLogCommand;
        private readonly AsyncRelayCommand _importSyslogCommand;
        private readonly AsyncRelayCommand _importCustomFormatCommand;
        private bool _isRestoringSession;

        public ObservableCollection<LogBookmark> Bookmarks => _bookmarkService.Bookmarks;
        public ObservableCollection<LogTag> AvailableTags => _bookmarkService.Tags;
        public ObservableCollection<SmartAlert> SmartAlerts => _alertService.Alerts;
        public ObservableCollection<LogAlert> TriggeredAlerts => _alertService.TriggeredAlerts;
        public ObservableCollection<KeyValuePair<string, int>> LogLevelDistribution => _logLevelDistribution;
        public ObservableCollection<KeyValuePair<string, int>> TopSources => _topSources;

        [ObservableProperty]
        private LogStatistics? _currentStatistics;

        [ObservableProperty]
        private ObservableCollection<ErrorCluster> _errorClusters = new();

        [ObservableProperty]
        private ObservableCollection<LogDataPoint> _timeSeriesData = new();

        [ObservableProperty]
        private string _activeView = "Logs";

        [ObservableProperty]
        private bool _showAnalyticsDashboard;

        [ObservableProperty]
        private bool _showBookmarksPanel;

        [ObservableProperty]
        private bool _showAlertsPanel = true;

        public ICommand AddBookmarkCommand { get; }
        public ICommand RemoveBookmarkCommand { get; }
        public ICommand GoToBookmarkCommand { get; }
        public ICommand AddTagCommand { get; }
        public ICommand AssignTagCommand { get; }
        public ICommand RemoveTagCommand { get; }
        public ICommand FilterByTagCommand { get; }
        public ICommand CreateAlertCommand { get; }
        public ICommand EnableAlertCommand { get; }
        public ICommand DisableAlertCommand { get; }
        public ICommand AcknowledgeAlertCommand { get; }
        public ICommand ClearAlertsCommand { get; }
        public ICommand RefreshAnalyticsCommand => _refreshAnalyticsCommand;
        public ICommand AnalyzeErrorPatternsCommand => _analyzeErrorPatternsCommand;
        public ICommand FindAnomaliesCommand => _findAnomaliesCommand;
        public ICommand ExportAnalyticsCommand => _exportAnalyticsCommand;
        public ICommand ExportToPdfCommand => _exportPdfCommand;
        public ICommand ExportLast24HoursCommand => _exportLast24HoursCommand;
        public ICommand ExportToMarkdownCommand => _exportMarkdownCommand;
        public ICommand ImportWindowsEventLogCommand => _importWindowsEventLogCommand;
        public ICommand ImportSyslogCommand => _importSyslogCommand;
        public ICommand ImportCustomFormatCommand => _importCustomFormatCommand;
        
        public new ICommand SwitchToLogsViewCommand { get; }
        public new ICommand SwitchToAnalyticsViewCommand { get; }
        public new ICommand SwitchToBookmarksViewCommand { get; }
        public new ICommand SwitchToAlertsViewCommand { get; }
        public ICommand SwitchToCompareViewCommand { get; }
        public ICommand SwitchToExportViewCommand { get; }
        public ICommand SwitchToSettingsViewCommand { get; }

        public EnhancedMainViewModel(
            ILogMonitoringService logMonitoringService,
            ILogParsingService logParsingService,
            IThemeService themeService,
            IExportService exportService,
            IEarlyLoggingService earlyLoggingService,
            IConfiguration configuration,
            ILogger<MainViewModel> baseLogger,
            IBookmarkService bookmarkService,
            ISmartAlertService alertService,
            IAnalyticsEngine analyticsEngine,
            ISessionStateService sessionStateService,
            IEnhancedExportService enhancedExportService,
            IExternalLogSourceService externalLogSourceService,
            ILogger<EnhancedMainViewModel> logger)
            : base(logMonitoringService, logParsingService, themeService, exportService, earlyLoggingService, configuration, baseLogger)
        {
            _bookmarkService = bookmarkService ?? throw new ArgumentNullException(nameof(bookmarkService));
            _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));
            _analyticsEngine = analyticsEngine ?? throw new ArgumentNullException(nameof(analyticsEngine));
            _sessionStateService = sessionStateService ?? throw new ArgumentNullException(nameof(sessionStateService));
            _enhancedExportService = enhancedExportService ?? throw new ArgumentNullException(nameof(enhancedExportService));
            _externalLogSourceService = externalLogSourceService ?? throw new ArgumentNullException(nameof(externalLogSourceService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            AddBookmarkCommand = new RelayCommand(AddBookmark, () => SelectedLogEntry != null);
            RemoveBookmarkCommand = new RelayCommand<LogBookmark>(RemoveBookmark);
            GoToBookmarkCommand = new RelayCommand<LogBookmark>(GoToBookmark);
            AddTagCommand = new RelayCommand<string>(AddTag);
            AssignTagCommand = new RelayCommand<LogTag>(AssignTag, _ => SelectedLogEntry != null);
            RemoveTagCommand = new RelayCommand<LogTag>(RemoveTag);
            FilterByTagCommand = new RelayCommand<LogTag>(FilterByTag);
            CreateAlertCommand = new RelayCommand(CreateAlert);
            EnableAlertCommand = new RelayCommand<SmartAlert>(EnableAlert);
            DisableAlertCommand = new RelayCommand<SmartAlert>(DisableAlert);
            AcknowledgeAlertCommand = new RelayCommand<LogAlert>(AcknowledgeAlert);
            ClearAlertsCommand = new RelayCommand(_alertService.ClearTriggeredAlerts);

            _refreshAnalyticsCommand = new AsyncRelayCommand(RefreshAnalyticsAsync, CanRunAnalyticsCommands);
            _analyzeErrorPatternsCommand = new AsyncRelayCommand(AnalyzeErrorPatternsAsync, CanRunAnalyticsCommands);
            _findAnomaliesCommand = new AsyncRelayCommand(FindAnomaliesAsync, CanRunAnalyticsCommands);
            _exportAnalyticsCommand = new AsyncRelayCommand(ExportAnalyticsAsync, CanRunAnalyticsCommands);
            _exportPdfCommand = new AsyncRelayCommand(ExportToPdfAsync, CanRunAnalyticsCommands);
            _exportLast24HoursCommand = new AsyncRelayCommand(ExportLast24HoursAsync, CanRunAnalyticsCommands);
            _exportMarkdownCommand = new AsyncRelayCommand(ExportToMarkdownAsync, CanRunAnalyticsCommands);
            _importWindowsEventLogCommand = new AsyncRelayCommand(ImportWindowsEventLogAsync);
            _importSyslogCommand = new AsyncRelayCommand(ImportSyslogAsync);
            _importCustomFormatCommand = new AsyncRelayCommand(ImportCustomFormatAsync);

            SwitchToLogsViewCommand = new RelayCommand(() => ActiveView = "Logs");
            SwitchToAnalyticsViewCommand = new RelayCommand(() =>
            {
                ActiveView = "Analytics";
                if (!_refreshAnalyticsCommand.IsRunning)
                {
                    _ = _refreshAnalyticsCommand.ExecuteAsync(null);
                }
            });
            SwitchToBookmarksViewCommand = new RelayCommand(() => ActiveView = "Bookmarks");
            SwitchToAlertsViewCommand = new RelayCommand(() => ActiveView = "Alerts");
            SwitchToCompareViewCommand = new RelayCommand(() => ActiveView = "Compare");
            SwitchToExportViewCommand = new RelayCommand(() => ActiveView = "Export");
            SwitchToSettingsViewCommand = new RelayCommand(() => ActiveView = "Settings");

            PropertyChanged += OnEnhancedPropertyChanged;
            LogEntries.CollectionChanged += OnLogEntriesCollectionChanged;
            _alertService.AlertTriggered += OnAlertTriggered;
            _bookmarkService.BookmarkAdded += OnBookmarkAdded;

            RestoreSession();
            NotifyAnalyticsCommandStates();
            UpdatePanelVisibility();

            _logger.LogInformation("EnhancedMainViewModel initialized");
        }

        // Bookmark Methods
        private void AddBookmark()
        {
            if (SelectedLogEntry == null) return;

            var name = $"Bookmark at {SelectedLogEntry.FormattedTimestamp}";
            var bookmark = _bookmarkService.AddBookmark(SelectedLogEntry.Id, name);
            SelectedLogEntry.IsBookmarked = true;
            
            _logger.LogInformation("Added bookmark: {Name}", name);
        }

        private void RemoveBookmark(LogBookmark? bookmark)
        {
            if (bookmark == null) return;
            _bookmarkService.RemoveBookmark(bookmark.Id);
        }

        private void GoToBookmark(LogBookmark? bookmark)
        {
            if (bookmark == null) return;
            // This would navigate to the log entry
            _logger.LogInformation("Navigating to bookmark: {Name}", bookmark.Name);
        }

        // Tag Methods
        private void AddTag(string? tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName)) return;
            _bookmarkService.AddTag(tagName);
        }

        private void AssignTag(LogTag? tag)
        {
            if (tag == null || SelectedLogEntry == null) return;
            _bookmarkService.AssignTagToEntry(SelectedLogEntry.Id, tag.Id);
            SelectedLogEntry.Tags.Add(tag);
        }

        private void RemoveTag(LogTag? tag)
        {
            if (tag == null) return;
            _bookmarkService.RemoveTag(tag.Id);
        }

        private void FilterByTag(LogTag? tag)
        {
            if (tag == null) return;
            // Implement tag filtering
            _logger.LogInformation("Filtering by tag: {TagName}", tag.Name);
        }

        // Alert Methods
        private void CreateAlert()
        {
            // This would open a dialog to create a new alert
            _logger.LogInformation("Creating new smart alert");
        }

        private void EnableAlert(SmartAlert? alert)
        {
            if (alert == null) return;
            _alertService.EnableAlert(alert.Id);
        }

        private void DisableAlert(SmartAlert? alert)
        {
            if (alert == null) return;
            _alertService.DisableAlert(alert.Id);
        }

        private void AcknowledgeAlert(LogAlert? alert)
        {
            if (alert == null) return;
            _alertService.AcknowledgeAlert(alert.AlertName);
        }

        private void OnAlertTriggered(object? sender, LogAlert alert)
        {
            _logger.LogWarning("Alert triggered: {AlertName}", alert.AlertName);
            // Could show notification here
        }

        private void OnBookmarkAdded(object? sender, LogBookmark bookmark)
        {
            _logger.LogInformation("Bookmark added: {Name}", bookmark.Name);
        }

        // Analytics Methods
        private async Task RefreshAnalyticsAsync()
        {
            try
            {
                var snapshot = LogEntries.ToList();
                if (!snapshot.Any())
                {
                    CurrentStatistics = null;
                    TimeSeriesData.Clear();
                    _logLevelDistribution.Clear();
                    _topSources.Clear();
                    return;
                }

                await Task.Run(() =>
                {
                    CurrentStatistics = _analyticsEngine.GetStatistics(snapshot);
                    TimeSeriesData = new ObservableCollection<LogDataPoint>(
                        _analyticsEngine.GetTimeSeriesData(snapshot, TimeSpan.FromHours(1)));
                    
                    RefreshDistribution(snapshot);
                    RefreshTopSources(snapshot);
                });
                
                _logger.LogInformation("Analytics refreshed for {Count} entries", snapshot.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh analytics");
            }
            finally
            {
                NotifyAnalyticsCommandStates();
            }
        }

        private async Task AnalyzeErrorPatternsAsync()
        {
            try
            {
                var snapshot = LogEntries.ToList();
                var clusters = await Task.Run(() => _analyticsEngine.AnalyzeErrorPatterns(snapshot));
                ErrorClusters = new ObservableCollection<ErrorCluster>(clusters);
                
                _logger.LogInformation("Identified {Count} error clusters", ErrorClusters.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze error patterns");
            }
            finally
            {
                NotifyAnalyticsCommandStates();
            }
        }

        private async Task FindAnomaliesAsync()
        {
            try
            {
                var snapshot = LogEntries.ToList();
                var anomalies = await Task.Run(() => _analyticsEngine.FindAnomalies(snapshot));
                
                foreach (var anomaly in anomalies)
                {
                    anomaly.IsHighlighted = true;
                }
                
                _logger.LogInformation("Found {Count} anomalies", anomalies.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to find anomalies");
            }
            finally
            {
                NotifyAnalyticsCommandStates();
            }
        }

        private async Task ExportAnalyticsAsync()
        {
            try
            {
                var snapshot = LogEntries.ToList();
                if (!snapshot.Any()) return;

                var folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var defaultPath = Path.Combine(folder, $"ErrorLog_Analytics_{DateTime.Now:yyyyMMdd_HHmmss}.md");

                await _enhancedExportService.ExportToMarkdownAsync(snapshot, defaultPath);
                StatusMessage = $"Analytics exported to {defaultPath}";
                _logger.LogInformation("Analytics exported to {File}", defaultPath);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Export failed: {ex.Message}";
                _logger.LogError(ex, "Failed to export analytics");
            }
        }

        // Export Methods
        private async Task ExportToPdfAsync()
        {
            try
            {
                var logs = LogEntries.ToList();
                if (!logs.Any())
                {
                    StatusMessage = "No logs to export";
                    return;
                }

                var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
                    $"ErrorLog_Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
                
                await _enhancedExportService.ExportToPdfAsync(logs, filePath);
                StatusMessage = $"Exported {logs.Count} logs to PDF: {Path.GetFileName(filePath)}";
                _logger.LogInformation("Exported to PDF: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                StatusMessage = $"PDF export failed: {ex.Message}";
                _logger.LogError(ex, "Failed to export PDF");
            }
        }

        private async Task ExportLast24HoursAsync()
        {
            try
            {
                var logs = LogEntries.ToList();
                if (!logs.Any())
                {
                    StatusMessage = "No logs to export";
                    return;
                }

                var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    $"ErrorLog_Last24Hours_{DateTime.Now:yyyyMMdd}.pdf");
                
                await _enhancedExportService.ExportLast24HoursReportAsync(logs, filePath);
                StatusMessage = $"Exported 24-hour report: {Path.GetFileName(filePath)}";
                _logger.LogInformation("Exported 24-hour report: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                StatusMessage = $"24-hour export failed: {ex.Message}";
                _logger.LogError(ex, "Failed to export 24-hour report");
            }
        }

        private async Task ExportToMarkdownAsync()
        {
            try
            {
                var logs = LogEntries.ToList();
                if (!logs.Any())
                {
                    StatusMessage = "No logs to export";
                    return;
                }

                var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    $"ErrorLog_Report_{DateTime.Now:yyyyMMdd_HHmmss}.md");
                
                await _enhancedExportService.ExportToMarkdownAsync(logs, filePath);
                StatusMessage = $"Exported {logs.Count} logs to Markdown: {Path.GetFileName(filePath)}";
                _logger.LogInformation("Exported to Markdown: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Markdown export failed: {ex.Message}";
                _logger.LogError(ex, "Failed to export Markdown");
            }
        }

        // External Source Methods
        private async Task ImportWindowsEventLogAsync()
        {
            try
            {
                StatusMessage = "Importing Windows Event Log...";
                var logs = await _externalLogSourceService.ImportFromWindowsEventLogAsync("Application", DateTime.Now.AddDays(-1));
                
                if (logs.Any())
                {
                    foreach (var entry in logs)
                    {
                        LogEntries.Add(entry);
                    }
                    StatusMessage = $"Imported {logs.Count} entries from Windows Event Log";
                    NotifyAnalyticsCommandStates();
                }
                else
                {
                    StatusMessage = "No entries found in Windows Event Log";
                }
                
                _logger.LogInformation("Imported {Count} entries from Windows Event Log", logs.Count);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Import failed: {ex.Message}";
                _logger.LogError(ex, "Failed to import Windows Event Log");
            }
        }

        private async Task ImportSyslogAsync()
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "Syslog Files (*.log;*.txt)|*.log;*.txt|All Files (*.*)|*.*",
                    Title = "Select Syslog File"
                };

                if (dialog.ShowDialog() == true)
                {
                    StatusMessage = "Importing syslog...";
                    var logs = await _externalLogSourceService.ImportFromSyslogAsync(dialog.FileName);
                    
                    foreach (var entry in logs)
                    {
                        LogEntries.Add(entry);
                    }
                    
                    StatusMessage = $"Imported {logs.Count} entries from syslog";
                    NotifyAnalyticsCommandStates();
                    _logger.LogInformation("Imported {Count} entries from syslog {File}", logs.Count, dialog.FileName);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Syslog import failed: {ex.Message}";
                _logger.LogError(ex, "Failed to import syslog");
            }
        }

        private async Task ImportCustomFormatAsync()
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "Log Files (*.log;*.txt;*.json)|*.log;*.txt;*.json|All Files (*.*)|*.*",
                    Title = "Select Log File"
                };

                if (dialog.ShowDialog() == true)
                {
                    var regex = Microsoft.VisualBasic.Interaction.InputBox(
                        "Enter regex pattern for log parsing:\\n" +
                        "Example: (?<timestamp>\\\\S+) \\\\[(?<level>\\\\w+)\\\\] (?<message>.*)",
                        "Custom Format Pattern",
                        @"(?<timestamp>\S+) \[(?<level>\w+)\] (?<message>.*)");

                    if (!string.IsNullOrWhiteSpace(regex))
                    {
                        StatusMessage = "Importing custom format...";
                        var logs = await _externalLogSourceService.ParseCustomFormatAsync(dialog.FileName, regex);
                        
                        foreach (var entry in logs)
                        {
                            LogEntries.Add(entry);
                        }
                        
                        StatusMessage = $"Imported {logs.Count} entries using custom format";
                        NotifyAnalyticsCommandStates();
                        _logger.LogInformation("Imported {Count} entries using custom format", logs.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Custom format import failed: {ex.Message}";
                _logger.LogError(ex, "Failed to import custom format");
            }
        }

        // Helper methods
        private bool CanRunAnalyticsCommands() => LogEntries.Count > 0 && !_isRestoringSession;

        private void RefreshDistribution(List<LogEntry> snapshot)
        {
            _logLevelDistribution.Clear();
            foreach (var kvp in _analyticsEngine.GetLogDistribution(snapshot))
            {
                _logLevelDistribution.Add(new KeyValuePair<string, int>(kvp.Key.ToString(), kvp.Value));
            }
        }

        private void RefreshTopSources(List<LogEntry> snapshot)
        {
            _topSources.Clear();
            foreach (var kvp in _analyticsEngine.GetTopSources(snapshot, 10))
            {
                _topSources.Add(new KeyValuePair<string, int>(kvp.Key, kvp.Value));
            }
        }

        private void NotifyAnalyticsCommandStates()
        {
            _refreshAnalyticsCommand.NotifyCanExecuteChanged();
            _analyzeErrorPatternsCommand.NotifyCanExecuteChanged();
            _findAnomaliesCommand.NotifyCanExecuteChanged();
            _exportAnalyticsCommand.NotifyCanExecuteChanged();
            _exportPdfCommand.NotifyCanExecuteChanged();
            _exportLast24HoursCommand.NotifyCanExecuteChanged();
            _exportMarkdownCommand.NotifyCanExecuteChanged();
        }

        private void UpdatePanelVisibility()
        {
            ShowAnalyticsDashboard = ActiveView == "Analytics" && LogEntries.Any();
            ShowBookmarksPanel = ActiveView == "Bookmarks";
            ShowAlertsPanel = TriggeredAlerts.Any();
        }

        private void OnEnhancedPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ActiveView))
            {
                UpdatePanelVisibility();
            }
        }

        private void OnLogEntriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            NotifyAnalyticsCommandStates();
            UpdatePanelVisibility();
        }

        private void RestoreSession()
        {
            try
            {
                _isRestoringSession = true;
                var session = _sessionStateService.LoadState();
                
                if (session?.LogDirectory != null)
                {
                    _logger.LogInformation("Session restored from {Dir}, active view: {View}", 
                        session.LogDirectory, session.ActiveView);
                    
                    if (!string.IsNullOrEmpty(session.ActiveView))
                    {
                        ActiveView = session.ActiveView;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore session state");
            }
            finally
            {
                _isRestoringSession = false;
                NotifyAnalyticsCommandStates();
            }
        }

        // Override hooks from MainViewModel
        protected override void AfterLogEntryAdded(LogEntry entry)
        {
            base.AfterLogEntryAdded(entry);
            
            _alertService.ProcessLogEntry(entry);
            
            var bookmarks = _bookmarkService.GetBookmarksForEntry(entry.Id);
            entry.IsBookmarked = bookmarks.Any();
            
            entry.Tags.Clear();
            var tags = _bookmarkService.GetTagsForEntry(entry.Id);
            foreach (var tag in tags)
            {
                entry.Tags.Add(tag);
            }
        }

        protected override void OnLogsCleared(object? sender, EventArgs e)
        {
            base.OnLogsCleared(sender, e);
            ErrorClusters.Clear();
            TimeSeriesData.Clear();
            _logLevelDistribution.Clear();
            _topSources.Clear();
            NotifyAnalyticsCommandStates();
        }

        protected override void ClearLogsInternal()
        {
            base.ClearLogsInternal();
            ErrorClusters.Clear();
            TimeSeriesData.Clear();
            _logLevelDistribution.Clear();
            _topSources.Clear();
            NotifyAnalyticsCommandStates();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    _logger.LogInformation("Disposing EnhancedMainViewModel");

                    // Unsubscribe from events
                    PropertyChanged -= OnEnhancedPropertyChanged;
                    LogEntries.CollectionChanged -= OnLogEntriesCollectionChanged;
                    _alertService.AlertTriggered -= OnAlertTriggered;
                    _bookmarkService.BookmarkAdded -= OnBookmarkAdded;

                    // Clear enhanced collections
                    ErrorClusters.Clear();
                    TimeSeriesData.Clear();
                    _logLevelDistribution.Clear();
                    _topSources.Clear();

                    _logger.LogInformation("EnhancedMainViewModel disposed successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during EnhancedMainViewModel disposal");
                }
            }

            // Call base disposal
            base.Dispose(disposing);
        }
    }
}
