using System.IO;
using System.Text.Json;
using GGs.Shared.Tweaks;

namespace GGs.Desktop.Services;

public sealed class TweakBundleProfile
{
    public string Name { get; set; } = "My Profile";
    public List<TweakDefinition> Tweaks { get; set; } = new();
}

public static class ProfileService
{
    private static readonly JsonSerializerOptions _opts = new JsonSerializerOptions { WriteIndented = true };

    public static void Export(string path, IEnumerable<TweakDefinition> tweaks, string name = "My Profile")
    {
        var profile = new TweakBundleProfile { Name = name, Tweaks = tweaks.ToList() };
        var json = JsonSerializer.Serialize(profile, _opts);
        File.WriteAllText(path, json);
    }

    public static TweakBundleProfile Import(string path)
    {
        var json = File.ReadAllText(path);
        var profile = JsonSerializer.Deserialize<TweakBundleProfile>(json, _opts) ?? new TweakBundleProfile();
        return profile;
    }
}

