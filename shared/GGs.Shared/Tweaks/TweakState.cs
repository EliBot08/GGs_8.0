using System.Text.Json;

namespace GGs.Shared.Tweaks;

// Lightweight state models for consistent before/after capture across tweak types.
// These are serialized into the existing TweakApplicationLog.BeforeState/AfterState strings
// to avoid schema changes.
public static class TweakStateSerializer
{
    private static readonly JsonSerializerOptions _opts = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Serialize<T>(T state) where T : ITweakState
        => JsonSerializer.Serialize(state, _opts);

    public static bool TryDeserialize(string? json, out ITweakState? state)
    {
        state = null;
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("type", out var typeEl)) return false;
            var type = typeEl.GetString()?.ToLowerInvariant();
            state = type switch
            {
                "registry" => JsonSerializer.Deserialize<RegistryState>(json, _opts),
                "service" => JsonSerializer.Deserialize<ServiceState>(json, _opts),
                "script" => JsonSerializer.Deserialize<ScriptState>(json, _opts),
                _ => null
            };
            return state != null;
        }
        catch { return false; }
    }
}

public interface ITweakState { string Type { get; } }

public sealed class RegistryState : ITweakState
{
    public string Type => "registry";
    public string? Path { get; set; }
    public string? Name { get; set; }
    public string? ValueType { get; set; }
    public string? Data { get; set; }
}

public sealed class ServiceState : ITweakState
{
    public string Type => "service";
    public string? ServiceName { get; set; }
    public string? Status { get; set; } // Running | Stopped | <notfound> | <error>
    public string? ActionApplied { get; set; } // Start | Stop | Restart | Enable | Disable
}

public sealed class ScriptState : ITweakState
{
    public string Type => "script";
    public string? Output { get; set; }
    public bool UndoAvailable { get; set; }
    public string? ScriptApplied { get; set; }
    public string? UndoScript { get; set; }
}

