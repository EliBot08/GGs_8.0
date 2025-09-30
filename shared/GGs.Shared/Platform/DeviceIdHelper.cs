using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using System.Runtime.Versioning;

namespace GGs.Shared.Platform;

public static class DeviceIdHelper
{
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static string GetStableDeviceId()
    {
        // Combine MachineGuid and baseboard serial if available, then hash
        var parts = new List<string>();
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Cryptography", false);
            var machineGuid = key?.GetValue("MachineGuid")?.ToString();
            if (!string.IsNullOrWhiteSpace(machineGuid)) parts.Add(machineGuid!);
        }
        catch { /* ignore */ }

        try
        {
            var baseBoard = WmiHelper.TryGetBaseBoardSerial();
            if (!string.IsNullOrWhiteSpace(baseBoard)) parts.Add(baseBoard!);
        }
        catch { /* ignore */ }

        if (parts.Count == 0)
        {
            parts.Add(Environment.MachineName);
        }

        var input = string.Join("|", parts);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash);
    }
}

internal static class WmiHelper
{
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static string? TryGetBaseBoardSerial()
    {
        try
        {
            // late-bind to avoid hard dependency for non-Windows tests
            var type = Type.GetType("System.Management.ManagementObjectSearcher, System.Management");
            if (type == null) return null;
            dynamic searcher = Activator.CreateInstance(type, new object[] { "SELECT SerialNumber FROM Win32_BaseBoard" })!;
            var collection = searcher.Get();
            foreach (var o in collection)
            {
                var mo = o; // dynamic
                var serial = mo["SerialNumber"]?.ToString();
                if (!string.IsNullOrWhiteSpace(serial)) return serial;
            }
        }
        catch
        {
            // ignore
        }
        return null;
    }
}
