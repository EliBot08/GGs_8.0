using Microsoft.Win32;

namespace GGs.Desktop.Services;

public static class SettingsService
{
    private const string RegistryRoot = @"Software\\GGs\\Desktop";
    private const string RunKey = @"Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    private const string AppRunName = "GGsDesktop";

    public static bool LaunchMinimized
    {
        get => GetBool("LaunchMinimized", defaultValue: false);
        set => SetBool("LaunchMinimized", value);
    }

    public static bool StartWithWindows
    {
        get => IsAutoStartEnabled();
        set => SetAutoStart(value);
    }

    public static string UpdateChannel
    {
        get => GetString("UpdateChannel", defaultValue: "stable");
        set => SetString("UpdateChannel", string.IsNullOrWhiteSpace(value) ? "stable" : value);
    }

    public static DateTime? LastUpdateCheckUtc
    {
        get => GetDateTime("LastUpdateCheckUtc");
        set => SetDateTime("LastUpdateCheckUtc", value);
    }

    public static bool UpdateSilent
    {
        get => GetBool("UpdateSilent", defaultValue: false);
        set => SetBool("UpdateSilent", value);
    }

    public static int UpdateBandwidthLimitKBps
    {
        get => GetInt("UpdateBandwidthLimitKBps", defaultValue: 0);
        set => SetInt("UpdateBandwidthLimitKBps", value);
    }

    public static bool CrashReportingEnabled
    {
        get => GetBool("CrashReportingEnabled", defaultValue: false);
        set => SetBool("CrashReportingEnabled", value);
    }

    // Explicit user opt-in for elevated, deep system optimizations (admin/service/driver powered).
    public static bool DeepOptimizationEnabled
    {
        get => GetBool("DeepOptimizationEnabled", defaultValue: false);
        set => SetBool("DeepOptimizationEnabled", value);
    }

    private static bool GetBool(string name, bool defaultValue)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRoot, false);
            var v = key?.GetValue(name)?.ToString();
            if (bool.TryParse(v, out var b)) return b;
        }
        catch { }
        return defaultValue;
    }

    private static void SetBool(string name, bool value)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryRoot);
            key?.SetValue(name, value.ToString());
        }
        catch { }
    }

    private static string GetString(string name, string defaultValue)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRoot, false);
            var v = key?.GetValue(name)?.ToString();
            if (!string.IsNullOrWhiteSpace(v)) return v!;
        }
        catch { }
        return defaultValue;
    }

    private static int GetInt(string name, int defaultValue)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRoot, false);
            var v = key?.GetValue(name)?.ToString();
            if (int.TryParse(v, out var i)) return i;
        }
        catch { }
        return defaultValue;
    }

    private static void SetInt(string name, int value)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryRoot);
            key?.SetValue(name, value);
        }
        catch { }
    }

    private static void SetString(string name, string value)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryRoot);
            key?.SetValue(name, value);
        }
        catch { }
    }

    private static DateTime? GetDateTime(string name)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRoot, false);
            var v = key?.GetValue(name)?.ToString();
            if (DateTime.TryParse(v, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var dt))
            {
                return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            }
        }
        catch { }
        return null;
    }

    private static void SetDateTime(string name, DateTime? value)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryRoot);
            if (value.HasValue)
            {
                key?.SetValue(name, value.Value.ToUniversalTime().ToString("o"));
            }
            else
            {
                key?.DeleteValue(name, false);
            }
        }
        catch { }
    }

    private static bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            var value = key?.GetValue(AppRunName) as string;
            return !string.IsNullOrWhiteSpace(value);
        }
        catch { return false; }
    }

    private static void SetAutoStart(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (enable)
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                key?.SetValue(AppRunName, '"' + exePath + '"');
            }
            else
            {
                key?.DeleteValue(AppRunName, false);
            }
        }
        catch { }
    }
}
