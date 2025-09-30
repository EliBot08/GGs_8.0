using System.Collections.ObjectModel;
using System.Text.Json;
using System.Timers;
using System.IO;
using System.Text;
using System.Linq;
using System.Reflection;
using CommunityToolkit.WinUI.Notifications;

namespace GGs.Desktop.Services;

public enum NotificationType
{
    Info,
    Warning,
    Error,
    License,
    Tweak,
    System
}

public sealed class NotificationItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public NotificationType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime TimeUtc { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }

    // Optional navigation hints
    public string? NavigateTab { get; set; } // e.g., "Analytics", "Tweaks", "Licenses"
    public Guid? TweakId { get; set; }
    public Guid? LogId { get; set; }
}

public static class NotificationCenter
{
    private static readonly ObservableCollection<NotificationItem> _items = new();
    public static ReadOnlyObservableCollection<NotificationItem> Items { get; } = new(_items);

    public static event EventHandler<int>? UnreadCountChanged;

    private static readonly object _sync = new();
    private static readonly string _dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GGs", "notifications");
    private static readonly string _filePath = Path.Combine(_dataDir, "notifications.json");
    private static readonly int _maxItems = 200;
    private static readonly TimeSpan _maxAge = TimeSpan.FromDays(30);
    private static readonly System.Timers.Timer _saveDebounce = new System.Timers.Timer(500) { AutoReset = false };

    static NotificationCenter()
    {
        try
        {
            var baseDirOverride = Environment.GetEnvironmentVariable("GGS_DATA_DIR");
            var baseDir = !string.IsNullOrWhiteSpace(baseDirOverride)
                ? baseDirOverride!
                : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _dataDir = Path.Combine(baseDir, "GGs", "notifications");
            Directory.CreateDirectory(_dataDir);
            _filePath = Path.Combine(_dataDir, "notifications.json");
            _saveDebounce.Elapsed += (_, __) => SaveInternal();
            LoadInternal();
        }
        catch { }
    }

    public static int UnreadCount => _items.Count(i => !i.IsRead);

    public static void Add(NotificationType type, string message, string? navigateTab = null, Guid? tweakId = null, Guid? logId = null, bool showToast = false)
    {
        var item = new NotificationItem
        {
            Type = type,
            Message = message,
            NavigateTab = navigateTab,
            TweakId = tweakId,
            LogId = logId,
            TimeUtc = DateTime.UtcNow,
            IsRead = false
        };
        _items.Insert(0, item);
        PruneIfNeeded();
        DebouncedSave();
        UnreadCountChanged?.Invoke(null, UnreadCount);
        if (showToast)
        {
            TryShowToast(item);
        }
    }

    public static void MarkAllAsRead()
    {
        foreach (var n in _items) n.IsRead = true;
        DebouncedSave();
        UnreadCountChanged?.Invoke(null, UnreadCount);
    }

    public static void MarkAsRead(Guid id)
    {
        var n = _items.FirstOrDefault(x => x.Id == id);
        if (n != null) n.IsRead = true;
        DebouncedSave();
        UnreadCountChanged?.Invoke(null, UnreadCount);
    }

    private static void LoadInternal()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            var json = File.ReadAllText(_filePath);
            var list = JsonSerializer.Deserialize<List<NotificationItem>>(json) ?? new List<NotificationItem>();
            _items.Clear();
            foreach (var i in list.OrderByDescending(i => i.TimeUtc)) _items.Add(i);
            PruneIfNeeded();
            UnreadCountChanged?.Invoke(null, UnreadCount);
        }
        catch { }
    }

    private static void SaveInternal()
    {
        try
        {
            var list = _items.ToList();
            var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json, Encoding.UTF8);
        }
        catch { }
    }

    private static void DebouncedSave()
    {
        try { _saveDebounce.Stop(); _saveDebounce.Start(); } catch { }
    }

    private static void PruneIfNeeded()
    {
        try
        {
            var cutoff = DateTime.UtcNow - _maxAge;
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                if (_items[i].TimeUtc < cutoff) _items.RemoveAt(i);
            }
            while (_items.Count > _maxItems) _items.RemoveAt(_items.Count - 1);
        }
        catch { }
    }

    private static void TryShowToast(NotificationItem item)
    {
        try
        {
            // Build toast content; deep link via launching app with nav argument
            var title = item.Type.ToString();
            var content = new ToastContentBuilder()
                .AddText($"{title}")
                .AddText(item.Message);
            if (!string.IsNullOrWhiteSpace(item.NavigateTab))
            {
                content.AddArgument("nav", item.NavigateTab);
            }
            // Attempt to invoke Show() via reflection if available; otherwise, no-op
            try
            {
                var mi = typeof(ToastContentBuilder).GetMethod("Show", Type.EmptyTypes);
                mi?.Invoke(content, null);
            }
            catch { }
        }
        catch { }
    }
}

// Add a simple NotificationService used by ErrorHandlingService
public sealed class NotificationService
{
    public void ShowError(string message, string title = "Error")
    {
        NotificationCenter.Add(NotificationType.Error, $"{title}: {message}", showToast: true);
    }

    public void ShowWarning(string message, string title = "Warning")
    {
        NotificationCenter.Add(NotificationType.Warning, $"{title}: {message}", showToast: true);
    }

    public void ShowInfo(string message, string title = "Information")
    {
        NotificationCenter.Add(NotificationType.Info, $"{title}: {message}", showToast: false);
    }

    public void ShowSuccess(string message, string title = "Success")
    {
        NotificationCenter.Add(NotificationType.System, $"{title}: {message}", showToast: false);
    }
}
