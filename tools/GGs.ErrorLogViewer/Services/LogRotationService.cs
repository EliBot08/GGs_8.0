using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using GGs.ErrorLogViewer.Models;
using Microsoft.Extensions.Logging;

namespace GGs.ErrorLogViewer.Services;

/// <summary>
/// Enterprise-grade log rotation and management service
/// Prevents log bloat and ensures only relevant logs are displayed
/// </summary>
public class LogRotationService
{
    private readonly ILogger<LogRotationService> _logger;
    private const int MAX_LOG_AGE_DAYS = 7;
    private const long MAX_LOG_SIZE_BYTES = 100 * 1024 * 1024; // 100MB
    private const int MAX_ARCHIVE_AGE_DAYS = 30;
    private readonly string _currentSessionId;

    public LogRotationService(ILogger<LogRotationService> logger)
    {
        _logger = logger;
        _currentSessionId = GenerateSessionId();
    }

    /// <summary>
    /// Gets the current session ID for this run
    /// </summary>
    public string CurrentSessionId => _currentSessionId;

    /// <summary>
    /// Rotates logs on startup - archives old logs and cleans up
    /// </summary>
    public async Task RotateLogsOnStartupAsync(string logDirectory)
    {
        try
        {
            _logger.LogInformation("Starting log rotation for directory: {LogDirectory}", logDirectory);

            if (!Directory.Exists(logDirectory))
            {
                _logger.LogWarning("Log directory does not exist: {LogDirectory}", logDirectory);
                return;
            }

            // Step 1: Archive old logs
            await ArchiveOldLogsAsync(logDirectory);

            // Step 2: Compress archived logs
            await CompressArchivedLogsAsync(logDirectory);

            // Step 3: Delete very old archives
            await DeleteOldArchivesAsync(logDirectory, MAX_ARCHIVE_AGE_DAYS);

            // Step 4: Check for log size limits
            await EnforceLogSizeLimitsAsync(logDirectory);

            _logger.LogInformation("Log rotation completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rotate logs");
        }
    }

    /// <summary>
    /// Adds a session marker to identify the current run
    /// </summary>
    public string CreateSessionMarker()
    {
        return $"═══════════════════════════════════════════════════════════════\n" +
               $"  New Session Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
               $"  Session ID: {_currentSessionId}\n" +
               $"  Machine: {Environment.MachineName}\n" +
               $"  User: {Environment.UserName}\n" +
               $"═══════════════════════════════════════════════════════════════\n";
    }

    /// <summary>
    /// Deduplicates log entries based on timestamp and message
    /// </summary>
    public void DeduplicateLogs(ObservableCollection<LogEntry> logs)
    {
        try
        {
            var seen = new HashSet<string>();
            var toRemove = new List<LogEntry>();

            foreach (var log in logs)
            {
                // Create unique key from timestamp and message
                var key = $"{log.Timestamp:O}|{log.Message}|{log.Source}";
                
                if (!seen.Add(key))
                {
                    toRemove.Add(log);
                }
            }

            _logger.LogInformation("Removing {Count} duplicate log entries", toRemove.Count);

            foreach (var log in toRemove)
            {
                logs.Remove(log);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deduplicate logs");
        }
    }

    /// <summary>
    /// Filters logs to show only the current session
    /// </summary>
    public ObservableCollection<LogEntry> FilterCurrentSession(ObservableCollection<LogEntry> allLogs)
    {
        try
        {
            // Get logs from the last hour (current session)
            var sessionStart = DateTime.Now.AddHours(-1);
            var sessionLogs = allLogs.Where(log => log.Timestamp >= sessionStart).ToList();

            _logger.LogInformation("Filtered to {Count} logs from current session (since {SessionStart})", 
                sessionLogs.Count, sessionStart);

            return new ObservableCollection<LogEntry>(sessionLogs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to filter current session logs");
            return allLogs;
        }
    }

    /// <summary>
    /// Archives logs older than MAX_LOG_AGE_DAYS
    /// </summary>
    private async Task ArchiveOldLogsAsync(string logDirectory)
    {
        try
        {
            var logFiles = Directory.GetFiles(logDirectory, "*.log");
            var cutoffDate = DateTime.Now.AddDays(-MAX_LOG_AGE_DAYS);

            foreach (var logFile in logFiles)
            {
                var fileInfo = new FileInfo(logFile);
                
                if (fileInfo.LastWriteTime < cutoffDate)
                {
                    var archiveName = $"{logFile}.{DateTime.Now:yyyyMMdd}.archive";
                    
                    _logger.LogInformation("Archiving old log: {LogFile} -> {ArchiveName}", 
                        Path.GetFileName(logFile), Path.GetFileName(archiveName));
                    
                    await Task.Run(() => File.Move(logFile, archiveName, overwrite: true));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to archive old logs");
        }
    }

    /// <summary>
    /// Compresses archived log files
    /// </summary>
    private async Task CompressArchivedLogsAsync(string logDirectory)
    {
        try
        {
            var archiveFiles = Directory.GetFiles(logDirectory, "*.archive");

            foreach (var archiveFile in archiveFiles)
            {
                // Skip if already compressed
                if (File.Exists($"{archiveFile}.gz"))
                    continue;

                _logger.LogInformation("Compressing archive: {ArchiveFile}", Path.GetFileName(archiveFile));

                await Task.Run(() =>
                {
                    using var input = File.OpenRead(archiveFile);
                    using var output = File.Create($"{archiveFile}.gz");
                    using var gzip = new GZipStream(output, CompressionMode.Compress);
                    input.CopyTo(gzip);
                });

                // Delete original archive after compression
                File.Delete(archiveFile);
                _logger.LogInformation("Compressed and deleted original: {ArchiveFile}", Path.GetFileName(archiveFile));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compress archived logs");
        }
    }

    /// <summary>
    /// Deletes compressed archives older than specified days
    /// </summary>
    private async Task DeleteOldArchivesAsync(string logDirectory, int maxAgeDays)
    {
        try
        {
            var archiveFiles = Directory.GetFiles(logDirectory, "*.archive.gz");
            var cutoffDate = DateTime.Now.AddDays(-maxAgeDays);
            var deletedCount = 0;

            foreach (var archiveFile in archiveFiles)
            {
                var fileInfo = new FileInfo(archiveFile);
                
                if (fileInfo.LastWriteTime < cutoffDate)
                {
                    _logger.LogInformation("Deleting old archive: {ArchiveFile} (age: {Days} days)", 
                        Path.GetFileName(archiveFile), 
                        (DateTime.Now - fileInfo.LastWriteTime).TotalDays);
                    
                    await Task.Run(() => File.Delete(archiveFile));
                    deletedCount++;
                }
            }

            if (deletedCount > 0)
            {
                _logger.LogInformation("Deleted {Count} old archive files", deletedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete old archives");
        }
    }

    /// <summary>
    /// Enforces maximum log size limits
    /// </summary>
    private async Task EnforceLogSizeLimitsAsync(string logDirectory)
    {
        try
        {
            var logFiles = Directory.GetFiles(logDirectory, "*.log");
            var totalSize = logFiles.Sum(f => new FileInfo(f).Length);

            if (totalSize > MAX_LOG_SIZE_BYTES)
            {
                _logger.LogWarning("Total log size ({TotalSizeMB}MB) exceeds limit ({LimitMB}MB). Archiving oldest logs.",
                    totalSize / (1024 * 1024), MAX_LOG_SIZE_BYTES / (1024 * 1024));

                // Sort by last write time and archive oldest
                var sortedFiles = logFiles
                    .Select(f => new FileInfo(f))
                    .OrderBy(f => f.LastWriteTime)
                    .ToList();

                foreach (var file in sortedFiles)
                {
                    if (totalSize <= MAX_LOG_SIZE_BYTES)
                        break;

                    var archiveName = $"{file.FullName}.{DateTime.Now:yyyyMMdd}.archive";
                    
                    _logger.LogInformation("Archiving to enforce size limit: {LogFile}", file.Name);
                    
                    await Task.Run(() => File.Move(file.FullName, archiveName, overwrite: true));
                    totalSize -= file.Length;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enforce log size limits");
        }
    }

    /// <summary>
    /// Generates a unique session ID
    /// </summary>
    private static string GenerateSessionId()
    {
        return $"{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}".Substring(0, 24);
    }

    /// <summary>
    /// Gets statistics about the log directory
    /// </summary>
    public LogDirectoryStats GetDirectoryStats(string logDirectory)
    {
        try
        {
            if (!Directory.Exists(logDirectory))
            {
                return new LogDirectoryStats();
            }

            var logFiles = Directory.GetFiles(logDirectory, "*.log");
            var archiveFiles = Directory.GetFiles(logDirectory, "*.archive*");

            var stats = new LogDirectoryStats
            {
                ActiveLogCount = logFiles.Length,
                ArchiveCount = archiveFiles.Length,
                TotalLogSize = logFiles.Sum(f => new FileInfo(f).Length),
                TotalArchiveSize = archiveFiles.Sum(f => new FileInfo(f).Length),
                OldestLogDate = logFiles.Length > 0 
                    ? logFiles.Select(f => new FileInfo(f).LastWriteTime).Min() 
                    : DateTime.MinValue,
                NewestLogDate = logFiles.Length > 0 
                    ? logFiles.Select(f => new FileInfo(f).LastWriteTime).Max() 
                    : DateTime.MinValue
            };

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get directory stats");
            return new LogDirectoryStats();
        }
    }

    /// <summary>
    /// Clears all logs (with confirmation)
    /// </summary>
    public async Task<bool> ClearAllLogsAsync(string logDirectory, bool includeArchives = false)
    {
        try
        {
            _logger.LogWarning("Clearing all logs from directory: {LogDirectory} (Include Archives: {IncludeArchives})", 
                logDirectory, includeArchives);

            var logFiles = Directory.GetFiles(logDirectory, "*.log");
            
            foreach (var logFile in logFiles)
            {
                await Task.Run(() => File.Delete(logFile));
            }

            _logger.LogInformation("Deleted {Count} log files", logFiles.Length);

            if (includeArchives)
            {
                var archiveFiles = Directory.GetFiles(logDirectory, "*.archive*");
                
                foreach (var archiveFile in archiveFiles)
                {
                    await Task.Run(() => File.Delete(archiveFile));
                }

                _logger.LogInformation("Deleted {Count} archive files", archiveFiles.Length);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear logs");
            return false;
        }
    }
}

/// <summary>
/// Statistics about the log directory
/// </summary>
public class LogDirectoryStats
{
    public int ActiveLogCount { get; set; }
    public int ArchiveCount { get; set; }
    public long TotalLogSize { get; set; }
    public long TotalArchiveSize { get; set; }
    public DateTime OldestLogDate { get; set; }
    public DateTime NewestLogDate { get; set; }

    public string TotalLogSizeMB => $"{TotalLogSize / (1024.0 * 1024.0):F2} MB";
    public string TotalArchiveSizeMB => $"{TotalArchiveSize / (1024.0 * 1024.0):F2} MB";
    public int LogAgeDays => OldestLogDate != DateTime.MinValue 
        ? (DateTime.Now - OldestLogDate).Days 
        : 0;
}
