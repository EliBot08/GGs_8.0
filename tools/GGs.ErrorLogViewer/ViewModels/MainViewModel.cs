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
    public partial class MainViewModel : ObservableObject
    {
        private readonly ILogMonitoringService _logMonitoringService;
        private readonly ILogParsingService _logParsingService;
        private readonly IThemeService _themeService;
        private readonly IExportService _exportService;
        private readonly IEarlyLoggingService _earlyLoggingService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MainViewModel> _logger;

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
        private bool _smartFilter = false;

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
        private LogEntry? _selectedLogEntry;

        public ObservableCollection<LogEntry> LogEntries { get; }
        public ICollectionView LogEntriesView { get; }
        public ObservableCollection<string> AvailableSources { get; }
        public IThemeService ThemeService => _themeService;

        // Smart Filter: Track seen messages for deduplication
        private readonly HashSet<string> _seenMessages = new HashSet<string>();

        public ICommand StartMonitoringCommand { get; }
        public ICommand StopMonitoringCommand { get; }
        public ICommand ClearLogsCommand { get; }
        public ICommand ExportLogsCommand { get; }
        public ICommand ToggleViewModeCommand { get; }
        public ICommand ToggleThemeCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand CopySelectedCommand { get; }
        public ICommand OpenLogDirectoryCommand { get; }
        public ICommand ClearOldLogsCommand { get; }
        // New copy commands
        public ICommand CopyRawCommand { get; }
        public ICommand CopyCompactCommand { get; }
        public ICommand CopyDetailsCommand { get; }

        public MainViewModel(
            ILogMonitoringService logMonitoringService,
            ILogParsingService logParsingService,
            IThemeService themeService,
            IExportService exportService,
            IEarlyLoggingService earlyLoggingService,
            IConfiguration configuration,
            ILogger<MainViewModel> logger)
        {
            _logMonitoringService = logMonitoringService;
            _logParsingService = logParsingService;
            _themeService = themeService;
            _exportService = exportService;
            _earlyLoggingService = earlyLoggingService;
            _configuration = configuration;
            _logger = logger;

            LogEntries = new ObservableCollection<LogEntry>();
            AvailableSources = new ObservableCollection<string> { "All" };

            // Set up collection view for filtering and sorting
            LogEntriesView = CollectionViewSource.GetDefaultView(LogEntries);
            LogEntriesView.Filter = FilterLogEntry;
            LogEntriesView.SortDescriptions.Add(new SortDescription(nameof(LogEntry.Timestamp), ListSortDirection.Descending));

            // Initialize commands
            StartMonitoringCommand = new AsyncRelayCommand(StartMonitoringAsync);
            StopMonitoringCommand = new RelayCommand(StopMonitoring);
            ClearLogsCommand = new RelayCommand(ClearLogs);
            ExportLogsCommand = new AsyncRelayCommand(ExportLogsAsync);
            ToggleViewModeCommand = new RelayCommand(ToggleViewMode);
            ToggleThemeCommand = new RelayCommand(_themeService.ToggleTheme);
            RefreshCommand = new AsyncRelayCommand(RefreshLogsAsync);
            CopySelectedCommand = new RelayCommand(CopySelected, () => SelectedLogEntry != null);
            OpenLogDirectoryCommand = new RelayCommand(OpenLogDirectory);
            ClearOldLogsCommand = new RelayCommand(ClearOldLogs);
            // New: extra copy commands
            CopyRawCommand = new RelayCommand(CopyRaw, () => SelectedLogEntry != null);
            CopyCompactCommand = new RelayCommand(CopyCompact, () => SelectedLogEntry != null);
            CopyDetailsCommand = new RelayCommand(CopyDetails, () => SelectedLogEntry != null);

            // Subscribe to log monitoring events
            _logMonitoringService.LogEntryAdded += OnLogEntryAdded;
            _logMonitoringService.LogEntriesAdded += OnLogEntriesAdded; // New: batch event
            _logMonitoringService.LogsCleared += OnLogsCleared;

            // Subscribe to property changes for filtering
            PropertyChanged += OnPropertyChanged;

            // Load initial configuration
            LoadConfiguration();

            // Note: Auto-start is now handled by AutoStartMonitoring() method called from App.xaml.cs
        }

        // New: Set log directory from command line
        public void SetLogDirectory(string logDirectory)
        {
            try
            {
                if (Directory.Exists(logDirectory))
                {
                    _logger.LogInformation("Log directory set from command line: {LogDirectory}", logDirectory);
                    StatusMessage = $"Log directory set to: {logDirectory}";
                }
                else
                {
                    _logger.LogWarning("Command line log directory does not exist: {LogDirectory}", logDirectory);
                    StatusMessage = $"Warning: Log directory does not exist: {logDirectory}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting log directory from command line: {LogDirectory}", logDirectory);
                StatusMessage = $"Error setting log directory: {ex.Message}";
            }
        }

        // New: Auto-start monitoring from command line
        public void AutoStartMonitoring()
        {
            try
            {
                if (_configuration.GetValue<bool>("ErrorLogViewer:AutoStartWithGGs", true))
                {
                    _logger.LogInformation("Auto-starting monitoring from command line");
                    _ = Task.Run(StartMonitoringAsync);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error auto-starting monitoring");
                StatusMessage = $"Error auto-starting monitoring: {ex.Message}";
            }
        }

        private void LoadConfiguration()
        {
            try
            {
                AutoScroll = _configuration.GetValue<bool>("UI:AutoScroll", false);
                IsRawMode = _configuration.GetValue<bool>("UI:DefaultViewMode", false);
                // New: load regex toggle and font size
                UseRegex = _configuration.GetValue<bool>("UI:UseRegex", false);
                LogFontSize = _configuration.GetValue<double>("UI:LogFontSize", 12.0);
                var defaultLevel = _configuration.GetValue<string>("UI:DefaultLogLevel", "All");
                if (Enum.TryParse<Models.LogLevel>(defaultLevel, true, out var level))
                {
                    SelectedLogLevel = level;
                }

                _logger.LogInformation("Configuration loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load some configuration values, using defaults");
            }
        }

        private async Task StartMonitoringAsync()
        {
            try
            {
                StatusMessage = "Starting log monitoring...";
                
                var logDirectory = _configuration["Logging:DefaultDirectory"] ?? 
                                  Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GGs", "Logs");
                
                await _logMonitoringService.StartMonitoringAsync(logDirectory);
                IsMonitoring = true;
                StatusMessage = "Monitoring active";
                _logger.LogInformation("Log monitoring started");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to start monitoring: {ex.Message}";
                _logger.LogError(ex, "Failed to start log monitoring");
            }
        }

        private void StopMonitoring()
        {
            try
            {
                _logMonitoringService.StopMonitoringAsync();
                IsMonitoring = false;
                StatusMessage = "Monitoring stopped";
                _logger.LogInformation("Log monitoring stopped");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to stop monitoring: {ex.Message}";
                _logger.LogError(ex, "Failed to stop log monitoring");
            }
        }

        private void ClearLogs()
        {
            try
            {
                LogEntries.Clear();
                AvailableSources.Clear();
                AvailableSources.Add("All");
                _seenMessages.Clear(); // Clear Smart Filter cache
                UpdateCounts();
                StatusMessage = "Logs cleared";
                _logger.LogInformation("Log entries cleared");
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

        private void OnLogEntryAdded(object? sender, LogEntry logEntry)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    LogEntries.Add(logEntry);
                    
                    // Update available sources
                    if (!string.IsNullOrEmpty(logEntry.Source) && !AvailableSources.Contains(logEntry.Source))
                    {
                        AvailableSources.Add(logEntry.Source);
                    }
                    
                    UpdateCounts();
                    
                    // Auto-scroll if enabled
                    if (AutoScroll && LogEntriesView.CurrentItem != logEntry)
                    {
                        LogEntriesView.MoveCurrentTo(logEntry);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing single log entry");
            }
        }

        // New: Handle batch log entries for better performance
        private void OnLogEntriesAdded(object? sender, System.Collections.Generic.IEnumerable<LogEntry> logEntries)
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
                        
                        // Update available sources
                        if (!string.IsNullOrEmpty(entry.Source) && !AvailableSources.Contains(entry.Source))
                        {
                            AvailableSources.Add(entry.Source);
                        }
                    }
                    
                    UpdateCounts();
                    
                    // Auto-scroll to latest if enabled
                    if (AutoScroll && entriesToAdd.Any())
                    {
                        LogEntriesView.MoveCurrentTo(entriesToAdd.Last());
                    }
                    
                    _logger.LogDebug("Processed batch of {Count} log entries", entriesToAdd.Count);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing batch log entries");
            }
        }

        private void OnLogsCleared(object? sender, EventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                LogEntries.Clear();
                UpdateCounts();
            });
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
            
            // Update command can execute states
            if (e.PropertyName == nameof(SelectedLogEntry))
            {
                ((RelayCommand)CopySelectedCommand).NotifyCanExecuteChanged();
            }
        }
    }
}