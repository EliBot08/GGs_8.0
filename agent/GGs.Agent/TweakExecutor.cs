using System.Diagnostics;
using System.ServiceProcess;
using Microsoft.Win32;
using GGs.Shared.Tweaks;

namespace GGs.Agent;

public static class TweakExecutor
{
    private static readonly ActivitySource _activity = new("GGs.Agent.Tweak");
    public static TweakApplicationLog Apply(TweakDefinition tweak)
    {
        using var activity = _activity.StartActivity("agent.tweak.apply", ActivityKind.Internal);
        activity?.SetTag("tweak.id", tweak.Id);
        activity?.SetTag("tweak.name", tweak.Name);
        activity?.SetTag("tweak.type", tweak.CommandType.ToString());
        var log = new TweakApplicationLog
        {
            TweakId = tweak.Id,
            TweakName = tweak.Name,
            DeviceId = Shared.Platform.DeviceIdHelper.GetStableDeviceId(),
            AppliedUtc = DateTime.UtcNow,
        };

        try
        {
            activity?.AddEvent(new ActivityEvent("restore_point.create"));
            CreateRestorePointSafe($"GGs - {tweak.Name}");
            switch (tweak.CommandType)
            {
                case GGs.Shared.Enums.CommandType.Registry:
                {
                    // Security: allow writes only to HKCU/HKLM
                    if (!IsRegistryRootAllowed(tweak.RegistryPath))
                    {
                        log.Error = "Registry path not allowed by policy (only HKCU/HKLM permitted).";
                        log.Success = false;
                        break;
                    }
                    log.RegistryPath = tweak.RegistryPath;
                    log.RegistryValueName = tweak.RegistryValueName;
                    log.BeforeState = GetRegistryStateJson(tweak.RegistryPath!, tweak.RegistryValueName!);

                    try { SetRegistryValue(tweak); }
                    catch (Exception rex) { log.Error = rex.ToString(); }

                    log.AfterState = GetRegistryStateJson(tweak.RegistryPath!, tweak.RegistryValueName!);

                    // Populate enriched fields from structured states
                    if (TweakStateSerializer.TryDeserialize(log.BeforeState, out var b) && b is RegistryState rb)
                    {
                        log.RegistryValueType = rb.ValueType;
                        log.OriginalValue = rb.Data;
                    }
                    if (TweakStateSerializer.TryDeserialize(log.AfterState, out var a) && a is RegistryState ra)
                    {
                        log.NewValue = ra.Data;
                        if (string.IsNullOrWhiteSpace(log.RegistryValueType) || log.RegistryValueType == "Unknown" || log.RegistryValueType == "<error>")
                            log.RegistryValueType = ra.ValueType;
                    }

                    log.Success = string.IsNullOrEmpty(log.Error);
                    if (!string.IsNullOrWhiteSpace(log.Error)) activity?.SetTag("tweak.error", Trim(log.Error));
                    break;
                }
                case GGs.Shared.Enums.CommandType.Service:
                {
                    log.ServiceName = tweak.ServiceName;
                    log.BeforeState = GetServiceStateJson(tweak.ServiceName!);
                    try { ApplyServiceAction(tweak.ServiceName!, tweak.ServiceAction!.Value); }
                    catch (InvalidOperationException pol) { log.Error = pol.Message; }
                    catch (Exception sex) { log.Error = sex.ToString(); }
                    // include which action we applied for redo/undo
                    var after = GetServiceState(tweak.ServiceName!);
                    after.ActionApplied = tweak.ServiceAction!.Value.ToString();
                    log.ActionApplied = after.ActionApplied;
                    log.AfterState = TweakStateSerializer.Serialize(after);
                    log.Success = string.IsNullOrEmpty(log.Error);
                    if (!string.IsNullOrWhiteSpace(log.Error)) activity?.SetTag("tweak.error", Trim(log.Error));
                    break;
                }
                case GGs.Shared.Enums.CommandType.Script:
                {
                    var (ok, output, policyDecision) = RunPowerShell(tweak.ScriptContent ?? string.Empty);
                    log.BeforeState = null;
                    var s = new ScriptState
                    {
                        Output = output,
                        UndoAvailable = !string.IsNullOrWhiteSpace(tweak.UndoScriptContent),
                        ScriptApplied = tweak.ScriptContent,
                        UndoScript = tweak.UndoScriptContent
                    };
                    log.ScriptApplied = tweak.ScriptContent;
                    log.UndoScript = tweak.UndoScriptContent;
                    log.AfterState = TweakStateSerializer.Serialize(s);
                    log.Success = ok;
                    if (!ok) log.Error = output;
                    if (!ok) activity?.SetTag("tweak.error", Trim(output));

                    // Attach policy decision to log
                    if (policyDecision != null)
                    {
                        log.ReasonCode = policyDecision.ReasonCode;
                        log.PolicyDecision = policyDecision.Decision;
                        activity?.SetTag("policy.mode", policyDecision.PolicyMode.ToString());
                        activity?.SetTag("policy.allowed", policyDecision.Allowed);
                        if (!string.IsNullOrEmpty(policyDecision.BlockedPattern))
                        {
                            activity?.SetTag("policy.blocked_pattern", policyDecision.BlockedPattern);
                        }
                    }

                    return log;
                }
                default:
                    throw new NotSupportedException($"Unsupported command type: {tweak.CommandType}");
            }
            // Success is set per-branch; if not set yet, consider it success
            if (!log.Success && string.IsNullOrEmpty(log.Error)) log.Success = true;
        }
        catch (Exception ex)
        {
            log.Success = false;
            log.Error = ex.ToString();
            activity?.SetTag("tweak.error", Trim(log.Error));
        }

        activity?.SetTag("tweak.success", log.Success);
        return log;
    }

    private static string Trim(string s) => s.Length > 512 ? s.Substring(0, 512) + "..." : s;

    private static void CreateRestorePointSafe(string description)
    {
        try
        {
            var type = Type.GetType("System.Management.ManagementClass, System.Management");
            if (type == null) return;
            dynamic sysRestore = Activator.CreateInstance(type, new object[] { "SystemRestore" })!;
            sysRestore.Scope = new System.Management.ManagementScope("\\\\.\\root\\default");
            // 0: Application install, 10: Device driver install, 12: Modify settings
            sysRestore.InvokeMethod("CreateRestorePoint", new object[] { description, 12, 100 });
        }
        catch
        {
            // Ignore if not supported / disabled
        }
    }

    private static string GetRegistryStateJson(string path, string name)
    {
        try
        {
            var (root, sub) = SplitRegistryPath(path);
            using var key = root.OpenSubKey(sub, false);
            var val = key?.GetValue(name);
            var kind = key?.GetValueKind(name).ToString();
            var state = new RegistryState
            {
                Path = path,
                Name = name,
                ValueType = kind ?? "Unknown",
                Data = val?.ToString() ?? "<null>"
            };
            return TweakStateSerializer.Serialize(state);
        }
        catch
        {
            return TweakStateSerializer.Serialize(new RegistryState { Path = path, Name = name, ValueType = "<error>", Data = "<error>" });
        }
    }

    private static void SetRegistryValue(TweakDefinition t)
    {
        var (root, sub) = SplitRegistryPath(t.RegistryPath!);
        using var key = root.CreateSubKey(sub, true)!;
        var kind = ParseRegistryValueKind(t.RegistryValueType);
        object data = kind switch
        {
            RegistryValueKind.DWord => Convert.ToInt32(t.RegistryValueData, 16),
            RegistryValueKind.QWord => Convert.ToInt64(t.RegistryValueData, 16),
            RegistryValueKind.MultiString => (object)(t.RegistryValueData?.Split(";", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>()),
            RegistryValueKind.Binary => Convert.FromHexString(t.RegistryValueData ?? string.Empty),
            _ => t.RegistryValueData ?? string.Empty
        };
        // Idempotency: skip write if current value already equals desired (and type matches)
        try
        {
            var existingKind = key.GetValueKind(t.RegistryValueName!);
            var existingVal = key.GetValue(t.RegistryValueName!);
            if (existingKind == kind && ValuesEqual(existingVal, data, kind))
            {
                return; // no-op
            }
        }
        catch
        {
            // missing value or type fetch failed; proceed to set
        }
        key.SetValue(t.RegistryValueName!, data, kind);
    }

    private static bool ValuesEqual(object? a, object? b, RegistryValueKind kind)
    {
        try
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return kind switch
            {
                RegistryValueKind.MultiString => string.Join("\n", (string[])a) == string.Join("\n", (string[])b),
                RegistryValueKind.Binary => ((byte[])a).SequenceEqual((byte[])b),
                _ => string.Equals(Convert.ToString(a), Convert.ToString(b), StringComparison.Ordinal)
            };
        }
        catch { return false; }
    }

    private static (bool ok, string output, ScriptPolicy.PolicyDecision? policyDecision) RunPowerShell(string script)
    {
        try
        {
            // Evaluate script against policy with detailed decision tracking
            var policyDecision = ScriptPolicy.Evaluate(script);

            if (!policyDecision.Allowed)
            {
                var errorMessage = $"Script blocked by policy: {policyDecision.Decision}";
                return (false, errorMessage, policyDecision);
            }

            string encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi)!;
            var stdOut = p.StandardOutput.ReadToEnd();
            var stdErr = p.StandardError.ReadToEnd();
            p.WaitForExit();
            if (p.ExitCode == 0) return (true, stdOut, policyDecision);
            return (false, string.IsNullOrWhiteSpace(stdErr) ? stdOut : stdErr, policyDecision);
        }
        catch (Exception ex)
        {
            return (false, ex.ToString(), null);
        }
    }

    private static ServiceState GetServiceState(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            return new ServiceState { ServiceName = serviceName, Status = $"{sc.Status}" };
        }
        catch { return new ServiceState { ServiceName = serviceName, Status = "<notfound>" }; }
    }

    private static string GetServiceStateJson(string serviceName)
    {
        var state = GetServiceState(serviceName);
        return TweakStateSerializer.Serialize(state);
    }

    private static void ApplyServiceAction(string serviceName, GGs.Shared.Enums.ServiceAction action)
    {
        // Security: block high-risk services from Stop/Disable
        var blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "WinDefend", "wuauserv", "TrustedInstaller", "RpcSs", "EventLog", "Winmgmt", "LSM", "TermService", "LanmanWorkstation", "LanmanServer"
        };
        if ((action == GGs.Shared.Enums.ServiceAction.Stop || action == GGs.Shared.Enums.ServiceAction.Disable) && blocked.Contains(serviceName))
        {
            throw new InvalidOperationException($"Service action blocked by policy: {serviceName} {action}");
        }
        using var sc = new ServiceController(serviceName);
        switch (action)
        {
            case GGs.Shared.Enums.ServiceAction.Start:
                if (sc.Status != ServiceControllerStatus.Running) { sc.Start(); sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15)); }
                break;
            case GGs.Shared.Enums.ServiceAction.Stop:
                if (sc.Status != ServiceControllerStatus.Stopped) { sc.Stop(); sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15)); }
                break;
            case GGs.Shared.Enums.ServiceAction.Restart:
                if (sc.Status == ServiceControllerStatus.Running) { sc.Stop(); sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15)); }
                sc.Start(); sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
                break;
            case GGs.Shared.Enums.ServiceAction.Enable:
                ScConfig(serviceName, "auto");
                break;
            case GGs.Shared.Enums.ServiceAction.Disable:
                ScConfig(serviceName, "disabled");
                break;
        }
    }

    private static void ScConfig(string serviceName, string startType)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = $"config \"{serviceName}\" start= {startType}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi)!;
        p.WaitForExit();
    }

    private static bool IsRegistryRootAllowed(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var root = path.Split('\\', 2)[0].ToUpperInvariant();
        return root is "HKCU" or "HKEY_CURRENT_USER" or "HKLM" or "HKEY_LOCAL_MACHINE";
    }

    private static (RegistryKey root, string subkey) SplitRegistryPath(string path)
    {
        var parts = path.Split('\\', 2);
        var rootName = parts[0].ToUpperInvariant();
        var sub = parts.Length > 1 ? parts[1].TrimStart('\\') : string.Empty;
        return rootName switch
        {
            "HKLM" or "HKEY_LOCAL_MACHINE" => (Registry.LocalMachine, sub),
            "HKCU" or "HKEY_CURRENT_USER" => (Registry.CurrentUser, sub),
            "HKCR" or "HKEY_CLASSES_ROOT" => (Registry.ClassesRoot, sub),
            "HKU" or "HKEY_USERS" => (Registry.Users, sub),
            "HKCC" or "HKEY_CURRENT_CONFIG" => (Registry.CurrentConfig, sub),
            _ => (Registry.CurrentUser, path)
        };
    }

    private static RegistryValueKind ParseRegistryValueKind(string? kind)
    {
        return (kind?.ToLowerInvariant()) switch
        {
            "string" or "sz" => RegistryValueKind.String,
            "expandstring" or "expandsz" => RegistryValueKind.ExpandString,
            "dword" => RegistryValueKind.DWord,
            "qword" => RegistryValueKind.QWord,
            "multistring" => RegistryValueKind.MultiString,
            "binary" => RegistryValueKind.Binary,
            _ => RegistryValueKind.String
        };
    }
}
