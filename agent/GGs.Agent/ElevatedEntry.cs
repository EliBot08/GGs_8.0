using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace GGs.Agent;

/// <summary>
/// Elevated entry point for privileged operations.
/// Provides discrete, audited execution with structured logging and rollback tracking.
/// </summary>
internal static class ElevatedEntry
{
    private static readonly ActivitySource Activity = new("GGs.Agent.ElevatedEntry");

    internal static int Run(string[] args)
    {
        using var activity = Activity.StartActivity("elevated_operation");
        var startTime = DateTime.UtcNow;

        try
        {
            var payloadPath = GetArgValue(args, "--payload");
            if (string.IsNullOrWhiteSpace(payloadPath) || !File.Exists(payloadPath))
            {
                LogError("Missing or invalid payload path", activity);
                return WriteResponse(1, "Missing or invalid payload path.");
            }

            var json = File.ReadAllText(payloadPath, Encoding.UTF8);
            var req = JsonSerializer.Deserialize<ElevatedRequest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (req == null || string.IsNullOrWhiteSpace(req.Type))
            {
                LogError("Invalid payload", activity);
                return WriteResponse(1, "Invalid payload.");
            }

            activity?.SetTag("operation_type", req.Type);
            LogInfo($"Elevated operation started: Type={req.Type}", activity);

            int result;
            switch (req.Type.ToLowerInvariant())
            {
                case "flushdns":
                    LogInfo("Executing FlushDns", activity);
                    result = ExecProcess("ipconfig", "/flushdns", activity);
                    break;
                case "winsockreset":
                    LogInfo("Executing WinsockReset", activity);
                    result = ExecProcess("netsh", "winsock reset", activity);
                    break;
                case "tcpauthnormal":
                    LogInfo("Executing TcpAutotuningNormal", activity);
                    result = ExecProcess("netsh", "int tcp set global autotuninglevel=normal", activity);
                    break;
                case "powercfgsetactive":
                    if (!Guid.TryParse(req.Guid, out var g))
                    {
                        LogError("Invalid GUID for PowerCfgSetActive", activity);
                        return WriteResponse(1, "Invalid GUID.");
                    }
                    LogInfo($"Executing PowerCfgSetActive: {g}", activity);
                    result = ExecProcess("powercfg", $"/setactive {g}", activity);
                    break;
                case "bcdedittimeout":
                    if (req.TimeoutSeconds is null || req.TimeoutSeconds < 0 || req.TimeoutSeconds > 60)
                    {
                        LogError($"Invalid timeout: {req.TimeoutSeconds}", activity);
                        return WriteResponse(1, "Timeout out of range (0-60).");
                    }
                    LogInfo($"Executing BcdEditTimeout: {req.TimeoutSeconds}s", activity);
                    result = ExecProcess("bcdedit", $"/timeout {req.TimeoutSeconds}", activity);
                    break;
                case "registryset":
                {
                    if (req.Registry is null)
                    {
                        LogError("Missing registry payload", activity);
                        return WriteResponse(1, "Missing registry payload");
                    }
                    LogInfo($"Executing RegistrySet: {req.Registry.Path}\\{req.Registry.Name}", activity);
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
                    result = log.Success ? 0 : 1;
                    if (log.Success)
                        LogInfo($"RegistrySet succeeded: {req.Registry.Path}\\{req.Registry.Name}", activity);
                    else
                        LogError($"RegistrySet failed: {log.Error}", activity);
                    return WriteResponse(result, log.Success ? "OK" : log.Error ?? "Error");
                }
                case "serviceaction":
                {
                    if (req.Service is null)
                    {
                        LogError("Missing service payload", activity);
                        return WriteResponse(1, "Missing service payload");
                    }
                    if (!Enum.TryParse<GGs.Shared.Enums.ServiceAction>(req.Service.Action, true, out var act))
                    {
                        LogError($"Invalid service action: {req.Service.Action}", activity);
                        return WriteResponse(1, "Invalid service action");
                    }
                    LogInfo($"Executing ServiceAction: {req.Service.Name} - {act}", activity);
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
                    result = log.Success ? 0 : 1;
                    if (log.Success)
                        LogInfo($"ServiceAction succeeded: {req.Service.Name} - {act}", activity);
                    else
                        LogError($"ServiceAction failed: {log.Error}", activity);
                    return WriteResponse(result, log.Success ? "OK" : log.Error ?? "Error");
                }
                case "netshsetdns":
                {
                    if (req.Netsh == null || string.IsNullOrWhiteSpace(req.Netsh.InterfaceName) || string.IsNullOrWhiteSpace(req.Netsh.Dns))
                    {
                        LogError("Missing netsh payload", activity);
                        return WriteResponse(1, "Missing netsh payload");
                    }
                    if (!ElevatedEntryHelpers.IsValidInterfaceName(req.Netsh.InterfaceName))
                    {
                        LogError($"Invalid interface name: {req.Netsh.InterfaceName}", activity);
                        return WriteResponse(1, "Invalid interface name");
                    }
                    if (!ElevatedEntryHelpers.IsValidIpv4(req.Netsh.Dns))
                    {
                        LogError($"Invalid DNS IPv4: {req.Netsh.Dns}", activity);
                        return WriteResponse(1, "Invalid DNS IPv4");
                    }
                    var ifaceEsc = req.Netsh.InterfaceName.Replace("\"", string.Empty);
                    LogInfo($"Executing NetshSetDns: {ifaceEsc} -> {req.Netsh.Dns}", activity);
                    result = ExecProcess("netsh", $"interface ip set dns \"{ifaceEsc}\" static {req.Netsh.Dns}", activity);
                    break;
                }
                default:
                    LogError($"Unsupported operation type: {req.Type}", activity);
                    return WriteResponse(1, "Unsupported elevated action.");
            }

            var duration = DateTime.UtcNow - startTime;
            activity?.SetTag("duration_ms", duration.TotalMilliseconds);
            activity?.SetTag("exit_code", result);
            LogInfo($"Elevated operation completed: ExitCode={result} | Duration={duration.TotalMilliseconds}ms", activity);

            return result;
        }
        catch (Exception ex)
        {
            LogError($"Elevated operation failed: {ex.Message}", activity);
            activity?.SetTag("error", ex.Message);
            return WriteResponse(1, ex.Message);
        }
    }

    private static int ExecProcess(string file, string args, Activity? activity)
    {
        try
        {
            activity?.SetTag("process_file", file);
            activity?.SetTag("process_args", args);

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

            activity?.SetTag("process_exit_code", p.ExitCode);

            if (p.ExitCode == 0)
            {
                LogInfo($"Process succeeded: {file} {args}", activity);
                return WriteResponse(0, string.IsNullOrWhiteSpace(outText) ? "OK" : outText);
            }

            LogError($"Process failed: {file} {args} | ExitCode={p.ExitCode} | Error={errText}", activity);
            return WriteResponse(1, string.IsNullOrWhiteSpace(errText) ? outText : errText);
        }
        catch (Exception ex)
        {
            LogError($"Process execution exception: {file} {args} | {ex.Message}", activity);
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

    private static void LogInfo(string message, Activity? activity)
    {
        activity?.AddEvent(new ActivityEvent("info", tags: new ActivityTagsCollection { { "message", message } }));
        Console.Error.WriteLine($"[INFO] {DateTime.UtcNow:O} | {message}");
    }

    private static void LogError(string message, Activity? activity)
    {
        activity?.AddEvent(new ActivityEvent("error", tags: new ActivityTagsCollection { { "message", message } }));
        Console.Error.WriteLine($"[ERROR] {DateTime.UtcNow:O} | {message}");
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

