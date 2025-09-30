using System;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Timers;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GGs.Desktop.Services;

public class SystemMonitorService
{
    private PerformanceCounter? _cpuCounter;
    private PerformanceCounter? _ramCounter;
    private PerformanceCounter? _diskCounter;
    private IGpuProvider _gpuProvider = new WindowsCounterGpuProvider();
    private System.Timers.Timer? _updateTimer;
    
    public event EventHandler<SystemStatsEventArgs>? StatsUpdated;
    
    public SystemMonitorService()
    {
        try
        {
            // Initialize performance counters
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _ramCounter = new PerformanceCounter("Memory", "Available MBytes");
            try { _diskCounter = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total"); } catch { _diskCounter = null; }
        }
        catch (Exception ex)
        {
            // If performance counters fail to initialize, the app can still run with simulated data
            Debug.WriteLine($"Performance counter initialization failed: {ex.Message}");
        }
        
        if (AppConfig.DemoMode)
        {
            try { AppLogger.LogInfo("SystemMonitorService running in DemoMode: some metrics will be simulated 🧪"); } catch { }
        }
        
        // Initialize timer
        _updateTimer = new System.Timers.Timer(2000); // Update every 2 seconds
        _updateTimer.Elapsed += async (sender, e) => await UpdateStats();
    }
    
    public void Start()
    {
        _updateTimer?.Start();
        Task.Run(async () => await UpdateStats()); // Initial update
    }
    
    public void Stop()
    {
        _updateTimer?.Stop();
        _cpuCounter?.Dispose();
        _ramCounter?.Dispose();
    }
    
    private async Task UpdateStats()
    {
        try
        {
            var stats = new SystemStats
            {
                CpuUsage = GetCpuUsage(),
                GpuUsage = await GetGpuUsageAsync(),
                RamUsage = GetRamUsage(),
                NetworkLatency = GetNetworkLatency(),
                TotalRam = GetTotalRam(),
                AvailableRam = GetAvailableRam(),
                ProcessCount = GetProcessCount(),
                Temperature = GetCpuTemperature(),
                DiskUsage = GetDiskUsage()
            };
            
            StatsUpdated?.Invoke(this, new SystemStatsEventArgs { Stats = stats });
        }
        catch (Exception ex)
        {
            // Log error but don't crash
            Console.WriteLine($"Error updating stats: {ex.Message}");
        }
    }

    private float GetDiskUsage()
    {
        try
        {
            if (_diskCounter != null)
            {
                // The first call may return 0; call twice if needed in caller if precise values required
                var v = _diskCounter.NextValue();
                if (v < 0) v = 0; if (v > 100) v = 100; return v;
            }
        }
        catch { }
        return 0;
    }
    
    private float GetCpuUsage()
    {
        try
        {
            return _cpuCounter?.NextValue() ?? 0;
        }
        catch
        {
            return 0;
        }
    }
    
    private async Task<float> GetGpuUsageAsync()
    {
        try
        {
            var val = await _gpuProvider.GetGpuUsageAsync();
            if (val >= 0) return val;
        }
        catch { }
        // Fallback if provider not available or fails
        return AppConfig.DemoMode ? new Random().Next(20, 60) : 0;
    }
    
    private RamInfo GetRamUsage()
    {
        try
        {
            var totalRam = GetTotalRam();
            var availableRam = _ramCounter?.NextValue() ?? 8192;
            var usedRam = (totalRam - availableRam) / 1024; // Convert to GB
            var percentage = ((totalRam / 1024 - usedRam) / (totalRam / 1024)) * 100;
            
            return new RamInfo
            {
                UsedGB = Math.Round(usedRam, 1),
                TotalGB = Math.Round(totalRam / 1024, 1),
                UsagePercent = Math.Round(100 - percentage, 0)
            };
        }
        catch
        {
            return new RamInfo { UsedGB = 0, TotalGB = 16, UsagePercent = 0 };
        }
    }
    private float GetTotalRam()
    {
        try
        {
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    var totalRam = Convert.ToDouble(obj["TotalPhysicalMemory"]);
                    return (float)(totalRam / (1024 * 1024)); // Convert to MB
                }
            }
        }
        catch { }
        // As a fallback, use GC info
        try { return (float)(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024)); } catch { }
        return AppConfig.DemoMode ? 16384 : 0; // Default in demo; 0 in production unknown
    }

    private float GetAvailableRam()
    {
        try
        {
            return _ramCounter?.NextValue() ?? 8192;
        }
        catch
        {
            return 8192; // Default 8GB available
        }
    }

    private int GetNetworkLatency()
    {
        try
        {
            using (var ping = new Ping())
            {
                var reply = ping.Send("8.8.8.8", 1500); // 1.5s timeout
                if (reply.Status == IPStatus.Success)
                {
                    return (int)reply.RoundtripTime;
                }
            }
        }
        catch { }
        return 0;
    }

    private int GetProcessCount()
    {
        try
        {
            return Process.GetProcesses().Length;
        }
        catch
        {
            return 0;
        }
    }

    private float GetCpuTemperature()
    {
        try
        {
            using (var searcher = new ManagementObjectSearcher(@"root\\WMI", "SELECT * FROM MSAcpi_ThermalZoneTemperature"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    var temp = Convert.ToDouble(obj["CurrentTemperature"]);
                    // Convert from tenths of Kelvin to Celsius
                    return (float)((temp - 2732) / 10.0);
                }
            }
        }
        catch { }

        // Return a simulated value if can't get real temperature
        return AppConfig.DemoMode ? new Random().Next(45, 75) : 0;
    }

    // ----------------------------------------------------------------
    // Methods expected by DashboardView (demo implementations)
    // ----------------------------------------------------------------

    public Task<SystemOverviewStatistics> GetSystemStatisticsAsync()
    {
        // Demo values; in a real implementation, aggregate telemetry or server data
        var rnd = new Random();
        var stats = new SystemOverviewStatistics
        {
            ActiveUsers = rnd.Next(1200, 2500),
            PreviousActiveUsers = rnd.Next(800, 1500),
            TweaksApplied = rnd.Next(5000, 10000),
            PreviousTweaksApplied = rnd.Next(4000, 9000),
            AveragePerformanceGain = Math.Round(rnd.NextDouble() * 0.30, 3), // 0-30%
            PreviousPerformanceGain = Math.Round(rnd.NextDouble() * 0.30, 3),
            EliBotQueries = rnd.Next(200, 1200),
            PreviousEliBotQueries = rnd.Next(150, 1000)
        };
        return Task.FromResult(stats);
    }

    public Task<IReadOnlyList<ActivityItem>> GetRecentActivitiesAsync(int count)
    {
        var now = DateTime.Now;
        var list = new List<ActivityItem>();
        for (int i = 0; i < Math.Max(1, count); i++)
        {
            list.Add(new ActivityItem
            {
                Title = i % 3 == 0 ? "Optimization Applied" : (i % 3 == 1 ? "EliBot Answered" : "Profile Updated"),
                Description = i % 3 == 0 ? "Applied Quick Optimize to improve responsiveness" : (i % 3 == 1 ? "Answered a user question about FPS stutter" : "Updated Gaming profile settings"),
                Timestamp = now.AddMinutes(-i * 7),
                Severity = i % 4 == 0 ? ActivitySeverity.Warning : ActivitySeverity.Info
            });
        }
        return Task.FromResult((IReadOnlyList<ActivityItem>)list);
    }

    public async Task RunQuickOptimizationAsync()
    {
        // Simulate some work and log via AppLogger
        try { AppLogger.LogInfo("Running quick optimization..."); } catch { }
        await Task.Delay(1200);
        try { AppLogger.LogInfo("Quick optimization finished."); } catch { }
    }

    public async Task<SystemScanResults> RunSystemScanAsync()
    {
        // Simulated scan results
        await Task.Delay(1500);
        return new SystemScanResults
        {
            IssuesFound = 3,
            Recommendations = new[]
            {
                "Disable unnecessary startup apps",
                "Adjust power plan to High Performance",
                "Update graphics drivers"
            }
        };
    }
}

public class SystemStats
{
    public float CpuUsage { get; set; }
    public float GpuUsage { get; set; }
    public RamInfo RamUsage { get; set; } = new RamInfo();
    public int NetworkLatency { get; set; }
    public float TotalRam { get; set; }
    public float AvailableRam { get; set; }
    public int ProcessCount { get; set; }
    public float Temperature { get; set; }
    public float DiskUsage { get; set; }
}

public class RamInfo
{
    public double UsedGB { get; set; }
    public double TotalGB { get; set; }
    public double UsagePercent { get; set; }
}

public class SystemStatsEventArgs : EventArgs
{
    public SystemStats Stats { get; set; } = new SystemStats();
}

// ----------------------------------------------------------------
// Simple DTOs used by DashboardView expectations
// ----------------------------------------------------------------

public class SystemOverviewStatistics
{
    public int ActiveUsers { get; set; }
    public int PreviousActiveUsers { get; set; }
    public int TweaksApplied { get; set; }
    public int PreviousTweaksApplied { get; set; }
    public double AveragePerformanceGain { get; set; }
    public double PreviousPerformanceGain { get; set; }
    public int EliBotQueries { get; set; }
    public int PreviousEliBotQueries { get; set; }
}

public enum ActivitySeverity { Info, Warning, Error }

public class ActivityItem
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime Timestamp { get; set; }
    public ActivitySeverity Severity { get; set; } = ActivitySeverity.Info;
}

public class SystemScanResults
{
    public int IssuesFound { get; set; }
    public IEnumerable<string> Recommendations { get; set; } = Array.Empty<string>();
}
