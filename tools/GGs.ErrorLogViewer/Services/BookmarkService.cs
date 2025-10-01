#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using GGs.ErrorLogViewer.Models;
using Microsoft.Extensions.Logging;

namespace GGs.ErrorLogViewer.Services
{
    public interface IBookmarkService
    {
        ObservableCollection<LogBookmark> Bookmarks { get; }
        ObservableCollection<LogTag> Tags { get; }
        
        event EventHandler<LogBookmark>? BookmarkAdded;
        event EventHandler<LogBookmark>? BookmarkRemoved;
        event EventHandler<LogTag>? TagAdded;
        event EventHandler<LogTag>? TagRemoved;
        
        // Bookmark operations
        LogBookmark AddBookmark(long logEntryId, string name, string? notes = null, string? color = null);
        void RemoveBookmark(string bookmarkId);
        void UpdateBookmark(LogBookmark bookmark);
        LogBookmark? GetBookmark(string bookmarkId);
        List<LogBookmark> GetBookmarksForEntry(long logEntryId);
        
        // Tag operations
        LogTag AddTag(string name, string? color = null, string? icon = null);
        void RemoveTag(string tagId);
        void UpdateTag(LogTag tag);
        LogTag? GetTag(string tagId);
        
        // Tag assignment
        void AssignTagToEntry(long logEntryId, string tagId);
        void RemoveTagFromEntry(long logEntryId, string tagId);
        List<LogTag> GetTagsForEntry(long logEntryId);
        List<long> GetEntriesForTag(string tagId);
        
        // Persistence
        void SaveToFile(string filePath);
        void LoadFromFile(string filePath);
    }

    public class BookmarkService : IBookmarkService
    {
        private readonly ILogger<BookmarkService> _logger;
        private readonly Dictionary<long, List<string>> _entryToTags = new();
        private readonly Dictionary<string, List<long>> _tagToEntries = new();

        public ObservableCollection<LogBookmark> Bookmarks { get; } = new();
        public ObservableCollection<LogTag> Tags { get; } = new();

        public event EventHandler<LogBookmark>? BookmarkAdded;
        public event EventHandler<LogBookmark>? BookmarkRemoved;
        public event EventHandler<LogTag>? TagAdded;
        public event EventHandler<LogTag>? TagRemoved;

        public BookmarkService(ILogger<BookmarkService> logger)
        {
            _logger = logger;
            InitializeDefaultTags();
        }

        private void InitializeDefaultTags()
        {
            // Create some useful default tags
            AddTag("Important", "#FF4444", "⭐");
            AddTag("To Investigate", "#FFA500", "🔍");
            AddTag("Fixed", "#44FF44", "✅");
            AddTag("Known Issue", "#FFFF00", "⚠️");
            AddTag("Critical", "#FF0000", "💥");
        }

        public LogBookmark AddBookmark(long logEntryId, string name, string? notes = null, string? color = null)
        {
            var bookmark = new LogBookmark
            {
                LogEntryId = logEntryId,
                Name = name,
                Notes = notes,
                Color = color ?? "#007ACC"
            };

            Bookmarks.Add(bookmark);
            BookmarkAdded?.Invoke(this, bookmark);
            
            _logger.LogInformation("Added bookmark '{Name}' for log entry {LogEntryId}", name, logEntryId);
            return bookmark;
        }

        public void RemoveBookmark(string bookmarkId)
        {
            var bookmark = Bookmarks.FirstOrDefault(b => b.Id == bookmarkId);
            if (bookmark != null)
            {
                Bookmarks.Remove(bookmark);
                BookmarkRemoved?.Invoke(this, bookmark);
                _logger.LogInformation("Removed bookmark '{Name}'", bookmark.Name);
            }
        }

        public void UpdateBookmark(LogBookmark bookmark)
        {
            var existing = Bookmarks.FirstOrDefault(b => b.Id == bookmark.Id);
            if (existing != null)
            {
                var index = Bookmarks.IndexOf(existing);
                Bookmarks[index] = bookmark;
                _logger.LogDebug("Updated bookmark '{Name}'", bookmark.Name);
            }
        }

        public LogBookmark? GetBookmark(string bookmarkId)
        {
            return Bookmarks.FirstOrDefault(b => b.Id == bookmarkId);
        }

        public List<LogBookmark> GetBookmarksForEntry(long logEntryId)
        {
            return Bookmarks.Where(b => b.LogEntryId == logEntryId).ToList();
        }

        public LogTag AddTag(string name, string? color = null, string? icon = null)
        {
            // Check if tag with same name already exists
            var existing = Tags.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                return existing;
            }

            var tag = new LogTag
            {
                Name = name,
                Color = color ?? "#007ACC",
                Icon = icon
            };

            Tags.Add(tag);
            _tagToEntries[tag.Id] = new List<long>();
            TagAdded?.Invoke(this, tag);
            
            _logger.LogInformation("Added tag '{Name}'", name);
            return tag;
        }

        public void RemoveTag(string tagId)
        {
            var tag = Tags.FirstOrDefault(t => t.Id == tagId);
            if (tag != null)
            {
                Tags.Remove(tag);
                
                // Remove all associations
                if (_tagToEntries.ContainsKey(tagId))
                {
                    var entries = _tagToEntries[tagId].ToList();
                    foreach (var entryId in entries)
                    {
                        RemoveTagFromEntry(entryId, tagId);
                    }
                    _tagToEntries.Remove(tagId);
                }
                
                TagRemoved?.Invoke(this, tag);
                _logger.LogInformation("Removed tag '{Name}'", tag.Name);
            }
        }

        public void UpdateTag(LogTag tag)
        {
            var existing = Tags.FirstOrDefault(t => t.Id == tag.Id);
            if (existing != null)
            {
                var index = Tags.IndexOf(existing);
                Tags[index] = tag;
                _logger.LogDebug("Updated tag '{Name}'", tag.Name);
            }
        }

        public LogTag? GetTag(string tagId)
        {
            return Tags.FirstOrDefault(t => t.Id == tagId);
        }

        public void AssignTagToEntry(long logEntryId, string tagId)
        {
            if (!_entryToTags.ContainsKey(logEntryId))
            {
                _entryToTags[logEntryId] = new List<string>();
            }

            if (!_entryToTags[logEntryId].Contains(tagId))
            {
                _entryToTags[logEntryId].Add(tagId);
            }

            if (!_tagToEntries.ContainsKey(tagId))
            {
                _tagToEntries[tagId] = new List<long>();
            }

            if (!_tagToEntries[tagId].Contains(logEntryId))
            {
                _tagToEntries[tagId].Add(logEntryId);
            }

            _logger.LogDebug("Assigned tag {TagId} to entry {LogEntryId}", tagId, logEntryId);
        }

        public void RemoveTagFromEntry(long logEntryId, string tagId)
        {
            if (_entryToTags.ContainsKey(logEntryId))
            {
                _entryToTags[logEntryId].Remove(tagId);
                if (_entryToTags[logEntryId].Count == 0)
                {
                    _entryToTags.Remove(logEntryId);
                }
            }

            if (_tagToEntries.ContainsKey(tagId))
            {
                _tagToEntries[tagId].Remove(logEntryId);
            }

            _logger.LogDebug("Removed tag {TagId} from entry {LogEntryId}", tagId, logEntryId);
        }

        public List<LogTag> GetTagsForEntry(long logEntryId)
        {
            if (_entryToTags.ContainsKey(logEntryId))
            {
                return _entryToTags[logEntryId]
                    .Select(tagId => GetTag(tagId))
                    .Where(tag => tag != null)
                    .Select(tag => tag!)
                    .ToList();
            }
            return new List<LogTag>();
        }

        public List<long> GetEntriesForTag(string tagId)
        {
            return _tagToEntries.ContainsKey(tagId) ? _tagToEntries[tagId].ToList() : new List<long>();
        }

        public void SaveToFile(string filePath)
        {
            try
            {
                var data = new
                {
                    Bookmarks = Bookmarks.ToList(),
                    Tags = Tags.ToList(),
                    EntryToTags = _entryToTags.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value)
                };

                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
                
                _logger.LogInformation("Saved {BookmarkCount} bookmarks and {TagCount} tags to {FilePath}", 
                    Bookmarks.Count, Tags.Count, filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save bookmarks and tags to {FilePath}", filePath);
            }
        }

        public void LoadFromFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("Bookmark file {FilePath} not found", filePath);
                    return;
                }

                var json = File.ReadAllText(filePath);
                var data = JsonSerializer.Deserialize<BookmarkData>(json);

                if (data != null)
                {
                    Bookmarks.Clear();
                    Tags.Clear();
                    _entryToTags.Clear();
                    _tagToEntries.Clear();

                    foreach (var bookmark in data.Bookmarks ?? new List<LogBookmark>())
                    {
                        Bookmarks.Add(bookmark);
                    }

                    foreach (var tag in data.Tags ?? new List<LogTag>())
                    {
                        Tags.Add(tag);
                        _tagToEntries[tag.Id] = new List<long>();
                    }

                    if (data.EntryToTags != null)
                    {
                        foreach (var kvp in data.EntryToTags)
                        {
                            if (long.TryParse(kvp.Key, out var entryId))
                            {
                                _entryToTags[entryId] = kvp.Value;
                                foreach (var tagId in kvp.Value)
                                {
                                    if (_tagToEntries.ContainsKey(tagId))
                                    {
                                        _tagToEntries[tagId].Add(entryId);
                                    }
                                }
                            }
                        }
                    }

                    _logger.LogInformation("Loaded {BookmarkCount} bookmarks and {TagCount} tags from {FilePath}", 
                        Bookmarks.Count, Tags.Count, filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load bookmarks and tags from {FilePath}", filePath);
            }
        }

        private class BookmarkData
        {
            public List<LogBookmark>? Bookmarks { get; set; }
            public List<LogTag>? Tags { get; set; }
            public Dictionary<string, List<string>>? EntryToTags { get; set; }
        }
    }
}
