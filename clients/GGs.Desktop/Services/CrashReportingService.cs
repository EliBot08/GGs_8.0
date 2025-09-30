using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GGs.Desktop.Services;

public sealed class CrashReportingService
{
    private static CrashReportingService? _instance;
    public static CrashReportingService Instance => _instance ??= new CrashReportingService();

    private readonly ConcurrentQueue<Breadcrumb> _breadcrumbs = new();
    private bool _initialized;

    private CrashReportingService() { }

    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        try
        {
            AppLogger.LogInfo("CrashReportingService initialized (opt-in: " + SettingsService.CrashReportingEnabled + ")");
        }
        catch { }
    }

    public void AddBreadcrumb(string message, string category = "app", string level = "info")
    {
        try
        {
            _breadcrumbs.Enqueue(new Breadcrumb
            {
                TimestampUtc = DateTime.UtcNow,
                Message = Scrub(message),
                Category = category,
                Level = level
            });
            // keep last ~100
            while (_breadcrumbs.Count > 100 && _breadcrumbs.TryDequeue(out _)) { }
        }
        catch { }
    }

    public void CaptureException(Exception? ex, string? contextMessage = null, Dictionary<string, string>? context = null)
    {
        try
        {
            if (!SettingsService.CrashReportingEnabled)
            {
                AppLogger.LogWarn("Crash captured but reporting disabled. Enable in Settings to send anonymized reports.");
                return;
            }

            var report = new CrashReport
            {
                TimestampUtc = DateTime.UtcNow,
                Message = Scrub(contextMessage ?? ex?.Message ?? "(no message)"),
                ExceptionType = ex?.GetType().FullName ?? "(unknown)",
                StackTrace = Scrub(ex?.ToString() ?? string.Empty),
                AppVersion = GetAppVersion(),
                OSDescription = RuntimeInformation.OSDescription,
                ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                AdditionalContext = context?.ToDictionary(kv => Scrub(kv.Key), kv => Scrub(kv.Value)) ?? new Dictionary<string, string>(),
                Breadcrumbs = _breadcrumbs.ToArray(),
                LogTail = ReadLogTailSafe(500)
            };

            var dir = Path.Combine(GetReportsRoot(), DateTime.UtcNow.ToString("yyyyMMdd"));
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"crash_{DateTime.UtcNow:HHmmss_fff}_{Guid.NewGuid():N}.json");
            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(file, json, Encoding.UTF8);
            AppLogger.LogInfo($"Crash report written to {file}");
        }
        catch (Exception writeEx)
        {
            try { AppLogger.LogError("Failed to write crash report", writeEx); } catch { }
        }
    }

    private static string GetAppVersion()
    {
        try { return System.Reflection.Assembly.GetEntryAssembly()?.GetName()?.Version?.ToString() ?? "0.0.0.0"; } catch { return "0.0.0.0"; }
    }

    private static string GetReportsRoot()
    {
        try
        {
            var baseDir = Path.GetDirectoryName(AppLogger.LogFilePath) ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var root = Path.Combine(baseDir, "crash-reports");
            return root;
        }
        catch { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GGs", "crash-reports"); }
    }

    public string GetReportsDirectory() => GetReportsRoot();

    public void OpenReportsFolder()
    {
        try
        {
            var dir = GetReportsRoot();
            Directory.CreateDirectory(dir);
            var psi = new ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch { }
    }

    private static string ReadLogTailSafe(int lineCount)
    {
        try
        {
            var path = AppLogger.LogFilePath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return string.Empty;
            // Efficient tail read
            var lines = new List<string>(lineCount);
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);
            var q = new Queue<string>(lineCount);
            while (!sr.EndOfStream)
            {
                var line = sr.ReadLine();
                if (line == null) break;
                if (q.Count == lineCount) q.Dequeue();
                q.Enqueue(Scrub(line));
            }
            return string.Join("\n", q);
        }
        catch { return string.Empty; }
    }

    private static readonly Regex EmailRegex = new("[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\\.[A-Za-z]{2,}", RegexOptions.Compiled);
    private static readonly Regex GuidRegex = new("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.Compiled);
    private static readonly Regex BearerRegex = new("Bearer\\s+[A-Za-z0-9-._~+/=]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LicenseRegex = new("[A-F0-9]{16,64}", RegexOptions.Compiled);

    public static string Scrub(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var s = input;
        try { s = EmailRegex.Replace(s, "[REDACTED_EMAIL]"); } catch { }
        try { s = BearerRegex.Replace(s, "Bearer [REDACTED]"); } catch { }
        try { s = GuidRegex.Replace(s, "[REDACTED_GUID]"); } catch { }
        try { s = LicenseRegex.Replace(s, "[REDACTED_KEY]"); } catch { }
        return s;
    }

    private sealed class Breadcrumb
    {
        public DateTime TimestampUtc { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Category { get; set; } = "app";
        public string Level { get; set; } = "info";
    }

    private sealed class CrashReport
    {
        public DateTime TimestampUtc { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ExceptionType { get; set; } = string.Empty;
        public string StackTrace { get; set; } = string.Empty;
        public string AppVersion { get; set; } = string.Empty;
        public string OSDescription { get; set; } = string.Empty;
        public string ProcessArchitecture { get; set; } = string.Empty;
        public Dictionary<string, string> AdditionalContext { get; set; } = new();
        public Breadcrumb[] Breadcrumbs { get; set; } = Array.Empty<Breadcrumb>();
        public string LogTail { get; set; } = string.Empty;
    }
}
