using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading.Tasks;

namespace GGs.Desktop.Services;

public enum NetRiskLevel { Low, Medium, High }
public enum TcpAutotuningLevel { Normal, Disabled }

public sealed class NetworkProfile
{
    public string Name { get; set; } = string.Empty;
    public NetRiskLevel Risk { get; set; } = NetRiskLevel.Medium;

    // Per-adapter DNS configuration. If null/empty, DNS is not changed for that adapter.
    public Dictionary<string, string[]> DnsPerAdapter { get; set; } = new();

    // TCP autotuning global level
    public TcpAutotuningLevel? Autotuning { get; set; }

    // Additional TCP global options (e.g., rss, chimney)
    public Dictionary<string, string>? TcpGlobalOptions { get; set; }
}

public sealed class NetworkSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public List<AdapterSnapshot> Adapters { get; set; } = new();
    public Dictionary<string, string> TcpGlobal { get; set; } = new();

    public sealed class AdapterSnapshot
    {
        public string Name { get; set; } = string.Empty;
        public List<string> DnsServers { get; set; } = new();
    }
}

public sealed class NetworkApplyResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<string> Errors { get; set; } = new();
    public Guid? SnapshotId { get; set; }
}

public interface INetOpsRunner
{
    Task<(bool ok, string? message)> SetDnsAsync(string adapterName, string primaryDns);
    Task<(bool ok, string? message)> SetTcpAutotuningLevelAsync(TcpAutotuningLevel level);
    Task<(bool ok, string? message)> SetTcpGlobalOptionsAsync(Dictionary<string, string> options);
}

internal sealed class DefaultNetOpsRunner : INetOpsRunner
{
    private readonly bool _simulate;

    public DefaultNetOpsRunner()
    {
        _simulate = string.Equals(Environment.GetEnvironmentVariable("GGS_NETWORK_SIMULATE"), "1", StringComparison.Ordinal);
    }

    public Task<(bool ok, string? message)> SetDnsAsync(string adapterName, string primaryDns)
    {
        if (_simulate) return Task.FromResult(((bool ok, string? message))(true, "simulated"));
        return ElevationService.SetDnsAsync(adapterName, primaryDns);
    }

    public Task<(bool ok, string? message)> SetTcpAutotuningLevelAsync(TcpAutotuningLevel level)
    {
        if (_simulate) return Task.FromResult(((bool ok, string? message))(true, "simulated"));
        return level switch
        {
            TcpAutotuningLevel.Normal => ElevationService.TcpAutotuningNormalAsync(),
            TcpAutotuningLevel.Disabled => ElevationService.TcpAutotuningDisabledAsync(),
            _ => Task.FromResult(((bool ok, string? message))(false, "Unsupported level"))
        };
    }

    public Task<(bool ok, string? message)> SetTcpGlobalOptionsAsync(Dictionary<string, string> options)
    {
        if (_simulate) return Task.FromResult(((bool ok, string? message))(true, "simulated"));
        return ElevationService.NetshTcpGlobalAsync(options);
    }
}

public sealed class NetworkOptimizationService
{
    private readonly INetOpsRunner _runner;
    private readonly string _dataDir;

    public NetworkOptimizationService(INetOpsRunner? runner = null)
    {
        _runner = runner ?? new DefaultNetOpsRunner();
        var baseDirOverride = Environment.GetEnvironmentVariable("GGS_DATA_DIR");
        var baseDir = !string.IsNullOrWhiteSpace(baseDirOverride)
            ? baseDirOverride!
            : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _dataDir = Path.Combine(baseDir, "GGs", "network");
        Directory.CreateDirectory(_dataDir);
    }

    public bool IsSimulation => string.Equals(Environment.GetEnvironmentVariable("GGS_NETWORK_SIMULATE"), "1", StringComparison.Ordinal);

    public IReadOnlyList<string> GetActiveAdapterNames()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                            n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Select(n => n.Name)
                .Distinct()
                .ToList();
        }
        catch { return Array.Empty<string>(); }
    }

    public NetworkSnapshot CaptureSnapshot()
    {
        var snap = new NetworkSnapshot();
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;
                var ipProps = nic.GetIPProperties();
                var dns = ipProps?.DnsAddresses?.Select(a => a.ToString())?.ToList() ?? new List<string>();
                snap.Adapters.Add(new NetworkSnapshot.AdapterSnapshot { Name = nic.Name, DnsServers = dns });
            }
        }
        catch { }

        // Try to capture tcp global via netsh (non-elevated ok)
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "int tcp show global",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p != null)
            {
                var output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                var dict = ParseNetshKeyValues(output);
                snap.TcpGlobal = dict;
            }
        }
        catch { }

        return snap;
    }

    public async Task<NetworkApplyResult> ApplyProfileAsync(NetworkProfile profile, bool dryRun = false)
    {
        var activity = new System.Diagnostics.ActivitySource("GGs.Desktop.Network").StartActivity("network.apply_profile", System.Diagnostics.ActivityKind.Internal);
        activity?.SetTag("profile.name", profile.Name);
        activity?.SetTag("profile.risk", profile.Risk.ToString());
        try
        {
            if (profile.Risk == NetRiskLevel.High && !IsSimulation)
            {
                AppLogger.LogWarn($"Applying high-risk network profile '{profile.Name}'");
            }

            // Persist snapshot first for rollback
            var snapshot = CaptureSnapshot();
            var snapshotPath = SaveSnapshot(snapshot);

            if (dryRun)
            {
                return new NetworkApplyResult { Success = true, Message = "Dry run: no changes applied", SnapshotId = snapshot.Id };
            }

            // Apply DNS changes
            foreach (var kv in profile.DnsPerAdapter)
            {
                var adapter = kv.Key;
                var servers = kv.Value;
                if (servers == null || servers.Length == 0) continue;
                var primary = servers[0];
                var (ok, message) = await _runner.SetDnsAsync(adapter, primary);
                if (!ok)
                {
                    AppLogger.LogError($"Failed to set DNS for {adapter}: {message}");
                    await TryRollbackAsync(snapshot);
                    return new NetworkApplyResult { Success = false, Message = message ?? "Failed to set DNS", Errors = new List<string> { message ?? "dns failed" }, SnapshotId = snapshot.Id };
                }
            }

            // Apply autotuning
            if (profile.Autotuning.HasValue)
            {
                var (ok, message) = await _runner.SetTcpAutotuningLevelAsync(profile.Autotuning.Value);
                if (!ok)
                {
                    AppLogger.LogError($"Failed to set TCP autotuning: {message}");
                    await TryRollbackAsync(snapshot);
                    return new NetworkApplyResult { Success = false, Message = message ?? "Failed to set autotuning", Errors = new List<string> { message ?? "autotuning failed" }, SnapshotId = snapshot.Id };
                }
            }

            // Apply tcp globals
            if (profile.TcpGlobalOptions != null && profile.TcpGlobalOptions.Count > 0)
            {
                var (ok, message) = await _runner.SetTcpGlobalOptionsAsync(profile.TcpGlobalOptions);
                if (!ok)
                {
                    AppLogger.LogError($"Failed to set TCP globals: {message}");
                    await TryRollbackAsync(snapshot);
                    return new NetworkApplyResult { Success = false, Message = message ?? "Failed to set tcp global", Errors = new List<string> { message ?? "tcp global failed" }, SnapshotId = snapshot.Id };
                }
            }

            AppLogger.LogSuccess($"Network profile '{profile.Name}' applied successfully");
            return new NetworkApplyResult { Success = true, Message = "Applied", SnapshotId = snapshot.Id };
        }
        catch (Exception ex)
        {
            AppLogger.LogError($"Exception applying network profile '{profile.Name}'", ex);
            return new NetworkApplyResult { Success = false, Message = ex.Message, Errors = new List<string> { ex.Message } };
        }
        finally
        {
            activity?.Dispose();
        }
    }

    public async Task<bool> RollbackLastAsync()
    {
        var activity = new System.Diagnostics.ActivitySource("GGs.Desktop.Network").StartActivity("network.rollback_last", System.Diagnostics.ActivityKind.Internal);
        try
        {
            var last = GetLastSnapshotPath();
            if (last == null || !File.Exists(last)) return false;
            var json = await File.ReadAllTextAsync(last);
            var snap = JsonSerializer.Deserialize<NetworkSnapshot>(json);
            if (snap == null) return false;
            return await TryRollbackAsync(snap);
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Rollback failed", ex);
            return false;
        }
        finally
        {
            activity?.Dispose();
        }
    }

    private async Task<bool> TryRollbackAsync(NetworkSnapshot snapshot)
    {
        bool okAll = true;
        // Rollback DNS per adapter
        foreach (var a in snapshot.Adapters)
        {
            try
            {
                var primary = a.DnsServers.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(primary))
                {
                    var (ok, _) = await _runner.SetDnsAsync(a.Name, primary);
                    okAll &= ok;
                }
            }
            catch { okAll = false; }
        }
        // Rollback TCP autotuning if known
        if (snapshot.TcpGlobal.TryGetValue("Receive Window Auto-Tuning Level", out var level))
        {
            TcpAutotuningLevel target = level.Contains("normal", StringComparison.OrdinalIgnoreCase) ? TcpAutotuningLevel.Normal : TcpAutotuningLevel.Disabled;
            try { var (ok, _) = await _runner.SetTcpAutotuningLevelAsync(target); okAll &= ok; } catch { okAll = false; }
        }
        // We skip restoring other globals for safety unless explicitly supported
        if (!okAll)
        {
            AppLogger.LogWarn("One or more rollback steps failed. System may require manual review.");
        }
        else
        {
            AppLogger.LogSuccess("Network settings restored from snapshot");
        }
        return okAll;
    }

    private string SaveSnapshot(NetworkSnapshot snap)
    {
        try
        {
            Directory.CreateDirectory(_dataDir);
            var path = Path.Combine(_dataDir, $"snapshot_{snap.Id:N}_{snap.CreatedUtc:yyyyMMdd_HHmmss}.json");
            var json = JsonSerializer.Serialize(snap, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
            // Update pointer to last
            File.WriteAllText(Path.Combine(_dataDir, "last_snapshot.txt"), path);
            return path;
        }
        catch { return string.Empty; }
    }

    private string? GetLastSnapshotPath()
    {
        try
        {
            var p = Path.Combine(_dataDir, "last_snapshot.txt");
            if (File.Exists(p)) return File.ReadAllText(p).Trim();
        }
        catch { }
        return null;
    }

    private static Dictionary<string, string> ParseNetshKeyValues(string output)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var sr = new StringReader(output ?? string.Empty);
        string? line;
        while ((line = sr.ReadLine()) != null)
        {
            var idx = line.IndexOf(':');
            if (idx > 0)
            {
                var key = line.Substring(0, idx).Trim();
                var val = line.Substring(idx + 1).Trim();
                if (!string.IsNullOrWhiteSpace(key)) dict[key] = val;
            }
        }
        return dict;
    }
}

