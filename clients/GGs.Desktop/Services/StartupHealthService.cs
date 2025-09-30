using System.Text.Json;
using System.IO;

namespace GGs.Desktop.Services;

public sealed class StartupHealthService
{
    private readonly string _healthDir;
    private readonly string _startupFile;
    private readonly string _runningLock;
    private readonly string _readyFile;

    private readonly object _sync = new object();

    private class StartupEntry
    {
        public DateTime TimestampUtc { get; set; }
        public bool CleanExit { get; set; }
    }

    public StartupHealthService()
    {
        var baseDir = Environment.GetEnvironmentVariable("GGS_HEALTH_DIR");
        if (string.IsNullOrWhiteSpace(baseDir))
        {
            baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GGs", "health");
        }
        _healthDir = baseDir!;
        _startupFile = Path.Combine(_healthDir, "startup.json");
        _runningLock = Path.Combine(_healthDir, "running.lock");
        _readyFile = Path.Combine(_healthDir, "desktop_ready");
        try { Directory.CreateDirectory(_healthDir); } catch { }
    }

    public void OnStartupBegin()
    {
        try
        {
            lock (_sync)
            {
                var entries = LoadEntries();
                entries.Add(new StartupEntry { TimestampUtc = DateTime.UtcNow, CleanExit = false });
                SaveEntries(entries);
                // Create running lock
                File.WriteAllText(_runningLock, DateTime.UtcNow.ToString("o"));
            }
        }
        catch { }
    }

    public void MarkReady()
    {
        try
        {
            File.WriteAllText(_readyFile, DateTime.UtcNow.ToString("o"));
        }
        catch { }
    }

    public void MarkCleanExit()
    {
        try
        {
            lock (_sync)
            {
                var entries = LoadEntries();
                if (entries.Count > 0)
                {
                    entries[^1].CleanExit = true;
                    SaveEntries(entries);
                }
                if (File.Exists(_runningLock)) File.Delete(_runningLock);
                if (File.Exists(_readyFile)) File.Delete(_readyFile);
            }
        }
        catch { }
    }

    public bool IsCrashLoop(int thresholdCount = 3, int windowSeconds = 60)
    {
        try
        {
            var entries = LoadEntries();
            var since = DateTime.UtcNow.AddSeconds(-windowSeconds);
            var recent = entries.Where(e => e.TimestampUtc >= since).ToList();
            // Count unclean recent startups OR presence of leftover running.lock from previous session
            int unclean = recent.Count(e => !e.CleanExit);
            bool leftoverLock = File.Exists(_runningLock);
            if (leftoverLock) unclean++;
            return unclean >= thresholdCount;
        }
        catch { return false; }
    }

    private List<StartupEntry> LoadEntries()
    {
        try
        {
            if (!File.Exists(_startupFile)) return new List<StartupEntry>();
            var json = File.ReadAllText(_startupFile);
            var entries = JsonSerializer.Deserialize<List<StartupEntry>>(json) ?? new List<StartupEntry>();
            // Prune older than 24h
            var cutoff = DateTime.UtcNow.AddHours(-24);
            return entries.Where(e => e.TimestampUtc >= cutoff).ToList();
        }
        catch { return new List<StartupEntry>(); }
    }

    private void SaveEntries(List<StartupEntry> entries)
    {
        try
        {
            var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_startupFile, json);
        }
        catch { }
    }
}
