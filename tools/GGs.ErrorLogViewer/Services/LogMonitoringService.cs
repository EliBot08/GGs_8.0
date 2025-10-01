using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using GGs.ErrorLogViewer.Models;

namespace GGs.ErrorLogViewer.Services
{
    public interface ILogMonitoringService
    {
        event EventHandler<LogEntry> LogEntryAdded;
        event EventHandler<IEnumerable<LogEntry>> LogEntriesAdded;
        event EventHandler LogsCleared;

        Task StartMonitoringAsync(string logDirectory, CancellationToken cancellationToken = default);
        Task StopMonitoringAsync();
        IEnumerable<LogEntry> GetAllEntries();
        void ClearLogs();
        Task<IEnumerable<LogEntry>> LoadHistoricalLogsAsync(string directory, DateTime? since = null);
    }

    public class LogMonitoringService : BackgroundService, ILogMonitoringService
    {
        private readonly ILogger<LogMonitoringService> _logger;
        private readonly ILogParsingService _parsingService;
        private readonly IConfiguration _configuration;

        private readonly ConcurrentQueue<LogEntry> _logEntries = new();
        private readonly Dictionary<string, FileWatcher> _fileWatchers = new();
        private readonly Dictionary<string, long> _filePositions = new();
        private readonly ConcurrentDictionary<string, DateTime> _seenLogHashes = new(); // Prevent duplicate entries
        private readonly object _lockObject = new();

        private string _logDirectory = string.Empty;
        private Timer? _pollingTimer;
        private long _nextLogId = 1;
        private int _maxEntries;
        private int _refreshInterval;
        private readonly int _logRetentionDays;
        private readonly int _loadHistoricalLogsFromHours;
        private readonly bool _deleteOldLogFilesOnStartup;
        private readonly int _oldLogFileThresholdHours;
        private readonly string _seenHashesFilePath;

        public event EventHandler<LogEntry>? LogEntryAdded;
        public event EventHandler<IEnumerable<LogEntry>>? LogEntriesAdded;
        public event EventHandler? LogsCleared;

        public LogMonitoringService(
            ILogger<LogMonitoringService> logger,
            ILogParsingService parsingService,
            IConfiguration configuration)
        {
            _logger = logger;
            _parsingService = parsingService;
            _configuration = configuration;

            _maxEntries = _configuration.GetValue<int>("ErrorLogViewer:MaxLogEntries", 5000);
            _refreshInterval = _configuration.GetValue<int>("ErrorLogViewer:RefreshIntervalMs", 1000);
            _logRetentionDays = _configuration.GetValue<int>("ErrorLogViewer:LogRetentionDays", 7);
            _loadHistoricalLogsFromHours = _configuration.GetValue<int>("ErrorLogViewer:LoadHistoricalLogsFromHours", 1);
            _deleteOldLogFilesOnStartup = _configuration.GetValue<bool>("ErrorLogViewer:DeleteOldLogFilesOnStartup", true);
            _oldLogFileThresholdHours = _configuration.GetValue<int>("ErrorLogViewer:OldLogFileThresholdHours", 24);
            _seenHashesFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GGs", "seenHashes.json");

            LoadSeenHashes();
        }

        public async Task StartMonitoringAsync(string logDirectory, CancellationToken cancellationToken = default)
        {
            _logDirectory = logDirectory;

            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
                _logger.LogInformation("Created log directory: {LogDirectory}", _logDirectory);
            }

            if (_configuration.GetValue<bool>("ErrorLogViewer:ClearLogsOnStartup"))
            {
                ClearLogDirectory();
            }

            if (_deleteOldLogFilesOnStartup)
            {
                DeleteOldLogFiles();
            }

            _logger.LogInformation("Starting log monitoring for directory: {LogDirectory}", _logDirectory);

            // Load existing logs first
            await LoadExistingLogsAsync(cancellationToken);

            // Set up file system watchers
            SetupFileWatchers();

            // Start polling timer for active files
            _pollingTimer = new Timer(PollActiveFiles, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(_refreshInterval));

            _logger.LogInformation("Log monitoring started successfully");
        }

        public async Task StopMonitoringAsync()
        {
            _logger.LogInformation("Stopping log monitoring...");

            _pollingTimer?.Dispose();
            _pollingTimer = null;

            lock (_lockObject)
            {
                foreach (var watcher in _fileWatchers.Values)
                {
                    watcher.Dispose();
                }
                _fileWatchers.Clear();
            }

            SaveSeenHashes();

            _logger.LogInformation("Log monitoring stopped");
        }

        private void ClearLogDirectory()
        {
            try
            {
                var logFiles = GetLogFiles(_logDirectory);
                int clearedFileCount = 0;
                foreach (var file in logFiles)
                {
                    try
                    {
                        File.Delete(file);
                        clearedFileCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not delete log file: {File}", file);
                    }
                }
                if (clearedFileCount > 0)
                {
                    _logger.LogInformation("Cleared {Count} log files from the log directory.", clearedFileCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing log directory.");
            }
        }

        private void DeleteOldLogFiles()
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.AddHours(-_oldLogFileThresholdHours);
                var logFiles = GetLogFiles(_logDirectory);
                int deletedCount = 0;
                long deletedBytes = 0;

                _logger.LogInformation("Scanning for log files older than {Hours} hours (before {CutoffTime})...", _oldLogFileThresholdHours, cutoffTime);

                foreach (var file in logFiles)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.LastWriteTimeUtc < cutoffTime)
                        {
                            deletedBytes += fileInfo.Length;
                            File.Delete(file);
                            deletedCount++;
                            _logger.LogDebug("Deleted old log file: {File} (last modified: {LastWrite}, size: {Size} bytes)", 
                                file, fileInfo.LastWriteTimeUtc, fileInfo.Length);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not delete old log file: {File}", file);
                    }
                }

                if (deletedCount > 0)
                {
                    _logger.LogInformation("Deleted {Count} old log files, freed {SizeMB:F2} MB of disk space", 
                        deletedCount, deletedBytes / 1024.0 / 1024.0);
                }
                else
                {
                    _logger.LogInformation("No old log files found to delete");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting old log files");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // This service is manually started via StartMonitoringAsync
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }

        private async Task LoadExistingLogsAsync(CancellationToken cancellationToken)
        {
            try
            {
                var logFiles = GetLogFiles(_logDirectory);
                var allEntries = new List<LogEntry>();
                var cutoffTime = DateTime.UtcNow.AddHours(-_loadHistoricalLogsFromHours);

                _logger.LogInformation("Loading logs from the last {Hours} hours (since {CutoffTime})", _loadHistoricalLogsFromHours, cutoffTime);

                foreach (var file in logFiles.OrderBy(f => File.GetCreationTime(f)))
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    // Skip files older than the cutoff
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.LastWriteTimeUtc < cutoffTime)
                    {
                        _logger.LogDebug("Skipping old log file: {File} (last modified: {LastWrite})", file, fileInfo.LastWriteTimeUtc);
                        continue;
                    }

                    var entries = await LoadLogFileAsync(file, cancellationToken, cutoffTime);
                    allEntries.AddRange(entries);
                }

                // Sort by timestamp and take most recent entries
                var sortedEntries = allEntries
                    .Where(e => e.Timestamp >= cutoffTime)
                    .OrderBy(e => e.Timestamp)
                    .TakeLast(_maxEntries)
                    .ToList();

                foreach (var entry in sortedEntries)
                {
                    _logEntries.Enqueue(entry);
                }

                if (sortedEntries.Any())
                {
                    LogEntriesAdded?.Invoke(this, sortedEntries);
                    _logger.LogInformation("Loaded {Count} existing log entries", sortedEntries.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading existing logs");
            }
        }

        private async Task<IEnumerable<LogEntry>> LoadLogFileAsync(string filePath, CancellationToken cancellationToken, DateTime? cutoffTime = null)
        {
            var entries = new List<LogEntry>();

            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                // Handle compressed files
                Stream readStream = stream;
                if (filePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
                {
                    readStream = new GZipStream(stream, CompressionMode.Decompress);
                }

                using var reader = new StreamReader(readStream);
                string? line;
                long lineNumber = 0;

                while ((line = await reader.ReadLineAsync()) != null && !cancellationToken.IsCancellationRequested)
                {
                    lineNumber++;

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var entry = _parsingService.ParseLogLine(line, filePath, lineNumber);
                    if (entry != null)
                    {
                        // Filter by cutoff time if specified
                        if (cutoffTime.HasValue && entry.Timestamp < cutoffTime.Value)
                        {
                            continue;
                        }

                        entry.Id = Interlocked.Increment(ref _nextLogId);
                        entries.Add(entry);
                    }
                }

                // Update file position for future monitoring
                _filePositions[filePath] = stream.Length;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading log file: {FilePath}", filePath);
            }

            return entries;
        }

        private void SetupFileWatchers()
        {
            try
            {
                var watcher = new FileSystemWatcher(_logDirectory)
                {
                    Filter = "*.*",
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
                };

                watcher.Created += OnFileCreated;
                watcher.Changed += OnFileChanged;
                watcher.Renamed += OnFileRenamed;
                watcher.Error += OnWatcherError;

                watcher.EnableRaisingEvents = true;

                lock (_lockObject)
                {
                    _fileWatchers["main"] = new FileWatcher { Watcher = watcher };
                }

                _logger.LogDebug("File system watcher setup for: {Directory}", _logDirectory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting up file system watcher");
            }
        }

        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            if (IsLogFile(e.FullPath))
            {
                _logger.LogDebug("New log file detected: {FilePath}", e.FullPath);
                _ = Task.Run(async () => await ProcessNewFileAsync(e.FullPath));
            }
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (IsLogFile(e.FullPath))
            {
                // File changes are handled by polling timer for better performance
            }
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            if (IsLogFile(e.FullPath))
            {
                _logger.LogDebug("Log file renamed: {OldPath} -> {NewPath}", e.OldFullPath, e.FullPath);

                lock (_lockObject)
                {
                    if (_filePositions.ContainsKey(e.OldFullPath))
                    {
                        _filePositions[e.FullPath] = _filePositions[e.OldFullPath];
                        _filePositions.Remove(e.OldFullPath);
                    }
                }
            }
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            _logger.LogError(e.GetException(), "File system watcher error");
        }

        private async void PollActiveFiles(object? state)
        {
            try
            {
                var logFiles = GetLogFiles(_logDirectory);
                var tasks = logFiles.Select(ProcessFileChangesAsync).ToArray();
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during file polling");
            }
        }

        private async Task ProcessFileChangesAsync(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists)
                    return;

                var currentPosition = _filePositions.GetValueOrDefault(filePath, 0);

                if (fileInfo.Length <= currentPosition)
                    return; // No new content

                var newEntries = new List<LogEntry>();

                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                stream.Seek(currentPosition, SeekOrigin.Begin);

                using var reader = new StreamReader(stream);
                string? line;
                long lineNumber = currentPosition == 0 ? 0 : -1; // Approximate line number

                while ((line = await reader.ReadLineAsync()) != null)
                {
                    lineNumber++;

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var entry = _parsingService.ParseLogLine(line, filePath, lineNumber);
                    if (entry != null)
                    {
                        // Create a hash to detect duplicates
                        var logHash = $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}|{entry.Level}|{entry.Source}|{entry.Message}";

                        if (!_seenLogHashes.ContainsKey(logHash))
                        {
                            if (_seenLogHashes.TryAdd(logHash, entry.Timestamp))
                            {
                                entry.Id = Interlocked.Increment(ref _nextLogId);
                                newEntries.Add(entry);
                                _logEntries.Enqueue(entry);
                            }
                        }
                    }
                }

                _filePositions[filePath] = stream.Position;

                if (newEntries.Any())
                {
                    // Maintain max entries limit
                    while (_logEntries.Count > _maxEntries)
                    {
                        _logEntries.TryDequeue(out _);
                    }

                    LogEntriesAdded?.Invoke(this, newEntries);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing file changes: {FilePath}", filePath);
            }
        }

        private async Task ProcessNewFileAsync(string filePath)
        {
            // Wait a bit for the file to be fully created
            await Task.Delay(100);

            var entries = await LoadLogFileAsync(filePath, CancellationToken.None, null);
            var entryList = entries.ToList();

            if (entryList.Any())
            {
                foreach (var entry in entryList)
                {
                    _logEntries.Enqueue(entry);
                }

                LogEntriesAdded?.Invoke(this, entryList);
            }
        }

        private static bool IsLogFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension is ".log" or ".jsonl" or ".txt" or ".gz";
        }

        private static IEnumerable<string> GetLogFiles(string directory)
        {
            if (!Directory.Exists(directory))
                return Enumerable.Empty<string>();

            return Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
                .Where(IsLogFile);
        }

        public IEnumerable<LogEntry> GetAllEntries()
        {
            return _logEntries.ToArray();
        }

        public void ClearLogs()
        {
            while (_logEntries.TryDequeue(out _)) { }
            LogsCleared?.Invoke(this, EventArgs.Empty);
            _logger.LogInformation("Log entries cleared");
        }

        public async Task<IEnumerable<LogEntry>> LoadHistoricalLogsAsync(string directory, DateTime? since = null)
        {
            var entries = new List<LogEntry>();
            var logFiles = GetLogFiles(directory);

            foreach (var file in logFiles)
            {
                var fileEntries = await LoadLogFileAsync(file, CancellationToken.None, since);
                if (since.HasValue)
                {
                    fileEntries = fileEntries.Where(e => e.Timestamp >= since.Value);
                }
                entries.AddRange(fileEntries);
            }

            return entries.OrderBy(e => e.Timestamp);
        }

        public override void Dispose()
        {
            _pollingTimer?.Dispose();

            lock (_lockObject)
            {
                foreach (var watcher in _fileWatchers.Values)
                {
                    watcher.Dispose();
                }
                _fileWatchers.Clear();
            }

            SaveSeenHashes();

            base.Dispose();
        }

        private void LoadSeenHashes()
        {
            try
            {
                if (File.Exists(_seenHashesFilePath))
                {
                    var jsonData = File.ReadAllText(_seenHashesFilePath);
                    var hashes = JsonSerializer.Deserialize<Dictionary<string, DateTime>>(jsonData);
                    if (hashes != null)
                    {
                        var cutoff = DateTime.UtcNow.AddDays(-_logRetentionDays);
                        foreach (var item in hashes)
                        {
                            if (item.Value > cutoff)
                            {
                                _seenLogHashes.TryAdd(item.Key, item.Value);
                            }
                        }
                        _logger.LogInformation("Loaded {Count} recent log hashes.", _seenLogHashes.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading seen hashes");
            }
        }

        private void SaveSeenHashes()
        {
            try
            {
                var hashesToSave = new Dictionary<string, DateTime>();
                var cutoff = DateTime.UtcNow.AddDays(-_logRetentionDays);

                foreach (var item in _seenLogHashes)
                {
                    if (item.Value > cutoff)
                    {
                        hashesToSave.Add(item.Key, item.Value);
                    }
                }

                var jsonData = JsonSerializer.Serialize(hashesToSave);
                File.WriteAllText(_seenHashesFilePath, jsonData);
                _logger.LogInformation("Saved {Count} log hashes.", hashesToSave.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving seen hashes");
            }
        }

        private class FileWatcher : IDisposable
        {
            public FileSystemWatcher? Watcher { get; set; }

            public void Dispose()
            {
                Watcher?.Dispose();
            }
        }
    }
}
