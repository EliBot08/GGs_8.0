using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GGs.Desktop.Configuration;
using Microsoft.Extensions.Configuration;

namespace GGs.Desktop.Services;

public sealed class SettingsManager
{
    private static readonly object _sync = new();
    private readonly string _settingsPath;
    private readonly string _secretsPath;

    public SettingsManager()
    {
        var baseDirOverride = Environment.GetEnvironmentVariable("GGS_DATA_DIR");
        var baseDir = !string.IsNullOrWhiteSpace(baseDirOverride)
            ? baseDirOverride!
            : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(baseDir, "GGs");
        Directory.CreateDirectory(dir);
        _settingsPath = Path.Combine(dir, "settings.json");
        _secretsPath = Path.Combine(dir, "settings.secrets.bin");
    }

    public UserSettings Load()
    {
        lock (_sync)
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    // Bootstrap from existing services (back-compat)
                    var seed = new UserSettings
                    {
                        LaunchMinimized = SettingsService.LaunchMinimized,
                        StartWithWindows = SettingsService.StartWithWindows,
                        ServerBaseUrl = ReadServerBaseUrlFallback(),
                        UpdateChannel = SettingsService.UpdateChannel,
                        UpdateSilent = SettingsService.UpdateSilent,
                        UpdateBandwidthLimitKBps = Math.Max(0, SettingsService.UpdateBandwidthLimitKBps),
                        CrashReportingEnabled = SettingsService.CrashReportingEnabled
                    };
                    Save(seed);
                    return seed;
                }
                var json = File.ReadAllText(_settingsPath, Encoding.UTF8);
                return UserSettings.FromJson(json);
            }
            catch
            {
                return new UserSettings();
            }
        }
    }

    public void Save(UserSettings settings)
    {
        lock (_sync)
        {
            var json = UserSettings.ToJson(settings);
            File.WriteAllText(_settingsPath, json, Encoding.UTF8);
            // Keep registry-backed toggles in sync for back-compat
            SettingsService.LaunchMinimized = settings.LaunchMinimized;
            SettingsService.StartWithWindows = settings.StartWithWindows;
            SettingsService.UpdateChannel = settings.UpdateChannel;
            SettingsService.UpdateSilent = settings.UpdateSilent;
            SettingsService.UpdateBandwidthLimitKBps = settings.UpdateBandwidthLimitKBps;
            SettingsService.CrashReportingEnabled = settings.CrashReportingEnabled;

            // Apply appearance immediately (best-effort)
            try { ThemeManagerService.Instance.ApplyAppearance(settings); } catch { }
        }
    }

    public (bool ok, string? error) TryImport(string path)
    {
        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            var imported = UserSettings.FromJson(json);
            Save(imported);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public (bool ok, string? error) TryExport(string path)
    {
        try
        {
            var json = UserSettings.ToJson(Load());
            File.WriteAllText(path, json, Encoding.UTF8);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    // Secrets API (DPAPI protected)
    private sealed class SecretsModel
    {
        public string? CloudProfiles_ApiToken { get; set; }
    }

    public void SetCloudProfilesApiToken(string? token)
    {
        var model = LoadSecrets();
        model.CloudProfiles_ApiToken = token;
        SaveSecrets(model);
    }

    public string? GetCloudProfilesApiToken()
    {
        var model = LoadSecrets();
        return model.CloudProfiles_ApiToken;
    }

    private SecretsModel LoadSecrets()
    {
        try
        {
            if (!File.Exists(_secretsPath)) return new SecretsModel();
            var blob = File.ReadAllBytes(_secretsPath);
            var bytes = ProtectedData.Unprotect(blob, null, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(bytes);
            return JsonSerializer.Deserialize<SecretsModel>(json) ?? new SecretsModel();
        }
        catch
        {
            return new SecretsModel();
        }
    }

    private void SaveSecrets(SecretsModel secrets)
    {
        try
        {
            var json = JsonSerializer.Serialize(secrets, new JsonSerializerOptions { WriteIndented = true });
            var bytes = Encoding.UTF8.GetBytes(json);
            var blob = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(_secretsPath, blob);
        }
        catch { }
    }

    private static string ReadServerBaseUrlFallback()
    {
        try
        {
            var cfg = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();
            var v = cfg["Server:BaseUrl"];
            if (!string.IsNullOrWhiteSpace(v)) return v!;
        }
        catch { }
        return "https://localhost:5001";
    }
}

