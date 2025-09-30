using System;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace GGs.Desktop.Services;

public static class SecurityPolicyService
{
    public static string[] Modes => new[] { "strict", "moderate", "permissive" };

    public static string GetScriptMode()
    {
        try
        {
            // Prefer machine-level environment variable
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment", false);
            var val = key?.GetValue("GGS_SCRIPTS_MODE") as string;
            if (!string.IsNullOrWhiteSpace(val)) return Normalize(val);
        }
        catch { }
        var env = Environment.GetEnvironmentVariable("GGS_SCRIPTS_MODE", EnvironmentVariableTarget.User)
                  ?? Environment.GetEnvironmentVariable("GGS_SCRIPTS_MODE", EnvironmentVariableTarget.Process)
                  ?? Environment.GetEnvironmentVariable("GGS_SCRIPTS_MODE");
        return Normalize(env);
    }

    public static bool SetScriptMode(string mode, bool machineWide = true)
    {
        try
        {
            mode = Normalize(mode);
            if (machineWide)
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment", writable: true);
                if (key == null) return false;
                key.SetValue("GGS_SCRIPTS_MODE", mode, RegistryValueKind.String);
            }
            else
            {
                Environment.SetEnvironmentVariable("GGS_SCRIPTS_MODE", mode, EnvironmentVariableTarget.User);
            }
            BroadcastEnvironmentChange();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string Normalize(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return "moderate";
        v = v.Trim().ToLowerInvariant();
        return v switch
        {
            "strict" => "strict",
            "permissive" => "permissive",
            _ => "moderate"
        };
    }

    private static void BroadcastEnvironmentChange()
    {
        try
        {
            const int HWND_BROADCAST = 0xffff;
            const int WM_SETTINGCHANGE = 0x001A;
            SendMessageTimeout(new IntPtr(HWND_BROADCAST), WM_SETTINGCHANGE, IntPtr.Zero, "Environment",
                SendMessageTimeoutFlags.SMTO_NORMAL, 1000, out _);
        }
        catch { }
    }

    [DllImport("user32.dll", SetLastError = false, CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, int Msg, IntPtr wParam, string lParam,
        SendMessageTimeoutFlags fuFlags, uint uTimeout, out IntPtr lpdwResult);

    [Flags]
    private enum SendMessageTimeoutFlags : uint
    {
        SMTO_NORMAL = 0x0000,
        SMTO_BLOCK = 0x0001,
        SMTO_ABORTIFHUNG = 0x0002,
        SMTO_NOTIMEOUTIFNOTHUNG = 0x0008
    }
}
