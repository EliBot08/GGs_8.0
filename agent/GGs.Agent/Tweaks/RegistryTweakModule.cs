using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using GGs.Shared.Tweaks;

namespace GGs.Agent.Tweaks;

/// <summary>
/// Registry Tweak Module: typed writers/readers, preflight validation (existence/type/policy),
/// atomic apply, verify, rollback; detailed Before -> After diffs in audit.
/// </summary>
public sealed class RegistryTweakModule : ITweakModule
{
    private readonly ILogger<RegistryTweakModule> _logger;
    private readonly ActivitySource _activity = new("GGs.Agent.Tweak.Registry");

    // Policy: only allow HKCU and HKLM for safety
    private static readonly HashSet<string> AllowedRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        "HKCU", "HKEY_CURRENT_USER", "HKLM", "HKEY_LOCAL_MACHINE"
    };

    // Policy: block critical system keys
    private static readonly HashSet<string> BlockedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        @"HKLM\SYSTEM\CurrentControlSet\Services\WinDefend",
        @"HKLM\SYSTEM\CurrentControlSet\Services\EventLog",
        @"HKLM\SYSTEM\CurrentControlSet\Services\RpcSs",
        @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
        @"HKLM\SOFTWARE\Policies\Microsoft\Windows Defender"
    };

    public string ModuleName => "Registry";

    public RegistryTweakModule(ILogger<RegistryTweakModule> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<TweakPreflightResult> PreflightAsync(
        TweakDefinition tweak,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activity.StartActivity("registry.preflight");
        activity?.SetTag("tweak.id", tweak.Id);
        activity?.SetTag("registry.path", tweak.RegistryPath);

        try
        {
            // Validate path
            if (string.IsNullOrWhiteSpace(tweak.RegistryPath))
            {
                return Task.FromResult(new TweakPreflightResult
                {
                    CanApply = false,
                    Reason = "Registry path is required",
                    ValidationError = "RegistryPath is null or empty",
                    BeforeState = "{}"
                });
            }

            // Check policy: allowed root
            var root = tweak.RegistryPath.Split('\\', 2)[0];
            if (!AllowedRoots.Contains(root))
            {
                return Task.FromResult(new TweakPreflightResult
                {
                    CanApply = false,
                    Reason = "Registry root not allowed by policy",
                    PolicyViolation = $"Only HKCU and HKLM are permitted. Got: {root}",
                    BeforeState = "{}"
                });
            }

            // Check policy: blocked paths
            if (BlockedPaths.Any(blocked => tweak.RegistryPath.StartsWith(blocked, StringComparison.OrdinalIgnoreCase)))
            {
                return Task.FromResult(new TweakPreflightResult
                {
                    CanApply = false,
                    Reason = "Registry path blocked by policy",
                    PolicyViolation = $"Path is in blocked list: {tweak.RegistryPath}",
                    BeforeState = "{}"
                });
            }

            // Validate value name
            if (string.IsNullOrWhiteSpace(tweak.RegistryValueName))
            {
                return Task.FromResult(new TweakPreflightResult
                {
                    CanApply = false,
                    Reason = "Registry value name is required",
                    ValidationError = "RegistryValueName is null or empty",
                    BeforeState = "{}"
                });
            }

            // Validate value type
            var valueKind = ParseRegistryValueKind(tweak.RegistryValueType);
            if (valueKind == RegistryValueKind.Unknown)
            {
                return Task.FromResult(new TweakPreflightResult
                {
                    CanApply = false,
                    Reason = "Invalid registry value type",
                    ValidationError = $"Unknown value type: {tweak.RegistryValueType}",
                    BeforeState = "{}"
                });
            }

            // Get current state
            var beforeState = GetRegistryState(tweak.RegistryPath, tweak.RegistryValueName);
            var beforeJson = TweakStateSerializer.Serialize(beforeState);

            // Check permissions by attempting to open key
            try
            {
                var (rootKey, subKey) = SplitRegistryPath(tweak.RegistryPath);
                using var key = rootKey.OpenSubKey(subKey, writable: true);
                if (key == null)
                {
                    // Key doesn't exist - we'll need to create it
                    _logger.LogInformation("Registry key does not exist, will be created: {Path}", tweak.RegistryPath);
                }
            }
            catch (UnauthorizedAccessException)
            {
                return Task.FromResult(new TweakPreflightResult
                {
                    CanApply = false,
                    Reason = "Insufficient permissions to modify registry key",
                    PermissionIssue = $"Access denied to: {tweak.RegistryPath}",
                    BeforeState = beforeJson
                });
            }

            return Task.FromResult(new TweakPreflightResult
            {
                CanApply = true,
                Reason = "Preflight validation passed",
                BeforeState = beforeJson
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registry preflight failed: {Path}", tweak.RegistryPath);
            return Task.FromResult(new TweakPreflightResult
            {
                CanApply = false,
                Reason = "Preflight validation failed",
                ValidationError = ex.Message,
                BeforeState = "{}"
            });
        }
    }

    public Task<TweakApplicationResult> ApplyAsync(
        TweakDefinition tweak,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activity.StartActivity("registry.apply");
        activity?.SetTag("tweak.id", tweak.Id);
        activity?.SetTag("registry.path", tweak.RegistryPath);
        activity?.SetTag("registry.value", tweak.RegistryValueName);

        var appliedAt = DateTime.UtcNow;

        try
        {
            if (string.IsNullOrWhiteSpace(tweak.RegistryPath) || string.IsNullOrWhiteSpace(tweak.RegistryValueName))
            {
                throw new ArgumentException("Registry path and value name are required");
            }

            // Get before state
            var beforeState = GetRegistryState(tweak.RegistryPath, tweak.RegistryValueName);
            var beforeJson = TweakStateSerializer.Serialize(beforeState);

            // Apply the change
            SetRegistryValue(tweak);

            // Get after state
            var afterState = GetRegistryState(tweak.RegistryPath, tweak.RegistryValueName);
            var afterJson = TweakStateSerializer.Serialize(afterState);

            // Generate detailed diff
            var diff = GenerateDiff(beforeState, afterState);

            _logger.LogInformation(
                "Registry value applied: {Path}\\{Name} | Before: {Before} | After: {After}",
                tweak.RegistryPath, tweak.RegistryValueName, beforeState.Data, afterState.Data);

            activity?.SetTag("success", true);

            return Task.FromResult(new TweakApplicationResult
            {
                Success = true,
                BeforeState = beforeJson,
                AfterState = afterJson,
                AppliedAtUtc = appliedAt,
                DetailedDiff = diff
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registry apply failed: {Path}\\{Name}",
                tweak.RegistryPath, tweak.RegistryValueName);

            activity?.SetTag("success", false);
            activity?.SetTag("error", ex.Message);

            return Task.FromResult(new TweakApplicationResult
            {
                Success = false,
                BeforeState = "{}",
                AfterState = "{}",
                Error = ex.ToString(),
                AppliedAtUtc = appliedAt
            });
        }
    }

    public Task<TweakVerificationResult> VerifyAsync(
        TweakDefinition tweak,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activity.StartActivity("registry.verify");
        activity?.SetTag("tweak.id", tweak.Id);

        try
        {
            var currentState = GetRegistryState(tweak.RegistryPath!, tweak.RegistryValueName!);
            var currentJson = TweakStateSerializer.Serialize(currentState);

            // Expected state based on tweak definition
            var expectedData = tweak.RegistryValueData ?? string.Empty;
            var expectedType = tweak.RegistryValueType ?? "String";

            var verified = (currentState.ValueType?.Equals(expectedType, StringComparison.OrdinalIgnoreCase) ?? false) &&
                          (currentState.Data?.Equals(expectedData, StringComparison.Ordinal) ?? false);

            var discrepancy = verified ? null :
                $"Expected: {expectedType}={expectedData}, Got: {currentState.ValueType}={currentState.Data}";

            return Task.FromResult(new TweakVerificationResult
            {
                Verified = verified,
                CurrentState = currentJson,
                ExpectedState = $"{{\"Type\":\"{expectedType}\",\"Data\":\"{expectedData}\"}}",
                Discrepancy = discrepancy
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registry verification failed: {Path}\\{Name}",
                tweak.RegistryPath, tweak.RegistryValueName);

            return Task.FromResult(new TweakVerificationResult
            {
                Verified = false,
                CurrentState = "{}",
                ExpectedState = "{}",
                Discrepancy = $"Verification error: {ex.Message}"
            });
        }
    }

    public Task<TweakRollbackResult> RollbackAsync(
        TweakApplicationLog applicationLog,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activity.StartActivity("registry.rollback");
        activity?.SetTag("tweak.id", applicationLog.TweakId);

        var rolledBackAt = DateTime.UtcNow;

        try
        {
            if (!TweakStateSerializer.TryDeserialize(applicationLog.BeforeState, out var state) ||
                state is not RegistryState beforeState)
            {
                throw new InvalidOperationException("Cannot deserialize before state for rollback");
            }

            if (string.IsNullOrWhiteSpace(beforeState.Path) || string.IsNullOrWhiteSpace(beforeState.Name))
            {
                throw new InvalidOperationException("Invalid registry state: path or name is null");
            }

            // Restore the previous value
            var (rootKey, subKey) = SplitRegistryPath(beforeState.Path);
            using var key = rootKey.OpenSubKey(subKey, writable: true);

            if (key == null)
            {
                throw new InvalidOperationException($"Registry key not found: {beforeState.Path}");
            }

            if (beforeState.Data == "<null>")
            {
                // Value didn't exist before, delete it
                key.DeleteValue(beforeState.Name, throwOnMissingValue: false);
            }
            else
            {
                // Restore previous value
                var valueKind = ParseRegistryValueKind(beforeState.ValueType);
                object data = ConvertToRegistryData(beforeState.Data ?? string.Empty, valueKind);
                key.SetValue(beforeState.Name, data, valueKind);
            }

            var restoredState = GetRegistryState(beforeState.Path, beforeState.Name);
            var restoredJson = TweakStateSerializer.Serialize(restoredState);

            _logger.LogInformation("Registry value rolled back: {Path}\\{Name}",
                beforeState.Path, beforeState.Name);

            return Task.FromResult(new TweakRollbackResult
            {
                Success = true,
                RestoredState = restoredJson,
                RolledBackAtUtc = rolledBackAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registry rollback failed: {TweakId}", applicationLog.TweakId);

            return Task.FromResult(new TweakRollbackResult
            {
                Success = false,
                RestoredState = "{}",
                Error = ex.ToString(),
                RolledBackAtUtc = rolledBackAt
            });
        }
    }

    // Helper methods
    private static RegistryState GetRegistryState(string path, string name)
    {
        try
        {
            var (rootKey, subKey) = SplitRegistryPath(path);
            using var key = rootKey.OpenSubKey(subKey, writable: false);

            if (key == null)
            {
                return new RegistryState
                {
                    Path = path,
                    Name = name,
                    ValueType = "Unknown",
                    Data = "<key_not_found>"
                };
            }

            var value = key.GetValue(name);
            if (value == null)
            {
                return new RegistryState
                {
                    Path = path,
                    Name = name,
                    ValueType = "Unknown",
                    Data = "<null>"
                };
            }

            var valueKind = key.GetValueKind(name);
            var data = FormatRegistryData(value, valueKind);

            return new RegistryState
            {
                Path = path,
                Name = name,
                ValueType = valueKind.ToString(),
                Data = data
            };
        }
        catch (Exception ex)
        {
            return new RegistryState
            {
                Path = path,
                Name = name,
                ValueType = "<error>",
                Data = $"<error: {ex.Message}>"
            };
        }
    }

    private static void SetRegistryValue(TweakDefinition tweak)
    {
        var (rootKey, subKey) = SplitRegistryPath(tweak.RegistryPath!);
        using var key = rootKey.CreateSubKey(subKey, writable: true) ??
            throw new InvalidOperationException($"Failed to create/open registry key: {tweak.RegistryPath}");

        var valueKind = ParseRegistryValueKind(tweak.RegistryValueType);
        var data = ConvertToRegistryData(tweak.RegistryValueData ?? string.Empty, valueKind);

        // Idempotency check: skip if value already matches
        try
        {
            var existingKind = key.GetValueKind(tweak.RegistryValueName!);
            var existingValue = key.GetValue(tweak.RegistryValueName!);

            if (existingKind == valueKind && ValuesEqual(existingValue, data, valueKind))
            {
                return; // Already set to desired value
            }
        }
        catch
        {
            // Value doesn't exist or can't be read, proceed with set
        }

        key.SetValue(tweak.RegistryValueName!, data, valueKind);
    }

    private static bool ValuesEqual(object? a, object? b, RegistryValueKind kind)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;

        try
        {
            return kind switch
            {
                RegistryValueKind.MultiString => string.Join("\n", (string[])a) == string.Join("\n", (string[])b),
                RegistryValueKind.Binary => ((byte[])a).SequenceEqual((byte[])b),
                _ => string.Equals(Convert.ToString(a), Convert.ToString(b), StringComparison.Ordinal)
            };
        }
        catch
        {
            return false;
        }
    }

    private static string FormatRegistryData(object value, RegistryValueKind kind)
    {
        return kind switch
        {
            RegistryValueKind.DWord => $"0x{Convert.ToInt32(value):X8}",
            RegistryValueKind.QWord => $"0x{Convert.ToInt64(value):X16}",
            RegistryValueKind.MultiString => string.Join(";", (string[])value),
            RegistryValueKind.Binary => Convert.ToHexString((byte[])value),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static object ConvertToRegistryData(string data, RegistryValueKind kind)
    {
        return kind switch
        {
            RegistryValueKind.DWord => Convert.ToInt32(data.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? data[2..] : data, data.StartsWith("0x") ? 16 : 10),
            RegistryValueKind.QWord => Convert.ToInt64(data.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? data[2..] : data, data.StartsWith("0x") ? 16 : 10),
            RegistryValueKind.MultiString => data.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            RegistryValueKind.Binary => Convert.FromHexString(data),
            _ => data
        };
    }

    private static string GenerateDiff(RegistryState before, RegistryState after)
    {
        var changes = new List<string>();

        if (before.ValueType != after.ValueType)
        {
            changes.Add($"Type: {before.ValueType} → {after.ValueType}");
        }

        if (before.Data != after.Data)
        {
            changes.Add($"Data: {before.Data} → {after.Data}");
        }

        return changes.Count > 0 ? string.Join(" | ", changes) : "No changes";
    }

    private static (RegistryKey rootKey, string subKey) SplitRegistryPath(string path)
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
            _ => throw new ArgumentException($"Unknown registry root: {rootName}", nameof(path))
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
            _ => RegistryValueKind.Unknown
        };
    }
}

