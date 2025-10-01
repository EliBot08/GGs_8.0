#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using GGs.ErrorLogViewer.Models;
using Microsoft.Extensions.Logging;

namespace GGs.ErrorLogViewer.Services
{
    public interface ILogCachingService
    {
        void AddToCache(LogEntry entry);
        void AddRangeToCache(IEnumerable<LogEntry> entries);
        IEnumerable<LogEntry> GetCachedLogs(int skip, int take);
        IEnumerable<LogEntry> GetAllCached();
        void ClearCache();
        int Count { get; }
    }

    public class LogCachingService : ILogCachingService
    {
        private readonly ConcurrentDictionary<long, LogEntry> _cache = new();
        private readonly ILogger<LogCachingService> _logger;
        private readonly object _lock = new();

        public int Count => _cache.Count;

        public LogCachingService(ILogger<LogCachingService> logger)
        {
            _logger = logger;
        }

        public void AddToCache(LogEntry entry)
        {
            if (entry == null) return;
            
            _cache.AddOrUpdate(entry.Id, entry, (key, oldValue) => entry);
        }

        public void AddRangeToCache(IEnumerable<LogEntry> entries)
        {
            if (entries == null) return;

            foreach (var entry in entries)
            {
                AddToCache(entry);
            }
            
            _logger.LogDebug("Added {Count} entries to cache, total cached: {Total}", 
                entries.Count(), _cache.Count);
        }

        public IEnumerable<LogEntry> GetCachedLogs(int skip, int take)
        {
            return _cache.Values
                .OrderByDescending(e => e.Timestamp)
                .Skip(skip)
                .Take(take);
        }

        public IEnumerable<LogEntry> GetAllCached()
        {
            return _cache.Values.OrderByDescending(e => e.Timestamp);
        }

        public void ClearCache()
        {
            var count = _cache.Count;
            _cache.Clear();
            _logger.LogInformation("Cleared {Count} entries from cache", count);
        }
    }
}
