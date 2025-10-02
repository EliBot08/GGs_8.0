#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows; // Added for Application.Current
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using GGs.ErrorLogViewer.Models;
using GGs.ErrorLogViewer.Services;
using GGs.ErrorLogViewer.Views;

namespace GGs.ErrorLogViewer.ViewModels
{
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private bool _disposed = false;
        private readonly ILogMonitoringService _logMonitoringService;
        private readonly ILogParsingService _logParsingService;
        private readonly IThemeService _themeService;
        private readonly IExportService _exportService;
        private readonly IEarlyLoggingService _earlyLoggingService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MainViewModel> _logger;
        private static class SectionNames
        {
            public const string Logs = "Logs";
        }

        [ObservableProperty]
        private bool _isMonitoring;

        [ObservableProperty]
        private bool _isRawMode = false;

        [ObservableProperty]
        private string _searchText = string.Empty;

        // New: Use regex for search
        [ObservableProperty]
        private bool _useRegex = false;

        // New: Smart Filter for deduplication
        [ObservableProperty]
        private bool _smartFilter = true;

        // New: Font size for log text
        [ObservableProperty]
        private double _logFontSize = 12.0;

        [ObservableProperty]
        private Models.LogLevel _selectedLogLevel = Models.LogLevel.All;

        [ObservableProperty]
        private string _selectedSource = "All";

        [ObservableProperty]
        private int _totalLogCount;

        [ObservableProperty]
        private int _filteredLogCount;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private bool _autoScroll = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSelectedLogEntry))]
        private LogEntry? _selectedLogEntry;

        [ObservableProperty]
        private bool _isDetailsPaneVisible = true;

        [ObservableProperty]
        private bool _showFilePathColumn = false;

        public ObservableCollection<LogEntry> LogEntries { get; }
        public ICollectionView LogEntriesView { get; }
        public ObservableCollection<string> AvailableSources { get; }
        public IThemeService ThemeService => _themeService;
        public bool HasSelectedLogEntry => SelectedLogEntry != null;

        // Smart Filter: Track seen messages for deduplication
        private readonly HashSet<string> _seenMessages = new HashSet<string>();
        private string _logDirectory = string.Empty;
        public string? CurrentLogDirectory => string.IsNullOrWhiteSpace(_logDirectory) ? null : _logDirectory;

        public ICommand StartMonitoringCommand { get; protected set; }
        public ICommand StopMonitoringCommand { get; protected set; }
        public ICommand ToggleThemeCommand { get; protected set; }
        public ICommand RefreshCommand { get; protected set; }
        public ICommand ClearLogsCommand { get; protected set; }
        public ICommand ExportLogsCommand { get; protected set; }
        public ICommand ExportToCsvCommand { get; protected set; }
        public ICommand ExportToJsonCommand { get; protected set; }
        public ICommand CopySelectedCommand { get; protected set; }
        public ICommand OpenLogDirectoryCommand { get; protected set; }
        public ICommand ClearOldLogsCommand { get; protected set; }
        // New copy commands
        public ICommand CopyRawCommand { get; protected set; }
        public ICommand CopyCompactCommand { get; protected set; }
        public ICommand CopyDetailsCommand { get; protected set; }
        public ICommand ToggleDetailsPaneCommand { get; protected set; }
        public ICommand ToggleFilePathColumnCommand { get; protected set; }
        
        // Navigation commands (placeholders - implemented in enhanced VM)
        public ICommand SwitchToLogsViewCommand { get; protected set; }
        public ICommand SwitchToAnalyticsViewCommand { get; protected set; }
        public ICommand SwitchToBookmarksViewCommand { get; protected set; }
        public ICommand SwitchToAlertsViewCommand { get; protected set; }

        public MainViewModel(
            ILogMonitoringService logMonitoringService,
            ILogParsingService logParsingService,
            IThemeService themeService,
            IExportService exportService,
            IEarlyLoggingService earlyLoggingService,
            IConfiguration configuration,
            ILogger<MainViewModel> logger)
        {
            _logMonitoringService = logMonitoringService ?? throw new ArgumentNullException(nameof(logMonitoringService));
            _logParsingService = logParsingService ?? throw new ArgumentNullException(nameof(logParsingService));
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
            _earlyLoggingService = earlyLoggingService ?? throw new ArgumentNullException(nameof(earlyLoggingService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            LogEntries = new ObservableCollection<LogEntry>();
            AvailableSources = new ObservableCollection<string> { "All" };

            LogEntriesView = CollectionViewSource.GetDefaultView(LogEntries);
            LogEntriesView.Filter = FilterLogEntry;

            PropertyChanged += OnPropertyChanged;

            StartMonitoringCommand = new AsyncRelayCommand(StartMonitoringInternalAsync, () => !IsMonitoring);
            StopMonitoringCommand = new AsyncRelayCommand(StopMonitoringInternalAsync, () => IsMonitoring);
            ToggleThemeCommand = new RelayCommand(_themeService.ToggleTheme);
            RefreshCommand = new AsyncRelayCommand(RefreshLogsAsync);
            ClearLogsCommand = new RelayCommand(ClearLogsInternal);
            ExportLogsCommand = new AsyncRelayCommand(ExportLogsAsync);
            ExportToCsvCommand = new AsyncRelayCommand(ExportToCsvAsync);
            ExportToJsonCommand = new AsyncRelayCommand(ExportToJsonAsync);
            CopySelectedCommand = new RelayCommand(CopySelected, () => SelectedLogEntry != null);
            OpenLogDirectoryCommand = new RelayCommand(OpenLogDirectory);
            ClearOldLogsCommand = new RelayCommand(ClearOldLogs);
            CopyRawCommand = new RelayCommand(CopyRaw, () => SelectedLogEntry != null);
            CopyCompactCommand = new RelayCommand(CopyCompact, () => SelectedLogEntry != null);
            CopyDetailsCommand = new RelayCommand(CopyDetails, () => SelectedLogEntry != null);
            ToggleDetailsPaneCommand = new RelayCommand(() => IsDetailsPaneVisible = !IsDetailsPaneVisible);
            ToggleFilePathColumnCommand = new RelayCommand(() => ShowFilePathColumn = !ShowFilePathColumn);

            SwitchToLogsViewCommand = new RelayCommand(() => { /* Placeholder - Enhanced VM handles actual navigation */ });
            SwitchToAnalyticsViewCommand = new RelayCommand(() => { /* Placeholder */ });
            SwitchToBookmarksViewCommand = new RelayCommand(() => { /* Placeholder */ });
            SwitchToAlertsViewCommand = new RelayCommand(() => { /* Placeholder */ });

            _logMonitoringService.LogEntriesAdded += OnLogEntriesAdded;
            _logMonitoringService.LogsCleared += OnLogsCleared;

            LogEntriesView.MoveCurrentToFirst();

            // Always start with AutoScroll off, regardless of session restore
            AutoScroll = false;
        }

        public void SetLogDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                _logger.LogWarning("Requested log directory '{Directory}' is invalid", directory);
                StatusMessage = "Log directory invalid";
                return;
            }

            if (string.Equals(_logDirectory, directory, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _logDirectory = directory;
            StatusMessage = $"Monitoring directory set to {_logDirectory}";

            if (IsMonitoring)
            {
                _ = RestartMonitoringAsync();
            }
        }

        public void AutoStartMonitoring()
        {
            _ = AutoStartMonitoringAsync();
        }

        public Task AutoStartMonitoringAsync()
        {
            return StartMonitoringInternalAsync();
        }

        private async Task StartMonitoringInternalAsync()
        {
            var targetDirectory = ResolveLogDirectory();
            if (string.IsNullOrEmpty(targetDirectory))
            {
                StatusMessage = "No log directory configured";
                return;
            }

            if (IsMonitoring)
            {
                StatusMessage = "Monitoring already active";
                return;
            }

            try
            {
                await _logMonitoringService.StartMonitoringAsync(targetDirectory);
                IsMonitoring = true;
                StatusMessage = $"Monitoring {targetDirectory}";
                RaiseMonitoringCommandCanExecuteChanged();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to start monitoring: {ex.Message}";
                _logger.LogError(ex, "Failed to start monitoring for directory {Directory}", targetDirectory);
            }
        }

        private async Task StopMonitoringInternalAsync()
        {
            if (!IsMonitoring)
            {
                StatusMessage = "Monitoring already stopped";
                return;
            }

            try
            {
                await _logMonitoringService.StopMonitoringAsync();
                IsMonitoring = false;
                StatusMessage = "Monitoring stopped";
                RaiseMonitoringCommandCanExecuteChanged();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to stop monitoring: {ex.Message}";
                _logger.LogError(ex, "Failed to stop monitoring");
            }
        }

        private async Task RestartMonitoringAsync()
        {
            if (!IsMonitoring)
            {
                await StartMonitoringInternalAsync();
                return;
            }

            await StopMonitoringInternalAsync();
            await StartMonitoringInternalAsync();
        }

        private void RaiseMonitoringCommandCanExecuteChanged()
        {
            ((AsyncRelayCommand)StartMonitoringCommand).NotifyCanExecuteChanged();
            ((AsyncRelayCommand)StopMonitoringCommand).NotifyCanExecuteChanged();
        }

        private string ResolveLogDirectory()
        {
            if (!string.IsNullOrEmpty(_logDirectory))
            {
                return _logDirectory;
            }

            var defaultDir = _configuration["Logging:DefaultDirectory"] ??
                             Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GGs", "Logs");

            if (!Directory.Exists(defaultDir))
            {
                Directory.CreateDirectory(defaultDir);
            }

            _logDirectory = defaultDir;
            return _logDirectory;
        }

        protected virtual void ClearLogsInternal()
        {
            try
            {
                LogEntries.Clear();
                _seenMessages.Clear();
                UpdateCounts();
                SelectedLogEntry = null;
                StatusMessage = "Log view cleared";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to clear logs: {ex.Message}";
                _logger.LogError(ex, "Failed to clear logs");
            }
        }

        private async Task ExportLogsAsync()
        {
            try
            {
                _earlyLoggingService.LogApplicationEvent("Export", "Export dialog opened");

                // Show format selection dialog
                var formatDialog = new ExportFormatDialog();
                if (formatDialog.ShowDialog() != true)
                {
                    _earlyLoggingService.LogApplicationEvent("Export", "Export cancelled by user");
                    return;
                }

                var selectedFormat = formatDialog.SelectedFormat;
                var includeDetails = formatDialog.IncludeDetails;

                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = _exportService.GetFileFilter(selectedFormat),
                    DefaultExt = GetFileExtension(selectedFormat),
                    FileName = _exportService.GetDefaultFileName(selectedFormat)
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var filteredEntries = LogEntriesView.Cast<LogEntry>();
                    bool success = false;

                    _earlyLoggingService.LogApplicationEvent("Export", "Starting export", new
                    {
                        Format = selectedFormat.ToString(),
                        EntryCount = filteredEntries.Count(),
                        FileName = saveFileDialog.FileName,
                        IncludeDetails = includeDetails
                    });

                    switch (selectedFormat)
                    {
                        case ExportFormat.Csv:
                            success = await _exportService.ExportToCsvAsync(filteredEntries, saveFileDialog.FileName, includeDetails);
                            break;
                        case ExportFormat.Json:
                            success = await _exportService.ExportToJsonAsync(filteredEntries, saveFileDialog.FileName, true);
                            break;
                        case ExportFormat.Xml:
                            success = await _exportService.ExportToXmlAsync(filteredEntries, saveFileDialog.FileName);
                            break;
                        case ExportFormat.Html:
                            success = await _exportService.ExportToHtmlAsync(filteredEntries, saveFileDialog.FileName, _themeService.IsDarkMode);
                            break;
                        case ExportFormat.Text:
                            success = await _exportService.ExportToTextAsync(filteredEntries, saveFileDialog.FileName, !IsRawMode);
                            break;
                    }

                    if (success)
                    {
                        StatusMessage = $"Exported {filteredEntries.Count()} logs to {saveFileDialog.FileName}";
                        _logger.LogInformation("Successfully exported {Count} log entries to {FileName} in {Format} format", 
                            filteredEntries.Count(), saveFileDialog.FileName, selectedFormat);
                        
                        _earlyLoggingService.LogApplicationEvent("Export", "Export completed successfully", new
                        {
                            Format = selectedFormat.ToString(),
                            EntryCount = filteredEntries.Count(),
                            FileName = saveFileDialog.FileName
                        });

                        // Show success message
                        System.Windows.MessageBox.Show($"Successfully exported {filteredEntries.Count()} log entries to {saveFileDialog.FileName}", 
                            "Export Complete", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    }
                    else
                    {
                        StatusMessage = $"Export failed";
                        _logger.LogError("Failed to export logs to {FileName}", saveFileDialog.FileName);
                        _earlyLoggingService.LogApplicationEvent("Export", "Export failed", new { FileName = saveFileDialog.FileName });
                        
                        System.Windows.MessageBox.Show("Failed to export logs. Please check the log file for details.", 
                            "Export Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    }
                }
                else
                {
                    _earlyLoggingService.LogApplicationEvent("Export", "Export cancelled - no file selected");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Export failed: {ex.Message}";
                _logger.LogError(ex, "Failed to export logs");
                _earlyLoggingService.LogApplicationEvent("Export", "Export failed with exception", new { Exception = ex.ToString() });
                
                System.Windows.MessageBox.Show($"Export failed: {ex.Message}", 
                    "Export Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private async Task ExportToCsvAsync()
        {
            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                    DefaultExt = "csv",
                    FileName = $"logs_export_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var filteredEntries = LogEntriesView.Cast<LogEntry>();
                    var success = await _exportService.ExportToCsvAsync(filteredEntries, saveFileDialog.FileName, includeDetails: true);

                    if (success)
                    {
                        StatusMessage = $"Exported {filteredEntries.Count()} logs to CSV";
                        _logger.LogInformation("Successfully exported {Count} log entries to CSV: {FileName}",
                            filteredEntries.Count(), saveFileDialog.FileName);

                        System.Windows.MessageBox.Show($"Successfully exported {filteredEntries.Count()} log entries to:\n{saveFileDialog.FileName}",
                            "Export Complete", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    }
                    else
                    {
                        StatusMessage = "CSV export failed";
                        _logger.LogError("Failed to export logs to CSV: {FileName}", saveFileDialog.FileName);
                        System.Windows.MessageBox.Show("Failed to export logs to CSV. Please check the log file for details.",
                            "Export Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"CSV export failed: {ex.Message}";
                _logger.LogError(ex, "Failed to export logs to CSV");
                System.Windows.MessageBox.Show($"CSV export failed: {ex.Message}",
                    "Export Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private async Task ExportToJsonAsync()
        {
            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                    DefaultExt = "json",
                    FileName = $"logs_export_{DateTime.Now:yyyyMMdd_HHmmss}.json"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var filteredEntries = LogEntriesView.Cast<LogEntry>();
                    var success = await _exportService.ExportToJsonAsync(filteredEntries, saveFileDialog.FileName, true);

                    if (success)
                    {
                        StatusMessage = $"Exported {filteredEntries.Count()} logs to JSON";
                        _logger.LogInformation("Successfully exported {Count} log entries to JSON: {FileName}",
                            filteredEntries.Count(), saveFileDialog.FileName);

                        System.Windows.MessageBox.Show($"Successfully exported {filteredEntries.Count()} log entries to:\n{saveFileDialog.FileName}",
                            "Export Complete", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    }
                    else
                    {
                        StatusMessage = "JSON export failed";
                        _logger.LogError("Failed to export logs to JSON: {FileName}", saveFileDialog.FileName);
                        System.Windows.MessageBox.Show("Failed to export logs to JSON. Please check the log file for details.",
                            "Export Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"JSON export failed: {ex.Message}";
                _logger.LogError(ex, "Failed to export logs to JSON");
                System.Windows.MessageBox.Show($"JSON export failed: {ex.Message}",
                    "Export Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private static string GetFileExtension(ExportFormat format)
        {
            return format switch
            {
                ExportFormat.Csv => "csv",
                ExportFormat.Json => "json",
                ExportFormat.Xml => "xml",
                ExportFormat.Html => "html",
                ExportFormat.Text => "txt",
                _ => "txt"
            };
        }

        private void ToggleViewMode()
        {
            IsRawMode = !IsRawMode;
            StatusMessage = IsRawMode ? "Switched to raw view" : "Switched to compact view";
            _logger.LogInformation("View mode changed to {Mode}", IsRawMode ? "Raw" : "Compact");
        }

        private async Task RefreshLogsAsync()
        {
            try
            {
                StatusMessage = "Refreshing logs...";
                _logMonitoringService.ClearLogs();
                
                var logDirectory = _configuration["Logging:DefaultDirectory"] ?? 
                                  Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GGs", "Logs");
                
                if (!string.IsNullOrEmpty(logDirectory))
                {
                    await _logMonitoringService.LoadHistoricalLogsAsync(logDirectory);
                }
                StatusMessage = "Logs refreshed";
                _logger.LogInformation("Log refresh completed");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Refresh failed: {ex.Message}";
                _logger.LogError(ex, "Failed to refresh logs");
            }
        }

        private void CopySelected()
        {
            if (SelectedLogEntry == null) return;

            try
            {
                var text = IsRawMode ? SelectedLogEntry.RawLine : SelectedLogEntry.CompactMessage;
                System.Windows.Clipboard.SetText(text);
                StatusMessage = "Log entry copied to clipboard";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to copy: {ex.Message}";
                _logger.LogError(ex, "Failed to copy log entry to clipboard");
            }
        }

        // New: explicit copy helpers
        private void CopyRaw()
        {
            if (SelectedLogEntry == null) return;
            try
            {
                System.Windows.Clipboard.SetText(SelectedLogEntry.RawLine);
                StatusMessage = "Raw line copied";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to copy raw: {ex.Message}";
            }
        }

        private void CopyCompact()
        {
            if (SelectedLogEntry == null) return;
            try
            {
                System.Windows.Clipboard.SetText(SelectedLogEntry.CompactMessage);
                StatusMessage = "Compact message copied";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to copy compact: {ex.Message}";
            }
        }

        private void CopyDetails()
        {
            if (SelectedLogEntry == null) return;
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"[{SelectedLogEntry.FormattedTimestamp}] {SelectedLogEntry.Level} {SelectedLogEntry.Source}");
                if (!string.IsNullOrWhiteSpace(SelectedLogEntry.Category)) sb.AppendLine($"Category: {SelectedLogEntry.Category}");
                if (!string.IsNullOrWhiteSpace(SelectedLogEntry.FilePath)) sb.AppendLine($"File: {SelectedLogEntry.FilePath}:{SelectedLogEntry.LineNumber}");
                sb.AppendLine($"Message: {SelectedLogEntry.Message}");
                if (!string.IsNullOrWhiteSpace(SelectedLogEntry.Exception)) sb.AppendLine($"Exception: {SelectedLogEntry.Exception}");
                if (!string.IsNullOrWhiteSpace(SelectedLogEntry.StackTrace)) sb.AppendLine("StackTrace:").AppendLine(SelectedLogEntry.StackTrace);
                System.Windows.Clipboard.SetText(sb.ToString());
                StatusMessage = "Details copied";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to copy details: {ex.Message}";
            }
        }

        private void OpenLogDirectory()
        {
            try
            {
                var logDirectory = _configuration["Logging:DefaultDirectory"] ?? 
                                  Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GGs", "Logs");
                
                if (Directory.Exists(logDirectory))
                {
                    System.Diagnostics.Process.Start("explorer.exe", logDirectory);
                    StatusMessage = "Opened log directory";
                }
                else
                {
                    StatusMessage = "Log directory not found";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to open directory: {ex.Message}";
                _logger.LogError(ex, "Failed to open log directory");
            }
        }

        private void ClearOldLogs()
        {
            try
            {
                var currentTime = DateTime.UtcNow;
                var sessionStartTime = currentTime.AddMinutes(-30); // Consider logs from last 30 minutes as "current session"
                
                var oldLogs = LogEntries.Where(entry => entry.Timestamp < sessionStartTime).ToList();
                
                foreach (var oldLog in oldLogs)
                {
                    LogEntries.Remove(oldLog);
                }
                
                _seenMessages.Clear(); // Clear Smart Filter cache
                UpdateCounts();
                StatusMessage = $"Cleared {oldLogs.Count} old log entries, showing current session only";
                _logger.LogInformation("Cleared {Count} old log entries", oldLogs.Count);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to clear old logs: {ex.Message}";
                _logger.LogError(ex, "Failed to clear old logs");
            }
        }

        // Handle batch log entries for better performance
        protected virtual void OnLogEntriesAdded(object? sender, System.Collections.Generic.IEnumerable<LogEntry> logEntries)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var entriesToAdd = logEntries.ToList();
                    if (!entriesToAdd.Any()) return;

                    // Batch add entries
                    foreach (var entry in entriesToAdd)
                    {
                        LogEntries.Add(entry);
                        AfterLogEntryAdded(entry);
                        // Update available sources
                        if (!string.IsNullOrEmpty(entry.Source) && !AvailableSources.Contains(entry.Source))
                        {
                            AvailableSources.Add(entry.Source);
                        }
                    }
                    
                    UpdateCounts();
                    
                    // Robust autoscroll: only scroll if enabled and view is in Logs
                    if (AutoScroll && entriesToAdd.Any())
                    {
                        LogEntriesView.MoveCurrentTo(entriesToAdd.Last());
                        // Use Dispatcher.BeginInvoke to ensure scroll happens after UI update
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            var mainWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
                            if (mainWindow != null)
                            {
                                var dataGrid = mainWindow.FindName("LogEntriesDataGrid") as System.Windows.Controls.DataGrid;
                                if (dataGrid != null && dataGrid.Items.Count > 0)
                                {
                                    dataGrid.ScrollIntoView(dataGrid.Items[dataGrid.Items.Count - 1]);
                                }
                            }
                        }), System.Windows.Threading.DispatcherPriority.Background);
                    }
                    
                    _logger.LogDebug("Processed batch of {Count} log entries", entriesToAdd.Count);
                });

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing batch log entries");
            }
        }

        protected virtual void OnLogsCleared(object? sender, EventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                LogEntries.Clear();
                UpdateCounts();
            });
        }

        protected virtual void AfterLogEntryAdded(LogEntry entry)
        {
            // Hook for derived classes (e.g., EnhancedMainViewModel) to decorate entries
        }

        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SearchText) || e.PropertyName == nameof(SelectedLogLevel) || 
                e.PropertyName == nameof(SelectedSource) || e.PropertyName == nameof(UseRegex) || 
                e.PropertyName == nameof(SmartFilter))
            {
                // Clear Smart Filter cache when toggled
                if (e.PropertyName == nameof(SmartFilter))
                {
                    _seenMessages.Clear();
                }
                
                LogEntriesView.Refresh();
                UpdateCounts();
            }
        }

        private bool FilterLogEntry(object obj)
        {
            if (obj is not LogEntry entry) return false;

            // Smart Filter: Deduplication for Error and Warning levels
            if (SmartFilter && (entry.Level == Models.LogLevel.Error || entry.Level == Models.LogLevel.Critical || entry.Level == Models.LogLevel.Warning))
            {
                var messageKey = $"{entry.Level}:{entry.Message}:{entry.Source}";
                if (_seenMessages.Contains(messageKey))
                {
                    return false; // Skip duplicate error/warning
                }
                _seenMessages.Add(messageKey);
            }

            // Filter by log level
            if (SelectedLogLevel != Models.LogLevel.All && entry.Level != SelectedLogLevel)
                return false;

            // Filter by source
            if (SelectedSource != "All" && !string.Equals(entry.Source, SelectedSource, StringComparison.OrdinalIgnoreCase))
                return false;

            // Filter by search text
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                try
                {
                    if (UseRegex)
                    {
                        // Use regex search across multiple fields
                        var regex = new System.Text.RegularExpressions.Regex(SearchText, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        return regex.IsMatch(entry.Message ?? string.Empty) ||
                               regex.IsMatch(entry.Source ?? string.Empty) ||
                               regex.IsMatch(entry.RawLine ?? string.Empty) ||
                               regex.IsMatch(entry.CompactMessage ?? string.Empty);
                    }
                    else
                    {
                        // Simple text search across multiple fields
                        var searchLower = SearchText.ToLowerInvariant();
                        return (entry.Message?.ToLowerInvariant().Contains(searchLower) == true) ||
                               (entry.Source?.ToLowerInvariant().Contains(searchLower) == true) ||
                               (entry.RawLine?.ToLowerInvariant().Contains(searchLower) == true) ||
                               (entry.CompactMessage?.ToLowerInvariant().Contains(searchLower) == true);
                    }
                }
                catch (ArgumentException)
                {
                    // If regex is invalid, fall back to simple text search
                    var searchLower = SearchText.ToLowerInvariant();
                    return (entry.Message?.ToLowerInvariant().Contains(searchLower) == true) ||
                           (entry.Source?.ToLowerInvariant().Contains(searchLower) == true) ||
                           (entry.RawLine?.ToLowerInvariant().Contains(searchLower) == true) ||
                           (entry.CompactMessage?.ToLowerInvariant().Contains(searchLower) == true);
                }
            }

            return true;
        }

        private void UpdateCounts()
        {
            TotalLogCount = LogEntries.Count;
            FilteredLogCount = LogEntriesView.Cast<LogEntry>().Count();
        }

        private static string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\"", "\"\"");
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            if (e.PropertyName == nameof(SelectedLogEntry))
            {
                ((RelayCommand)CopySelectedCommand).NotifyCanExecuteChanged();
                ((RelayCommand)CopyRawCommand).NotifyCanExecuteChanged();
                ((RelayCommand)CopyCompactCommand).NotifyCanExecuteChanged();
                ((RelayCommand)CopyDetailsCommand).NotifyCanExecuteChanged();
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try
                    {
                        // Stop monitoring if active
                        if (IsMonitoring)
                        {
                            _logger.LogInformation("Stopping log monitoring during disposal");
                            try
                            {
                                StopMonitoringCommand.Execute(null);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Error stopping monitoring during disposal");
                            }
                        }

                        // Clear collections
                        try
                        {
                            LogEntries.Clear();
                            AvailableSources.Clear();
                            _seenMessages.Clear();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error clearing collections during disposal");
                        }

                        _logger.LogInformation("MainViewModel disposed successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during MainViewModel disposal");
                    }
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
