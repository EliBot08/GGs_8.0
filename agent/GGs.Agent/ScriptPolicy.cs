using System;
using System.Collections.Generic;
using System.Linq;

namespace GGs.Agent;

internal static class ScriptPolicy
{
    // Policy modes: Strict (allowlist), Moderate (denylist), Permissive (allow all)
    private enum Mode { Strict, Moderate, Permissive }

    // Basic denylist to prevent high-risk PowerShell operations
    private static readonly string[] BlockList = new[]
    {
        "remove-item -recurse -force \\\\?\\c:",
        "remove-item -recurse -force c:\\",
        "format-volume",
        "format ",
        "cipher /w:",
        "bcdedit /deletevalue",
        "disable-computerrestore",
        "enable-computerrestore -disable" ,
        "stop-process -name wininit",
        "stop-process -name lsass",
        "stop-process -name csrss",
        "invoke-expression",
        "iex ",
        "start-process -verb runas"
    };

    // Conservative allowlist for Strict mode (expand cautiously)
    private static readonly string[] AllowListPrefixes = new[]
    {
        "write-host",
        "get-date",
        "get-process",
        "get-service",
        "test-path",
        "get-item",
        "get-childitem"
    };

    internal static bool IsAllowed(string? script)
    {
        if (string.IsNullOrWhiteSpace(script)) return true; // nothing to do
        var mode = GetMode();
        var s = script.Trim();
        var sl = s.ToLowerInvariant();

        if (mode == Mode.Permissive)
            return true;

        if (mode == Mode.Strict)
        {
            // Allowed if any line starts with an allowlisted prefix
            foreach (var line in s.Split(new[] {'\r','\n'}, StringSplitOptions.RemoveEmptyEntries))
            {
                var tl = line.TrimStart().ToLowerInvariant();
                if (AllowListPrefixes.Any(pref => tl.StartsWith(pref + " ") || tl.Equals(pref)))
                {
                    // also ensure it does not contain a denylisted token
                    if (!BlockList.Any(b => tl.Contains(b))) return true;
                }
            }
            return false;
        }

        // Moderate: block known risky tokens
        return !BlockList.Any(b => sl.Contains(b));
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

