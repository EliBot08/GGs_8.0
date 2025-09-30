using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Text.Json;

namespace GGs.Desktop.Configuration;

public sealed class UserSettings
{
    public const int CurrentVersion = 2;

    public int Version { get; set; } = CurrentVersion;

    // General
    public bool LaunchMinimized { get; set; }
    public bool StartWithWindows { get; set; }

    // Server
    [Url]
    [Required]
    public string ServerBaseUrl { get; set; } = "https://localhost:5001";

    // Updates
    [Required]
    [RegularExpression("^(stable|beta|dev)$", ErrorMessage = "Channel must be stable|beta|dev")] 
    public string UpdateChannel { get; set; } = "stable";
    public bool UpdateSilent { get; set; }
    [Range(0, 1024*1024)]
    public int UpdateBandwidthLimitKBps { get; set; }

    // Privacy
    public bool CrashReportingEnabled { get; set; }

    // Appearance
    // system|dark|light
    public string Theme { get; set; } = "system";
    // Hex colors like #RRGGBB or #AARRGGBB
    public string AccentPrimaryHex { get; set; } = "#FFFF1464"; // matches default AccentColor
    public string AccentSecondaryHex { get; set; } = "#FF00D9FF"; // matches default AccentLightColor
    // Global font size in points for accessibility (applies to Application resource GlobalFontSize)
    public double FontSizePoints { get; set; } = 13.0;

    // Reserved for future: typed feature flags, telemetry, etc.

    public void ValidateAndThrow()
    {
        var ctx = new ValidationContext(this);
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(this, ctx, results, validateAllProperties: true))
        {
            throw new ValidationException(string.Join("; ", results.ConvertAll(r => r.ErrorMessage)));
        }
    }

    public static UserSettings FromJson(string json)
    {
        var s = JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
        if (s.Version != CurrentVersion)
        {
            s = Migrate(s);
        }
        s.ValidateAndThrow();
        return s;
    }

    public static string ToJson(UserSettings settings)
    {
        settings.ValidateAndThrow();
        return JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
    }

    private static UserSettings Migrate(UserSettings incoming)
    {
        // v1 -> v2: introduce Theme/Accent/FontSizePoints with safe defaults
        if (incoming.Version < 2)
        {
            if (string.IsNullOrWhiteSpace(incoming.ServerBaseUrl)) incoming.ServerBaseUrl = "https://localhost:5001";
            if (string.IsNullOrWhiteSpace(incoming.UpdateChannel)) incoming.UpdateChannel = "stable";
            if (incoming.FontSizePoints <= 0) incoming.FontSizePoints = 13.0;
            if (string.IsNullOrWhiteSpace(incoming.Theme)) incoming.Theme = "system";
            if (string.IsNullOrWhiteSpace(incoming.AccentPrimaryHex)) incoming.AccentPrimaryHex = "#FFFF1464";
            if (string.IsNullOrWhiteSpace(incoming.AccentSecondaryHex)) incoming.AccentSecondaryHex = "#FF00D9FF";
        }
        incoming.Version = CurrentVersion;
        return incoming;
    }
}

