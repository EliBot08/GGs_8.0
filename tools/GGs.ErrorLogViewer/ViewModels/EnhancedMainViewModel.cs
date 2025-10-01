#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GGs.ErrorLogViewer.Models;
using GGs.ErrorLogViewer.Services;
using Microsoft.Extensions.Logging;

namespace GGs.ErrorLogViewer.ViewModels
{
    /// <summary>
    /// Enhanced ViewModel with bookmarks, tags, alerts, and analytics
    /// This extends the base MainViewModel with professional features
    /// </summary>
    public partial class EnhancedMainViewModel : ObservableObject
    {
        private readonly IBookmarkService _bookmarkService;
        private readonly ISmartAlertService _alertService;
        private readonly IAnalyticsEngine _analyticsEngine;
        private readonly ISessionStateService _sessionStateService;
        private readonly IEnhancedExportService _enhancedExportService;
        private readonly IExternalLogSourceService _externalLogSourceService;
        private readonly ILogger<EnhancedMainViewModel> _logger;

        // Bookmarks and Tags
        public ObservableCollection<LogBookmark> Bookmarks => _bookmarkService.Bookmarks;
        public ObservableCollection<LogTag> AvailableTags => _bookmarkService.Tags;

        // Smart Alerts
        public ObservableCollection<SmartAlert> SmartAlerts => _alertService.Alerts;
        public ObservableCollection<LogAlert> TriggeredAlerts => _alertService.TriggeredAlerts;

        // Analytics
        [ObservableProperty]
        private LogStatistics? _currentStatistics;

        [ObservableProperty]
        private ObservableCollection<ErrorCluster> _errorClusters = new();

        [ObservableProperty]
        private ObservableCollection<LogDataPoint> _timeSeriesData = new();

        // UI State
        [ObservableProperty]
        private string _activeView = "Logs"; // Logs, Analytics, Bookmarks, Alerts, Settings

        [ObservableProperty]
        private bool _showAnalyticsDashboard = false;

        [ObservableProperty]
        private bool _showBookmarksPanel = false;

        [ObservableProperty]
        private bool _showAlertsPanel = true;

        [ObservableProperty]
        private LogEntry? _selectedLogEntry;

        // Commands - Bookmarks
        public ICommand AddBookmarkCommand { get; }
        public ICommand RemoveBookmarkCommand { get; }
        public ICommand GoToBookmarkCommand { get; }

        // Commands - Tags
        public ICommand AddTagCommand { get; }
        public ICommand AssignTagCommand { get; }
        public ICommand RemoveTagCommand { get; }
        public ICommand FilterByTagCommand { get; }

        // Commands - Alerts
        public ICommand CreateAlertCommand { get; }
        public ICommand EnableAlertCommand { get; }
        public ICommand DisableAlertCommand { get; }
        public ICommand AcknowledgeAlertCommand { get; }
        public ICommand ClearAlertsCommand { get; }

        // Commands - Analytics
        public ICommand RefreshAnalyticsCommand { get; }
        public ICommand AnalyzeErrorPatternsCommand { get; }
        public ICommand FindAnomaliesCommand { get; }
        public ICommand ExportAnalyticsCommand { get; }

        // Commands - Export
        public ICommand ExportToPdfCommand { get; }
        public ICommand ExportLast24HoursCommand { get; }
        public ICommand ExportToMarkdownCommand { get; }

        // Commands - External Sources
        public ICommand ImportWindowsEventLogCommand { get; }
        public ICommand ImportSyslogCommand { get; }
        public ICommand ImportCustomFormatCommand { get; }

        // Commands - Views
        public ICommand SwitchToLogsViewCommand { get; }
        public ICommand SwitchToAnalyticsViewCommand { get; }
        public ICommand SwitchToBookmarksViewCommand { get; }
        public ICommand SwitchToAlertsViewCommand { get; }

        public EnhancedMainViewModel(
            IBookmarkService bookmarkService,
            ISmartAlertService alertService,
            IAnalyticsEngine analyticsEngine,
            ISessionStateService sessionStateService,
            IEnhancedExportService enhancedExportService,
            IExternalLogSourceService externalLogSourceService,
            ILogger<EnhancedMainViewModel> logger)
        {
            _bookmarkService = bookmarkService ?? throw new ArgumentNullException(nameof(bookmarkService));
            _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));
            _analyticsEngine = analyticsEngine ?? throw new ArgumentNullException(nameof(analyticsEngine));
            _sessionStateService = sessionStateService ?? throw new ArgumentNullException(nameof(sessionStateService));
            _enhancedExportService = enhancedExportService ?? throw new ArgumentNullException(nameof(enhancedExportService));
            _externalLogSourceService = externalLogSourceService ?? throw new ArgumentNullException(nameof(externalLogSourceService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize commands - Bookmarks
            AddBookmarkCommand = new RelayCommand(AddBookmark, () => SelectedLogEntry != null);
            RemoveBookmarkCommand = new RelayCommand<LogBookmark>(RemoveBookmark);
            GoToBookmarkCommand = new RelayCommand<LogBookmark>(GoToBookmark);

            // Initialize commands - Tags
            AddTagCommand = new RelayCommand<string>(AddTag);
            AssignTagCommand = new RelayCommand<LogTag>(AssignTag, _ => SelectedLogEntry != null);
            RemoveTagCommand = new RelayCommand<LogTag>(RemoveTag);
            FilterByTagCommand = new RelayCommand<LogTag>(FilterByTag);

            // Initialize commands - Alerts
            CreateAlertCommand = new RelayCommand(CreateAlert);
            EnableAlertCommand = new RelayCommand<SmartAlert>(EnableAlert);
            DisableAlertCommand = new RelayCommand<SmartAlert>(DisableAlert);
            AcknowledgeAlertCommand = new RelayCommand<LogAlert>(AcknowledgeAlert);
            ClearAlertsCommand = new RelayCommand(_alertService.ClearTriggeredAlerts);

            // Initialize commands - Analytics
            RefreshAnalyticsCommand = new AsyncRelayCommand(RefreshAnalyticsAsync);
            AnalyzeErrorPatternsCommand = new AsyncRelayCommand(AnalyzeErrorPatternsAsync);
            FindAnomaliesCommand = new AsyncRelayCommand(FindAnomaliesAsync);
            ExportAnalyticsCommand = new AsyncRelayCommand(ExportAnalyticsAsync);

            // Initialize commands - Export
            ExportToPdfCommand = new AsyncRelayCommand(ExportToPdfAsync);
            ExportLast24HoursCommand = new AsyncRelayCommand(ExportLast24HoursAsync);
            ExportToMarkdownCommand = new AsyncRelayCommand(ExportToMarkdownAsync);

            // Initialize commands - External Sources
            ImportWindowsEventLogCommand = new AsyncRelayCommand(ImportWindowsEventLogAsync);
            ImportSyslogCommand = new AsyncRelayCommand(ImportSyslogAsync);
            ImportCustomFormatCommand = new AsyncRelayCommand(ImportCustomFormatAsync);

            // Initialize commands - Views
            SwitchToLogsViewCommand = new RelayCommand(() => ActiveView = "Logs");
            SwitchToAnalyticsViewCommand = new RelayCommand(() => { ActiveView = "Analytics"; RefreshAnalyticsAsync(); });
            SwitchToBookmarksViewCommand = new RelayCommand(() => ActiveView = "Bookmarks");
            SwitchToAlertsViewCommand = new RelayCommand(() => ActiveView = "Alerts");

            // Subscribe to events
            _alertService.AlertTriggered += OnAlertTriggered;
            _bookmarkService.BookmarkAdded += OnBookmarkAdded;

            _logger.LogInformation("EnhancedMainViewModel initialized with professional features");
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
                // This would get logs from the main view model
                var logs = new List<LogEntry>(); // TODO: Get from main ViewModel
                
                CurrentStatistics = _analyticsEngine.GetStatistics(logs);
                TimeSeriesData = new ObservableCollection<LogDataPoint>(
                    _analyticsEngine.GetTimeSeriesData(logs, TimeSpan.FromHours(1)));
                
                _logger.LogInformation("Analytics refreshed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh analytics");
            }
        }

        private async Task AnalyzeErrorPatternsAsync()
        {
            try
            {
                var logs = new List<LogEntry>(); // TODO: Get from main ViewModel
                ErrorClusters = new ObservableCollection<ErrorCluster>(
                    _analyticsEngine.AnalyzeErrorPatterns(logs));
                
                _logger.LogInformation("Identified {Count} error clusters", ErrorClusters.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze error patterns");
            }
        }

        private async Task FindAnomaliesAsync()
        {
            try
            {
                var logs = new List<LogEntry>(); // TODO: Get from main ViewModel
                var anomalies = _analyticsEngine.FindAnomalies(logs);
                
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
        }

        private async Task ExportAnalyticsAsync()
        {
            // Export analytics report
            _logger.LogInformation("Exporting analytics report");
        }

        // Export Methods
        private async Task ExportToPdfAsync()
        {
            try
            {
                var logs = new List<LogEntry>(); // TODO: Get from main ViewModel
                var filePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
                    $"ErrorLog_Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
                
                await _enhancedExportService.ExportToPdfAsync(logs, filePath);
                _logger.LogInformation("Exported to PDF: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export PDF");
            }
        }

        private async Task ExportLast24HoursAsync()
        {
            try
            {
                var logs = new List<LogEntry>(); // TODO: Get from main ViewModel
                var filePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    $"ErrorLog_Last24Hours_{DateTime.Now:yyyyMMdd}.pdf");
                
                await _enhancedExportService.ExportLast24HoursReportAsync(logs, filePath);
                _logger.LogInformation("Exported 24-hour report: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export 24-hour report");
            }
        }

        private async Task ExportToMarkdownAsync()
        {
            try
            {
                var logs = new List<LogEntry>(); // TODO: Get from main ViewModel
                var filePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    $"ErrorLog_Report_{DateTime.Now:yyyyMMdd_HHmmss}.md");
                
                await _enhancedExportService.ExportToMarkdownAsync(logs, filePath);
                _logger.LogInformation("Exported to Markdown: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export Markdown");
            }
        }

        // External Source Methods
        private async Task ImportWindowsEventLogAsync()
        {
            try
            {
                var logs = await _externalLogSourceService.ImportFromWindowsEventLogAsync("Application", DateTime.Now.AddDays(-1));
                _logger.LogInformation("Imported {Count} entries from Windows Event Log", logs.Count);
                // TODO: Add to main log collection
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import Windows Event Log");
            }
        }

        private async Task ImportSyslogAsync()
        {
            // Would open file dialog
            _logger.LogInformation("Importing syslog");
        }

        private async Task ImportCustomFormatAsync()
        {
            // Would open dialog for custom format
            _logger.LogInformation("Importing custom format");
        }

        public void ProcessLogEntry(LogEntry entry)
        {
            // Process through alert system
            _alertService.ProcessLogEntry(entry);
            
            // Check if bookmarked
            var bookmarks = _bookmarkService.GetBookmarksForEntry(entry.Id);
            entry.IsBookmarked = bookmarks.Any();
            
            // Load tags
            entry.Tags.Clear();
            var tags = _bookmarkService.GetTagsForEntry(entry.Id);
            foreach (var tag in tags)
            {
                entry.Tags.Add(tag);
            }
        }
    }
}
