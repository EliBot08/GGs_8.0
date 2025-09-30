using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace GGs.Desktop.Services;

public sealed class ElevationService
{
    private static readonly string[] RiskyOps = new[] { "bcdedittimeout", "winsockreset" };

    public static async Task<(bool ok, string? message)> FlushDnsAsync()
        => await RunElevatedAsync(new { Type = "FlushDns" });

    public static async Task<(bool ok, string? message)> WinsockResetAsync()
        => await RunElevatedAsync(new { Type = "WinsockReset" }, requireConsent: true, consentText: "Resetting Winsock will require a restart and can affect network configuration. Continue?");

    public static async Task<(bool ok, string? message)> TcpAutotuningNormalAsync()
        => await RunElevatedAsync(new { Type = "TcpAuthNormal" });

    public static async Task<(bool ok, string? message)> TcpAutotuningDisabledAsync()
        => await RunElevatedAsync(new { Type = "TcpAuthDisabled" });

    public static async Task<(bool ok, string? message)> NetshTcpGlobalAsync(Dictionary<string, string> options)
        => await RunElevatedAsync(new { Type = "NetshTcpGlobal", Options = options });

    public static async Task<(bool ok, string? message)> PowerCfgSetActiveAsync(Guid scheme)
        => await RunElevatedAsync(new { Type = "PowerCfgSetActive", Guid = scheme.ToString() });

    public static async Task<(bool ok, string? message)> BcdeditTimeoutAsync(int seconds)
        => await RunElevatedAsync(new { Type = "BcdeditTimeout", TimeoutSeconds = seconds }, requireConsent: true, consentText: "Changing boot timeout modifies bootloader configuration. Continue?");

    public static async Task<(bool ok, string? message)> SetDnsAsync(string interfaceName, string dns)
        => await RunElevatedAsync(new { Type = "NetshSetDns", Netsh = new { InterfaceName = interfaceName, Dns = dns } });

    private static async Task<(bool ok, string? message)> RunElevatedAsync(object payload, bool requireConsent = false, string? consentText = null)
    {
        try
        {
            if (requireConsent)
            {
                var res = MessageBox.Show(consentText ?? "This operation requires administrator privileges.", "GGs", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                if (res != MessageBoxResult.OK) return (false, "Cancelled by user");
            }

            // Locate Agent
            var agentExe = TryFindAgentExe();
            var agentDll = TryFindAgentDll();
            if (agentExe == null && agentDll == null)
                return (false, "Agent not found for elevation.");

            // Write payload to temp file
            var tmp = Path.Combine(Path.GetTempPath(), $"ggs_elev_{Guid.NewGuid():N}.json");
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            await File.WriteAllTextAsync(tmp, json, Encoding.UTF8);

            var psi = new ProcessStartInfo
            {
                UseShellExecute = true, // required for Verb runas
                Verb = "runas",
                CreateNoWindow = true
            };

            if (agentExe != null)
            {
                psi.FileName = agentExe;
                psi.Arguments = $"--elevated --payload \"{tmp}\"";
            }
            else
            {
                psi.FileName = "dotnet";
                psi.Arguments = $"\"{agentDll}\" --elevated --payload \"{tmp}\"";
            }

            // For CI/tests, allow simulating without UAC prompt
            if (Environment.GetEnvironmentVariable("GGS_ELEVATION_SIMULATE") == "1")
            {
                psi.Verb = string.Empty;
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
            }

            using var proc = Process.Start(psi);
            if (proc == null) return (false, "Failed to start elevated process");

            string? output = null;
            if (!psi.UseShellExecute)
            {
                output = await proc.StandardOutput.ReadToEndAsync();
            }
            proc.WaitForExit();

            // Parse optional JSON response from stdout (simulation mode)
            if (!string.IsNullOrWhiteSpace(output))
            {
                try
                {
                    var resp = JsonSerializer.Deserialize<ElevatedResponse>(output.Trim());
                    return (resp?.Ok == true, resp?.Message);
                }
                catch { }
            }

            return (proc.ExitCode == 0, proc.ExitCode == 0 ? "OK" : $"ExitCode {proc.ExitCode}");
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // ERROR_CANCELLED from UAC prompt
            return (false, "User cancelled elevation");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static string? TryFindAgentExe()
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var candidate = Path.Combine(baseDir, "GGs.Agent.exe");
            if (File.Exists(candidate)) return candidate;
            return null;
        }
        catch { return null; }
    }

    private static string? TryFindAgentDll()
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var candidate = Path.Combine(baseDir, "GGs.Agent.dll");
            if (File.Exists(candidate)) return candidate;
            // Dev fallback: search repo agent bin
            var dev = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "GGs", "agent", "GGs.Agent", "bin", "Debug", "net8.0", "GGs.Agent.dll");
            if (File.Exists(dev)) return dev;
            return null;
        }
        catch { return null; }
    }

    private class ElevatedResponse
    {
        public bool Ok { get; set; }
        public string? Message { get; set; }
    }
}

