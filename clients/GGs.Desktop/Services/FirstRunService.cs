using System;
using System.IO;
using System.Text.Json;

namespace GGs.Desktop.Services;

public sealed class FirstRunState
{
    public bool ChecklistCompleted { get; set; }
    public bool ConnectedToServer { get; set; }
    public bool LicenseActivated { get; set; }
    public bool BaselineApplied { get; set; }
}

public static class FirstRunService
{
    private static readonly string PathFile = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GGs", "first_run.json");

    public static FirstRunState Load()
    {
        try
        {
            if (File.Exists(PathFile))
            {
                var json = File.ReadAllText(PathFile);
                return JsonSerializer.Deserialize<FirstRunState>(json) ?? new FirstRunState();
            }
        }
        catch { }
        return new FirstRunState();
    }

    public static void Save(FirstRunState state)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PathFile)!);
            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(PathFile, json);
        }
        catch { }
    }
}

