using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace GGs.Agent;

internal static class ElevatedEntry
{
    internal static int Run(string[] args)
    {
        try
        {
            var payloadPath = GetArgValue(args, "--payload");
            if (string.IsNullOrWhiteSpace(payloadPath) || !File.Exists(payloadPath))
            {
                return WriteResponse(1, "Missing or invalid payload path.");
            }
            var json = File.ReadAllText(payloadPath, Encoding.UTF8);
            var req = JsonSerializer.Deserialize<ElevatedRequest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (req == null || string.IsNullOrWhiteSpace(req.Type))
            {
                return WriteResponse(1, "Invalid payload.");
            }

            switch (req.Type.ToLowerInvariant())
            {
                case "flushdns":
                    return ExecProcess("ipconfig", "/flushdns");
                case "winsockreset":
                    return ExecProcess("netsh", "winsock reset");
                case "tcpauthnormal":
                    return ExecProcess("netsh", "int tcp set global autotuninglevel=normal");
                case "powercfgsetactive":
                    if (!Guid.TryParse(req.Guid, out var g)) return WriteResponse(1, "Invalid GUID.");
                    return ExecProcess("powercfg", $"/setactive {g}");
                case "bcdedittimeout":
                    if (req.TimeoutSeconds is null || req.TimeoutSeconds < 0 || req.TimeoutSeconds > 60)
                        return WriteResponse(1, "Timeout out of range (0-60).");
                    return ExecProcess("bcdedit", $"/timeout {req.TimeoutSeconds}");
                case "registryset":
                {
                    if (req.Registry is null) return WriteResponse(1, "Missing registry payload");
                    var t = new GGs.Shared.Tweaks.TweakDefinition
                    {
                        Id = Guid.NewGuid(),
                        Name = "Elevated RegistrySet",
                        CommandType = GGs.Shared.Enums.CommandType.Registry,
                        RegistryPath = req.Registry.Path,
                        RegistryValueName = req.Registry.Name,
                        RegistryValueType = req.Registry.ValueType,
                        RegistryValueData = req.Registry.Data,
                        AllowUndo = true,
                        RequiresAdmin = true
                    };
                    var log = TweakExecutor.Apply(t);
                    return WriteResponse(log.Success ? 0 : 1, log.Success ? "OK" : log.Error ?? "Error");
                }
                case "serviceaction":
                {
                    if (req.Service is null) return WriteResponse(1, "Missing service payload");
                    if (!Enum.TryParse<GGs.Shared.Enums.ServiceAction>(req.Service.Action, true, out var act))
                        return WriteResponse(1, "Invalid service action");
                    var t = new GGs.Shared.Tweaks.TweakDefinition
                    {
                        Id = Guid.NewGuid(),
                        Name = $"Elevated Service {act}",
                        CommandType = GGs.Shared.Enums.CommandType.Service,
                        ServiceName = req.Service.Name,
                        ServiceAction = act,
                        AllowUndo = true,
                        RequiresAdmin = true
                    };
                    var log = TweakExecutor.Apply(t);
                    return WriteResponse(log.Success ? 0 : 1, log.Success ? "OK" : log.Error ?? "Error");
                }
                case "netshsetdns":
                {
                    if (req.Netsh == null || string.IsNullOrWhiteSpace(req.Netsh.InterfaceName) || string.IsNullOrWhiteSpace(req.Netsh.Dns))
                        return WriteResponse(1, "Missing netsh payload");
                    if (!ElevatedEntryHelpers.IsValidInterfaceName(req.Netsh.InterfaceName)) return WriteResponse(1, "Invalid interface name");
                    if (!ElevatedEntryHelpers.IsValidIpv4(req.Netsh.Dns)) return WriteResponse(1, "Invalid DNS IPv4");
                    var ifaceEsc = req.Netsh.InterfaceName.Replace("\"", string.Empty);
                    return ExecProcess("netsh", $"interface ip set dns \"{ifaceEsc}\" static {req.Netsh.Dns}");
                }
                default:
                    return WriteResponse(1, "Unsupported elevated action.");
            }
        }
        catch (Exception ex)
        {
            return WriteResponse(1, ex.Message);
        }
    }

    private static int ExecProcess(string file, string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = file,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi)!;
            var outText = p.StandardOutput.ReadToEnd();
            var errText = p.StandardError.ReadToEnd();
            p.WaitForExit();
            if (p.ExitCode == 0)
                return WriteResponse(0, string.IsNullOrWhiteSpace(outText) ? "OK" : outText);
            return WriteResponse(1, string.IsNullOrWhiteSpace(errText) ? outText : errText);
        }
        catch (Exception ex)
        {
            return WriteResponse(1, ex.Message);
        }
    }

    private static int WriteResponse(int code, string message)
    {
        var resp = new ElevatedResponse { Ok = code == 0, Message = message };
        Console.WriteLine(JsonSerializer.Serialize(resp));
        return code;
    }

    private static string? GetArgValue(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }
}

internal class ElevatedRequest
{
    public string? Type { get; set; }
    public string? Guid { get; set; }
    public int? TimeoutSeconds { get; set; }
    public RegistryPayload? Registry { get; set; }
    public ServicePayload? Service { get; set; }
    public NetshPayload? Netsh { get; set; }
}

internal class RegistryPayload
{
    public string? Path { get; set; }
    public string? Name { get; set; }
    public string? ValueType { get; set; }
    public string? Data { get; set; }
}

internal class ServicePayload
{
    public string? Name { get; set; }
    public string? Action { get; set; }
}

internal class NetshPayload
{
    public string? InterfaceName { get; set; }
    public string? Dns { get; set; }
}

internal class ElevatedResponse
{
    public bool Ok { get; set; }
    public string? Message { get; set; }
}

// Helpers for validation
internal static class ElevatedEntryHelpers
{
    internal static bool IsValidIpv4(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return false;
        var parts = ip.Split('.');
        if (parts.Length != 4) return false;
        foreach (var p in parts)
        {
            if (!int.TryParse(p, out var n)) return false;
            if (n < 0 || n > 255) return false;
        }
        return true;
    }
    internal static bool IsValidInterfaceName(string name)
    {
        foreach (var c in name)
        {
            if (!(char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_' )) return false;
        }
        return true;
    }
}

