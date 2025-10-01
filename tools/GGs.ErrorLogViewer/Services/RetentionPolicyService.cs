#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GGs.ErrorLogViewer.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GGs.ErrorLogViewer.Services
{
    public interface IRetentionPolicyService
    {
        RetentionPolicy Policy { get; set; }
        Task<int> CleanupOldLogsAsync(string logDirectory, bool requireConfirmation = true);
        Task<long> CompressOldLogsAsync(string logDirectory);
        Task<Dictionary<string, long>> GetLogStatisticsAsync(string logDirectory);
        void StartAutoCleanup(string logDirectory);
        void StopAutoCleanup();
    }

    public class RetentionPolicyService : BackgroundService, IRetentionPolicyService
    {
        private readonly ILogger<RetentionPolicyService> _logger;
        private Timer? _cleanupTimer;
        private string? _monitoredDirectory;

        public RetentionPolicy Policy { get; set; } = new RetentionPolicy
        {
            IsEnabled = true,
            RetentionDays = 30,
            AutoClean = false,
            RequireConfirmation = true,
            MaxLogSizeMB = 1000,
            CompressOldLogs = true,
            CompressAfterDays = 7
        };

        public RetentionPolicyService(ILogger<RetentionPolicyService> logger)
        {
            _logger = logger;
        }

        public async Task<int> CleanupOldLogsAsync(string logDirectory, bool requireConfirmation = true)
        {
            if (!Policy.IsEnabled)
            {
                _logger.LogInformation("Retention policy is disabled, skipping cleanup");
                return 0;
            }

            try
            {
                if (!Directory.Exists(logDirectory))
                {
                    _logger.LogWarning("Log directory does not exist: {Directory}", logDirectory);
                    return 0;
                }

                var cutoffDate = DateTime.Now.AddDays(-Policy.RetentionDays);
                var logFiles = Directory.GetFiles(logDirectory, "*.log", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(logDirectory, "*.json", SearchOption.AllDirectories))
                    .ToList();

                var filesToDelete = logFiles
                    .Where(f => File.GetLastWriteTime(f) < cutoffDate)
                    .ToList();

                if (!filesToDelete.Any())
                {
                    _logger.LogInformation("No old log files found for cleanup");
                    return 0;
                }

                _logger.LogInformation("Found {Count} old log files to delete (older than {Days} days)",
                    filesToDelete.Count, Policy.RetentionDays);

                if (requireConfirmation && Policy.RequireConfirmation)
                {
                    _logger.LogWarning("Confirmation required - skipping actual deletion. Set requireConfirmation=false to proceed.");
                    return 0;
                }

                int deletedCount = 0;
                foreach (var file in filesToDelete)
                {
                    try
                    {
                        await Task.Run(() => File.Delete(file));
                        deletedCount++;
                        _logger.LogDebug("Deleted old log file: {File}", file);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to delete log file: {File}", file);
                    }
                }

                Policy.LastCleanup = DateTime.UtcNow;
                _logger.LogInformation("Cleanup complete: {DeletedCount} files deleted", deletedCount);

                return deletedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during log cleanup in directory: {Directory}", logDirectory);
                return 0;
            }
        }

        public async Task<long> CompressOldLogsAsync(string logDirectory)
        {
            if (!Policy.CompressOldLogs)
            {
                _logger.LogInformation("Log compression is disabled");
                return 0;
            }

            try
            {
                if (!Directory.Exists(logDirectory))
                {
                    _logger.LogWarning("Log directory does not exist: {Directory}", logDirectory);
                    return 0;
                }

                var cutoffDate = DateTime.Now.AddDays(-Policy.CompressAfterDays);
                var logFiles = Directory.GetFiles(logDirectory, "*.log", SearchOption.AllDirectories)
                    .Where(f => !f.EndsWith(".gz"))
                    .Where(f => File.GetLastWriteTime(f) < cutoffDate)
                    .ToList();

                long totalCompressed = 0;

                foreach (var file in logFiles)
                {
                    try
                    {
                        var compressedFile = file + ".gz";
                        
                        if (File.Exists(compressedFile))
                        {
                            continue; // Already compressed
                        }

                        var originalSize = new FileInfo(file).Length;

                        await Task.Run(() =>
                        {
                            using var originalStream = File.OpenRead(file);
                            using var compressedStream = File.Create(compressedFile);
                            using var gzipStream = new GZipStream(compressedStream, CompressionLevel.Optimal);
                            originalStream.CopyTo(gzipStream);
                        });

                        var compressedSize = new FileInfo(compressedFile).Length;
                        var savedSpace = originalSize - compressedSize;
                        totalCompressed += savedSpace;

                        // Delete original file after successful compression
                        File.Delete(file);

                        _logger.LogInformation("Compressed {File}: {Original} -> {Compressed} (saved {Saved} bytes)",
                            Path.GetFileName(file), originalSize, compressedSize, savedSpace);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to compress log file: {File}", file);
                    }
                }

                _logger.LogInformation("Compression complete: {TotalSaved} bytes saved", totalCompressed);
                return totalCompressed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during log compression in directory: {Directory}", logDirectory);
                return 0;
            }
        }

        public async Task<Dictionary<string, long>> GetLogStatisticsAsync(string logDirectory)
        {
            var stats = new Dictionary<string, long>();

            try
            {
                if (!Directory.Exists(logDirectory))
                {
                    return stats;
                }

                await Task.Run(() =>
                {
                    var logFiles = Directory.GetFiles(logDirectory, "*.*", SearchOption.AllDirectories)
                        .Where(f => f.EndsWith(".log") || f.EndsWith(".json") || f.EndsWith(".gz"))
                        .ToList();

                    stats["TotalFiles"] = logFiles.Count;
                    stats["TotalSizeBytes"] = logFiles.Sum(f => new FileInfo(f).Length);
                    stats["CompressedFiles"] = logFiles.Count(f => f.EndsWith(".gz"));
                    stats["LogFiles"] = logFiles.Count(f => f.EndsWith(".log") && !f.EndsWith(".gz"));
                    stats["JsonFiles"] = logFiles.Count(f => f.EndsWith(".json"));

                    var cutoffDate = DateTime.Now.AddDays(-Policy.RetentionDays);
                    stats["OldFiles"] = logFiles.Count(f => File.GetLastWriteTime(f) < cutoffDate);

                    var compressCutoff = DateTime.Now.AddDays(-Policy.CompressAfterDays);
                    stats["UncompressedOldFiles"] = logFiles.Count(f => 
                        !f.EndsWith(".gz") && 
                        f.EndsWith(".log") && 
                        File.GetLastWriteTime(f) < compressCutoff);
                });

                _logger.LogInformation("Log statistics: {TotalFiles} files, {TotalSizeMB} MB total",
                    stats["TotalFiles"], stats["TotalSizeBytes"] / 1024 / 1024);

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting log statistics for directory: {Directory}", logDirectory);
                return stats;
            }
        }

        public void StartAutoCleanup(string logDirectory)
        {
            if (!Policy.AutoClean)
            {
                _logger.LogInformation("Auto-cleanup is disabled in policy");
                return;
            }

            _monitoredDirectory = logDirectory;
            
            // Run cleanup daily at 2 AM
            var now = DateTime.Now;
            var nextRun = now.Date.AddDays(1).AddHours(2);
            var initialDelay = nextRun - now;

            _cleanupTimer = new Timer(
                AutoCleanupCallback,
                null,
                initialDelay,
                TimeSpan.FromDays(1));

            _logger.LogInformation("Auto-cleanup started for directory: {Directory}, next run at {NextRun}",
                logDirectory, nextRun);
        }

        public void StopAutoCleanup()
        {
            _cleanupTimer?.Dispose();
            _cleanupTimer = null;
            _logger.LogInformation("Auto-cleanup stopped");
        }

        private async void AutoCleanupCallback(object? state)
        {
            if (string.IsNullOrEmpty(_monitoredDirectory))
                return;

            try
            {
                _logger.LogInformation("Running automatic cleanup...");
                
                // Compress old logs first
                await CompressOldLogsAsync(_monitoredDirectory);
                
                // Then cleanup very old logs
                await CleanupOldLogsAsync(_monitoredDirectory, requireConfirmation: false);
                
                _logger.LogInformation("Automatic cleanup completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during automatic cleanup");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("RetentionPolicyService background service started");
            
            // Wait for cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        public override void Dispose()
        {
            StopAutoCleanup();
            base.Dispose();
        }
    }
}
