using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace GGs.Desktop.Views
{
    public partial class ErrorLogViewer : Window, INotifyPropertyChanged
    {
        public class LogEntry
        {
            public DateTime Timestamp { get; set; }
            public string Level { get; set; } = "INFO";
            public string Emoji { get; set; } = "ℹ️";
            public string Source { get; set; } = "";
            public string Message { get; set; } = "";
            public string RawLine { get; set; } = "";
        }

        private string _logRoot = string.Empty;
        private readonly DispatcherTimer _timer;
        private readonly FileSystemWatcher _watcher;
        private readonly Dictionary<string, long> _fileOffsets = new();
        private readonly HashSet<string> _watchedFiles = new(StringComparer.OrdinalIgnoreCase);
        private bool _paused;

        private const int MaxEntries = 20000;
        public ObservableCollection<LogEntry> Entries { get; } = new();

        public string LogRoot
        {
            get => _logRoot;
            set { _logRoot = value; OnPropertyChanged(nameof(LogRoot)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public ErrorLogViewer()
        {
            InitializeComponent();
            try { GGs.Desktop.Services.IconService.ApplyWindowIcon(this); } catch { }
            DataContext = this;

            // Determine log root
            var fromEnv = Environment.GetEnvironmentVariable("GGS_LOG_DIR");
            if (!string.IsNullOrWhiteSpace(fromEnv) && Directory.Exists(fromEnv))
                LogRoot = fromEnv!;
            else
                LogRoot = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GGs", "logs");

            // Ensure log directory exists
            try
            {
                if (!Directory.Exists(LogRoot))
                {
                    Directory.CreateDirectory(LogRoot);
                }
            }
            catch (Exception ex)
            {
                // Fallback to temp directory if LocalApplicationData fails
                LogRoot = System.IO.Path.Combine(Path.GetTempPath(), "GGs", "logs");
                Directory.CreateDirectory(LogRoot);
            }

            TxtFolder.Text = LogRoot;

            // Wire UI
            BtnPause.Click += (_, __) => { _paused = !_paused; BtnPause.Content = _paused ? "Resume" : "Pause"; };
            BtnClear.Click += (_, __) => Entries.Clear();
            BtnOpenFolder.Click += (_, __) => { try { Process.Start("explorer.exe", LogRoot); } catch { } };
            BtnExport.Click += (_, __) => ExportLogs();

            // Timer to poll files frequently (non-blocking)
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += (_, __) => { if (!_paused) Poll(); UpdateStats(); };
            _timer.Start();

            // Watcher to catch new files
            try
            {
                _watcher = new FileSystemWatcher(LogRoot)
                {
                    Filter = "*.*",
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
                };
                _watcher.Created += (_, e) => AddFileIfNew(e.FullPath);
                _watcher.Changed += (_, e) => { /* polled */ };
                _watcher.Renamed += (_, e) => AddFileIfNew(e.FullPath);
                _watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                // If FileSystemWatcher fails, we'll rely on polling only
                Debug.WriteLine($"FileSystemWatcher initialization failed: {ex.Message}");
            }

            // Initialize with current files (both text and JSON, including archives)
            try
            {
                foreach (var f in Directory.EnumerateFiles(LogRoot, "*.*", SearchOption.AllDirectories))
                    AddFileIfNew(f);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Directory enumeration failed: {ex.Message}");
            }

            // Ensure we include the active sinks even if the directory is empty or files are newly created
            try { AddFileIfNew(System.IO.Path.Combine(LogRoot, "desktop.log")); } catch { }
            try { AddFileIfNew(System.IO.Path.Combine(LogRoot, "desktop.jsonl")); } catch { }

            // Initial poll
            Poll();

            // If nothing was read (race conditions, env idiosyncrasies), add a priming entry so the viewer is never empty
            if (Entries.Count == 0)
            {
                Entries.Add(new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Level = "INFO",
                    Emoji = "ℹ️",
                    Source = "Viewer",
                    Message = "Log viewer started. Awaiting log entries…",
                    RawLine = ""
                });
            }
        }

        private static bool IsLogFile(string path)
        {
            var name = System.IO.Path.GetFileName(path).ToLowerInvariant();
            if (name.EndsWith(".log") || name.EndsWith(".log.gz") || name.EndsWith(".jsonl") || name.EndsWith(".jsonl.gz")) return true;
            return false;
        }

        private void AddFileIfNew(string path)
        {
            if (!File.Exists(path)) return;
            if (!IsLogFile(path)) return;
            if (_watchedFiles.Add(path))
            {
                // For archives (.gz), process once
                _fileOffsets[path] = 0L; // read from start; will be set to -1 after processing gz
            }
        }

        private void Poll()
        {
            try
            {
                foreach (var file in _watchedFiles.ToList())
                {
                    ReadNewLines(file);
                }

                // Auto-scroll last row
                if (GridLogs.Items.Count > 0)
                {
                    GridLogs.ScrollIntoView(GridLogs.Items[GridLogs.Items.Count - 1]);
                }
            }
            catch (Exception ex)
            {
                // Keep viewer resilient
                Entries.Add(new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Level = "WARN",
                    Emoji = "⚠️",
                    Source = "Viewer",
                    Message = $"Log viewer encountered an issue: {ex.Message}",
                    RawLine = ex.ToString()
                });
            }
        }

        private void UpdateStats()
        {
            try
            {
                var proc = Process.GetCurrentProcess();
                var ramMb = proc.WorkingSet64 / (1024.0 * 1024.0);
                TxtStats.Text = $"Entries: {Entries.Count:N0} | RAM: {ramMb:N1} MB";
            }
            catch { }
        }

        private void ExportLogs()
        {
            try
            {
                var dlg = new SaveFileDialog
                {
                    FileName = $"ggs_logs_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
                };
                if (dlg.ShowDialog(this) == true)
                {
                    using var sw = new StreamWriter(dlg.FileName, false, Encoding.UTF8);
                    sw.WriteLine("timestamp,level,emoji,source,message");
                    foreach (var e in Entries)
                    {
                        string esc(string v) => '"' + (v ?? string.Empty).Replace("\"", "\"\"") + '"';
                        sw.WriteLine($"{e.Timestamp:O},{e.Level},{esc(e.Emoji)},{esc(e.Source)},{esc(e.Message)}");
                    }
                }
            }
            catch (Exception ex)
            {
                try { MessageBox.Show(this, $"Export failed: {ex.Message}", "GGs", MessageBoxButton.OK, MessageBoxImage.Error); } catch { }
            }
        }

        private void ReadNewLines(string path)
        {
            try
            {
                var lastOffset = _fileOffsets.TryGetValue(path, out var off) ? off : 0L;

                var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
                var isGz = ext == ".gz";

                if (isGz)
                {
                    // Process compressed archives once in full, then mark as done
                    if (lastOffset == -1) return; // already processed
                    using var fsGz = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var gz = new GZipStream(fsGz, CompressionMode.Decompress);
                    using var srGz = new StreamReader(gz, Encoding.UTF8);
                    string? lineGz;
                    while ((lineGz = srGz.ReadLine()) != null)
                    {
                        var entry = ParseLine(path, lineGz);
                        if (PassesFilter(entry))
                        {
                            Entries.Add(entry);
                            if (Entries.Count > MaxEntries)
                            {
                                for (int i = 0; i < 1000 && Entries.Count > 0; i++) Entries.RemoveAt(0);
                            }
                        }
                    }
                    _fileOffsets[path] = -1; // mark as processed
                    return;
                }

                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (lastOffset > fs.Length) lastOffset = 0; // file rotated/truncated
                fs.Position = lastOffset;
                using var sr = new StreamReader(fs, Encoding.UTF8);
                string? line;
                while ((line = sr.ReadLine()) != null)
                {
                    var entry = ParseLine(path, line);
                    if (PassesFilter(entry))
                    {
                        Entries.Add(entry);
                        if (Entries.Count > MaxEntries)
                        {
                            for (int i = 0; i < 1000 && Entries.Count > 0; i++) Entries.RemoveAt(0);
                        }
                    }
                }
                _fileOffsets[path] = fs.Position;
            }
            catch
            {
                // Ignore transient read issues
            }
        }

        private bool PassesFilter(LogEntry entry)
        {
            // Level filters
            bool info = ChkInfo.IsChecked == true;
            bool warn = ChkWarn.IsChecked == true;
            bool error = ChkError.IsChecked == true;
            bool debug = ChkDebug.IsChecked == true;
            bool levelOk = entry.Level switch
            {
                "ERROR" => error,
                "WARN" => warn,
                "OK" => info,
                "INFO" => info,
                "DEBUG" => debug,
                _ => true
            };
            if (!levelOk) return false;

            var q = TxtSearch.Text?.Trim();
            if (!string.IsNullOrEmpty(q))
            {
                return (entry.Message?.IndexOf(q, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                    || (entry.Source?.IndexOf(q, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                    || (entry.RawLine?.IndexOf(q, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
            }
            return true;
        }

        private static readonly Regex DesktopPattern = new(
            @"^(?<level>START|OK|INFO|WARN|DEBUG|ERROR)\s+(?<ts>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}(?:\.\d{3})?)\s+(?<emoji>\p{So}|\S+)?\s*(?<msg>.*)$",
            RegexOptions.Compiled);

        private static readonly Regex LauncherPattern = new(
            @"^\[(?<ts>[^\]]+)\]\s+\[(?<level>INFO|WARNING|ERROR|SUCCESS|DEBUG)\]\s+(?<msg>.*)$",
            RegexOptions.Compiled);

        private LogEntry ParseLine(string path, string line)
        {
            try
            {
                // JSON structured log (desktop.jsonl)
                if (!string.IsNullOrWhiteSpace(line) && line.TrimStart().StartsWith("{"))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(line);
                        var root = doc.RootElement;
                        string lvl = root.TryGetProperty("Level", out var lvlProp) ? (lvlProp.GetString() ?? "INFO")
                                    : root.TryGetProperty("level", out var lvlProp2) ? (lvlProp2.GetString() ?? "INFO") : "INFO";
                        string msg = root.TryGetProperty("Message", out var msgProp) ? (msgProp.GetString() ?? "")
                                    : root.TryGetProperty("message", out var msgProp2) ? (msgProp2.GetString() ?? "") : "";
                        DateTime ts = DateTime.Now;
                        if (root.TryGetProperty("Timestamp", out var tsProp) && tsProp.ValueKind == JsonValueKind.String)
                        {
                            DateTime.TryParse(tsProp.GetString(), out ts);
                        }
                        else if (root.TryGetProperty("timestamp", out var tsProp2) && tsProp2.ValueKind == JsonValueKind.String)
                        {
                            DateTime.TryParse(tsProp2.GetString(), out ts);
                        }
                        // Exception summary
                        string excMsg = string.Empty;
                        if (root.TryGetProperty("Exception", out var exProp) && exProp.ValueKind == JsonValueKind.Object)
                        {
                            string type = exProp.TryGetProperty("Type", out var t) ? (t.GetString() ?? "") : "";
                            string em = exProp.TryGetProperty("Message", out var msgEl1) ? (msgEl1.GetString() ?? "") : "";
                            excMsg = string.IsNullOrEmpty(type) && string.IsNullOrEmpty(em) ? string.Empty : $" ({type}: {em})";
                        }
                        else if (root.TryGetProperty("exception", out var exProp2) && exProp2.ValueKind == JsonValueKind.Object)
                        {
                            string type = exProp2.TryGetProperty("type", out var t) ? (t.GetString() ?? "") : "";
                            string em = exProp2.TryGetProperty("message", out var msgEl2) ? (msgEl2.GetString() ?? "") : "";
                            excMsg = string.IsNullOrEmpty(type) && string.IsNullOrEmpty(em) ? string.Empty : $" ({type}: {em})";
                        }

                        var lvlNorm = lvl.ToUpperInvariant();
                        return new LogEntry
                        {
                            Timestamp = ts == default ? DateTime.Now : ts,
                            Level = lvlNorm,
                            Emoji = LevelToEmoji(lvlNorm),
                            Source = InferSource(path),
                            Message = string.IsNullOrEmpty(excMsg) ? msg : (msg + excMsg),
                            RawLine = line
                        };
                    }
                    catch
                    {
                        // fallthrough to text parsers
                    }
                }

                // Desktop format (AppLogger text)
                var dm = DesktopPattern.Match(line);
                if (dm.Success)
                {
                    var lvl = dm.Groups["level"].Value;
                    var emoji = dm.Groups["emoji"].Success ? dm.Groups["emoji"].Value : LevelToEmoji(lvl);
                    DateTime.TryParse(dm.Groups["ts"].Value, out var ts);
                    return new LogEntry
                    {
                        Timestamp = ts == default ? DateTime.Now : ts,
                        Level = lvl,
                        Emoji = emoji,
                        Source = InferSource(path),
                        Message = dm.Groups["msg"].Value,
                        RawLine = line
                    };
                }

                // Launcher format
                var l = LauncherPattern.Match(line);
                if (l.Success)
                {
                    var lvlMap = l.Groups["level"].Value switch
                    {
                        "SUCCESS" => "OK",
                        "WARNING" => "WARN",
                        var other => other
                    };
                    DateTime.TryParse(l.Groups["ts"].Value, out var ts);
                    return new LogEntry
                    {
                        Timestamp = ts == default ? DateTime.Now : ts,
                        Level = lvlMap,
                        Emoji = LevelToEmoji(lvlMap),
                        Source = InferSource(path),
                        Message = l.Groups["msg"].Value,
                        RawLine = line
                    };
                }

                // Fallback raw
                return new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Level = "INFO",
                    Emoji = "ℹ️",
                    Source = InferSource(path),
                    Message = line,
                    RawLine = line
                };
            }
            catch (Exception ex)
            {
                return new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Level = "WARN",
                    Emoji = "⚠️",
                    Source = "Parser",
                    Message = $"Could not parse line: {ex.Message}. Raw: {line}",
                    RawLine = line
                };
            }
        }

        private static string InferSource(string path)
        {
            var file = System.IO.Path.GetFileName(path);
            if (file.StartsWith("desktop", StringComparison.OrdinalIgnoreCase)) return "Desktop";
            if (file.Contains("server", StringComparison.OrdinalIgnoreCase)) return "Server";
            if (file.StartsWith("launch_", StringComparison.OrdinalIgnoreCase)) return "Launcher";
            return file;
        }

        private static string LevelToEmoji(string level) => level switch
        {
            "ERROR" => "❌",
            "WARN" => "⚠️",
            "OK" => "✅",
            "INFO" => "ℹ️",
            "DEBUG" => "🐛",
            "START" => "🚀",
            "STOP" => "🛑",
            _ => "ℹ️"
        };
    }
}

