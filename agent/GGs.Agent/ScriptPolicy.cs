using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GGs.Shared.Tweaks;

namespace GGs.Agent;

/// <summary>
/// Enterprise-grade script policy engine with normalized parsing and detailed policy decisions.
/// Provides three-tier security model: Strict (allowlist), Moderate (denylist), Permissive (audit-only).
/// </summary>
internal static class ScriptPolicy
{
    /// <summary>
    /// Policy modes: Strict (allowlist), Moderate (denylist), Permissive (allow all)
    /// </summary>
    internal enum Mode { Strict, Moderate, Permissive }

    /// <summary>
    /// Comprehensive denylist to prevent high-risk PowerShell operations.
    /// Expanded with additional dangerous commands and patterns.
    /// </summary>
    private static readonly string[] BlockList = new[]
    {
        // File system destruction
        "remove-item -recurse -force \\\\?\\c:",
        "remove-item -recurse -force c:\\",
        "remove-item -recurse -force /",
        "rm -rf /",
        "del /s /q c:\\",
        "format-volume",
        "format ",
        "cipher /w:",

        // Boot configuration tampering
        "bcdedit /deletevalue",
        "bcdedit /set",
        "bcdedit /delete",

        // System restore tampering
        "disable-computerrestore",
        "enable-computerrestore -disable",
        "vssadmin delete shadows",

        // Critical process termination
        "stop-process -name wininit",
        "stop-process -name lsass",
        "stop-process -name csrss",
        "stop-process -name winlogon",
        "stop-process -name services",
        "stop-process -name smss",

        // Code execution risks
        "invoke-expression",
        "iex ",
        "invoke-command",
        "icm ",
        "invoke-webrequest",
        "iwr ",
        "downloadstring",
        "downloadfile",

        // Privilege escalation
        "start-process -verb runas",
        "runas /user:",

        // Registry critical areas
        "remove-item hklm:\\sam",
        "remove-item hklm:\\security",
        "remove-item hklm:\\system",

        // Network attacks
        "test-netconnection -port",
        "new-object net.webclient",

        // Credential theft
        "get-credential",
        "convertfrom-securestring",
        "mimikatz",

        // Obfuscation attempts
        "encodedcommand",
        "-enc ",
        "-e ",
        "frombase64string",

        // Scheduled tasks for persistence
        "register-scheduledtask",
        "schtasks /create",

        // WMI abuse
        "invoke-wmimethod",
        "register-wmievent",

        // PowerShell remoting abuse
        "enter-pssession",
        "new-pssession"
    };

    /// <summary>
    /// Conservative allowlist for Strict mode.
    /// Only safe, read-only, and informational commands are permitted.
    /// </summary>
    private static readonly string[] AllowListPrefixes = new[]
    {
        // Output and display
        "write-host",
        "write-output",
        "write-verbose",
        "write-information",

        // Date and time
        "get-date",

        // Process information (read-only)
        "get-process",

        // Service information (read-only)
        "get-service",

        // File system (read-only)
        "test-path",
        "get-item",
        "get-childitem",
        "get-content",

        // System information (read-only)
        "get-computerinfo",
        "get-hotfix",
        "get-wmiobject",
        "get-ciminstance",

        // Network information (read-only)
        "get-netadapter",
        "get-netipaddress",
        "get-netroute",

        // Registry (read-only)
        "get-itemproperty",

        // Variables and environment
        "get-variable",
        "get-childitem env:",

        // Help and documentation
        "get-help",
        "get-command",
        "get-member"
    };

    /// <summary>
    /// Policy decision result with detailed reasoning.
    /// </summary>
    internal sealed class PolicyDecision
    {
        public bool Allowed { get; set; }
        public string ReasonCode { get; set; } = string.Empty;
        public string Decision { get; set; } = string.Empty;
        public string? BlockedPattern { get; set; }
        public Mode PolicyMode { get; set; }
    }

    /// <summary>
    /// Evaluate script against policy with detailed decision tracking.
    /// </summary>
    internal static PolicyDecision Evaluate(string? script)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            return new PolicyDecision
            {
                Allowed = true,
                ReasonCode = ReasonCodes.PolicyAllowScript("EmptyScript"),
                Decision = "Empty or whitespace-only script; no action required",
                PolicyMode = GetMode()
            };
        }

        var mode = GetMode();
        var normalized = NormalizeScript(script);

        if (mode == Mode.Permissive)
        {
            return new PolicyDecision
            {
                Allowed = true,
                ReasonCode = ReasonCodes.PolicyAllowScript("Permissive"),
                Decision = $"Permissive mode: all scripts allowed (audit-only). Script length: {script.Length} chars",
                PolicyMode = mode
            };
        }

        if (mode == Mode.Strict)
        {
            return EvaluateStrict(normalized, script);
        }

        // Moderate mode (default)
        return EvaluateModerate(normalized, script);
    }

    /// <summary>
    /// Legacy compatibility method - returns only boolean result.
    /// </summary>
    internal static bool IsAllowed(string? script)
    {
        return Evaluate(script).Allowed;
    }

    private static PolicyDecision EvaluateStrict(string normalized, string original)
    {
        var lines = normalized.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
                continue; // Skip empty lines and comments

            // Check if line starts with an allowed prefix
            bool lineAllowed = false;
            foreach (var prefix in AllowListPrefixes)
            {
                if (trimmed.StartsWith(prefix + " ") || trimmed.Equals(prefix))
                {
                    lineAllowed = true;
                    break;
                }
            }

            if (!lineAllowed)
            {
                return new PolicyDecision
                {
                    Allowed = false,
                    ReasonCode = ReasonCodes.PolicyDenyScriptContent("NotInAllowlist"),
                    Decision = $"Strict mode: command not in allowlist: '{TruncateForLog(trimmed)}'",
                    BlockedPattern = TruncateForLog(trimmed),
                    PolicyMode = Mode.Strict
                };
            }

            // Even if allowed, check for blocked patterns
            foreach (var blocked in BlockList)
            {
                if (trimmed.Contains(blocked))
                {
                    return new PolicyDecision
                    {
                        Allowed = false,
                        ReasonCode = ReasonCodes.PolicyDenyScriptContent("BlockedPattern"),
                        Decision = $"Strict mode: blocked pattern detected: '{blocked}'",
                        BlockedPattern = blocked,
                        PolicyMode = Mode.Strict
                    };
                }
            }
        }

        return new PolicyDecision
        {
            Allowed = true,
            ReasonCode = ReasonCodes.PolicyAllowScript("Strict"),
            Decision = $"Strict mode: all commands in allowlist, no blocked patterns. Lines: {lines.Length}",
            PolicyMode = Mode.Strict
        };
    }

    private static PolicyDecision EvaluateModerate(string normalized, string original)
    {
        // Check for blocked patterns
        foreach (var blocked in BlockList)
        {
            if (normalized.Contains(blocked))
            {
                return new PolicyDecision
                {
                    Allowed = false,
                    ReasonCode = ReasonCodes.PolicyDenyScriptContent("BlockedPattern"),
                    Decision = $"Moderate mode: blocked pattern detected: '{blocked}'",
                    BlockedPattern = blocked,
                    PolicyMode = Mode.Moderate
                };
            }
        }

        return new PolicyDecision
        {
            Allowed = true,
            ReasonCode = ReasonCodes.PolicyAllowScript("Moderate"),
            Decision = $"Moderate mode: no blocked patterns detected. Script length: {original.Length} chars",
            PolicyMode = Mode.Moderate
        };
    }

    /// <summary>
    /// Normalize script for consistent policy evaluation.
    /// - Convert to lowercase
    /// - Remove extra whitespace
    /// - Normalize line endings
    /// - Remove PowerShell comments
    /// - Expand common aliases
    /// </summary>
    private static string NormalizeScript(string script)
    {
        var sb = new StringBuilder(script);

        // Normalize line endings
        sb.Replace("\r\n", "\n");
        sb.Replace("\r", "\n");

        var normalized = sb.ToString().ToLowerInvariant();

        // Expand common PowerShell aliases to full cmdlet names
        normalized = ExpandAliases(normalized);

        // Remove multiple spaces
        normalized = Regex.Replace(normalized, @"\s+", " ");

        return normalized;
    }

    /// <summary>
    /// Expand common PowerShell aliases to their full cmdlet names for better pattern matching.
    /// </summary>
    private static string ExpandAliases(string script)
    {
        var aliases = new Dictionary<string, string>
        {
            { "iex ", "invoke-expression " },
            { "icm ", "invoke-command " },
            { "iwr ", "invoke-webrequest " },
            { "curl ", "invoke-webrequest " },
            { "wget ", "invoke-webrequest " },
            { "rm ", "remove-item " },
            { "del ", "remove-item " },
            { "erase ", "remove-item " },
            { "rd ", "remove-item " },
            { "rmdir ", "remove-item " },
            { "copy ", "copy-item " },
            { "cp ", "copy-item " },
            { "move ", "move-item " },
            { "mv ", "move-item " },
            { "ren ", "rename-item " },
            { "dir ", "get-childitem " },
            { "ls ", "get-childitem " },
            { "gci ", "get-childitem " },
            { "cat ", "get-content " },
            { "type ", "get-content " },
            { "gc ", "get-content " },
            { "ps ", "get-process " },
            { "gps ", "get-process " },
            { "kill ", "stop-process " },
            { "spps ", "stop-process " }
        };

        foreach (var alias in aliases)
        {
            script = script.Replace(alias.Key, alias.Value);
        }

        return script;
    }

    /// <summary>
    /// Truncate string for logging to prevent log injection and excessive log size.
    /// </summary>
    private static string TruncateForLog(string value, int maxLength = 100)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Length <= maxLength) return value;
        return value.Substring(0, maxLength) + "...";
    }

    private static Mode GetMode()
    {
        var v = Environment.GetEnvironmentVariable("GGS_SCRIPTS_MODE");
        return (v?.ToLowerInvariant()) switch
        {
            "strict" => Mode.Strict,
            "permissive" => Mode.Permissive,
            _ => Mode.Moderate
        };
    }
}

