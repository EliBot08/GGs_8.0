using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.SignalR.Client;
using GGs.Shared.Models;

namespace GGs.Agent.Services;

/// <summary>
/// Real-time system monitoring service for enterprise-grade continuous data collection
/// </summary>
public class RealTimeMonitoringService : IDisposable
{
    private readonly ILogger<RealTimeMonitoringService> _logger;
    private readonly SystemInformationService _systemInfoService;
    private readonly HardwareDetectionService _hardwareDetectionService;
    private static readonly ActivitySource _activity = new("GGs.Agent.RealTimeMonitoring");

    private readonly Timer _monitoringTimer;
    private readonly PerformanceCounter _cpuCounter;
    private readonly PerformanceCounter _memoryCounter;
    private readonly PerformanceCounter _diskCounter;
    private readonly PerformanceCounter _networkCounter;
    
    private HubConnection? _hubConnection;
    private bool _isMonitoring = false;
    private bool _disposed = false;

    // P/Invoke for advanced system information
    [DllImport("kernel32.dll")]
    private static extern bool GetSystemTimes(out long idleTime, out long kernelTime, out long userTime);

    [DllImport("psapi.dll")]
    private static extern bool GetPerformanceInfo(out PERFORMANCE_INFORMATION pPerformanceInformation, uint cb);

    [StructLayout(LayoutKind.Sequential)]
    private struct PERFORMANCE_INFORMATION
    {
        public uint cb;
        public IntPtr CommitTotal;
        public IntPtr CommitLimit;
        public IntPtr CommitPeak;
        public IntPtr PhysicalTotal;
        public IntPtr PhysicalAvailable;
        public IntPtr SystemCache;
        public IntPtr KernelTotal;
        public IntPtr KernelPaged;
        public IntPtr KernelNonpaged;
        public IntPtr PageSize;
        public uint HandleCount;
        public uint ProcessCount;
        public uint ThreadCount;
    }

    public event EventHandler<RealTimeSystemData>? SystemDataUpdated;
    public event EventHandler<string>? MonitoringStatusChanged;

    public RealTimeMonitoringService(
        ILogger<RealTimeMonitoringService> logger,
        SystemInformationService systemInfoService,
        HardwareDetectionService hardwareDetectionService)
    {
        _logger = logger;
        _systemInfoService = systemInfoService;
        _hardwareDetectionService = hardwareDetectionService;

        // Initialize performance counters
        _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
        _diskCounter = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total");
        _networkCounter = new PerformanceCounter("Network Interface", "Bytes Total/sec", GetPrimaryNetworkInterface());

        // Initialize monitoring timer (updates every 2 seconds)
        _monitoringTimer = new Timer(CollectRealTimeData, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// Starts real-time monitoring with specified interval
    /// </summary>
    public async Task StartMonitoringAsync(TimeSpan interval, CancellationToken cancellationToken = default)
    {
        using var activity = _activity.StartActivity("monitoring.start");
        
        try
        {
            if (_isMonitoring)
            {
                _logger.LogWarning("Real-time monitoring is already running");
                return;
            }

            _logger.LogInformation("Starting real-time system monitoring with {Interval}ms interval", interval.TotalMilliseconds);

            // Initialize hub connection for real-time updates
            await InitializeHubConnectionAsync(cancellationToken);

            // Start monitoring timer
            _monitoringTimer.Change(TimeSpan.Zero, interval);
            _isMonitoring = true;

            MonitoringStatusChanged?.Invoke(this, "Real-time monitoring started");
            
            _logger.LogInformation("Real-time monitoring started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start real-time monitoring");
            activity?.SetTag("error", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Stops real-time monitoring
    /// </summary>
    public async Task StopMonitoringAsync()
    {
        using var activity = _activity.StartActivity("monitoring.stop");
        
        try
        {
            if (!_isMonitoring)
            {
                _logger.LogWarning("Real-time monitoring is not running");
                return;
            }

            _logger.LogInformation("Stopping real-time system monitoring");

            // Stop monitoring timer
            _monitoringTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _isMonitoring = false;

            // Disconnect hub
            if (_hubConnection != null)
            {
                await _hubConnection.DisposeAsync();
                _hubConnection = null;
            }

            MonitoringStatusChanged?.Invoke(this, "Real-time monitoring stopped");
            
            _logger.LogInformation("Real-time monitoring stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop real-time monitoring");
            activity?.SetTag("error", ex.Message);
        }
    }

    /// <summary>
    /// Collects comprehensive real-time system data
    /// </summary>
    private async void CollectRealTimeData(object? state)
    {
        if (!_isMonitoring || _disposed) return;

        using var activity = _activity.StartActivity("monitoring.collect");
        
        try
        {
            var systemData = new RealTimeSystemData
            {
                Timestamp = DateTime.UtcNow,
                DeviceId = GGs.Shared.Platform.DeviceIdHelper.GetStableDeviceId()
            };

            // Collect CPU metrics
            systemData.CpuMetrics = await CollectCpuMetricsAsync();
            
            // Collect memory metrics
            systemData.MemoryMetrics = await CollectMemoryMetricsAsync();
            
            // Collect disk metrics
            systemData.DiskMetrics = await CollectDiskMetricsAsync();
            
            // Collect network metrics
            systemData.NetworkMetrics = await CollectNetworkMetricsAsync();
            
            // Collect GPU metrics
            systemData.GpuMetrics = await CollectGpuMetricsAsync();
            
            // Collect thermal metrics
            systemData.ThermalMetrics = await CollectThermalMetricsAsync();
            
            // Collect process metrics
            systemData.ProcessMetrics = await CollectProcessMetricsAsync();
            
            // Collect system health metrics
            systemData.SystemHealth = await CollectSystemHealthAsync();

            // Raise event for local subscribers
            SystemDataUpdated?.Invoke(this, systemData);

            // Send to hub for real-time dashboard updates
            await SendToHubAsync(systemData);

            _logger.LogDebug("Real-time system data collected and distributed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect real-time system data");
            activity?.SetTag("error", ex.Message);
        }
    }

    private async Task<CpuMetrics> CollectCpuMetricsAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var cpuUsage = _cpuCounter.NextValue();
                
                // Get system times for more detailed CPU analysis
                GetSystemTimes(out var idleTime, out var kernelTime, out var userTime);
                
                return new CpuMetrics
                {
                    UsagePercent = cpuUsage,
                    IdleTime = idleTime,
                    KernelTime = kernelTime,
                    UserTime = userTime,
                    CoreCount = Environment.ProcessorCount,
                    ClockSpeed = GetCurrentCpuClockSpeed(),
                    Temperature = GetCpuTemperature(),
                    PowerConsumption = GetCpuPowerConsumption()
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect CPU metrics");
                return new CpuMetrics { UsagePercent = 0 };
            }
        });
    }

    private async Task<MemoryMetrics> CollectMemoryMetricsAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var availableMemoryMB = _memoryCounter.NextValue();
                
                // Get detailed memory information
                GetPerformanceInfo(out var perfInfo, (uint)Marshal.SizeOf<PERFORMANCE_INFORMATION>());
                
                var totalMemoryMB = (long)perfInfo.PhysicalTotal * (long)perfInfo.PageSize / (1024 * 1024);
                var usedMemoryMB = totalMemoryMB - (long)availableMemoryMB;
                var usagePercent = totalMemoryMB > 0 ? (double)usedMemoryMB / totalMemoryMB * 100 : 0;

                return new MemoryMetrics
                {
                    UsagePercent = usagePercent,
                    TotalMemoryMB = totalMemoryMB,
                    UsedMemoryMB = usedMemoryMB,
                    AvailableMemoryMB = (long)availableMemoryMB,
                    CommittedMemoryMB = (long)perfInfo.CommitTotal * (long)perfInfo.PageSize / (1024 * 1024),
                    CachedMemoryMB = (long)perfInfo.SystemCache * (long)perfInfo.PageSize / (1024 * 1024),
                    PageFaultRate = GetPageFaultRate(),
                    MemoryPressure = CalculateMemoryPressure(usagePercent)
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect memory metrics");
                return new MemoryMetrics { UsagePercent = 0 };
            }
        });
    }

    private async Task<DiskMetrics> CollectDiskMetricsAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var diskUsage = _diskCounter.NextValue();
                
                return new DiskMetrics
                {
                    UsagePercent = diskUsage,
                    ReadBytesPerSec = GetDiskReadBytesPerSec(),
                    WriteBytesPerSec = GetDiskWriteBytesPerSec(),
                    QueueLength = GetDiskQueueLength(),
                    ResponseTime = GetDiskResponseTime(),
                    Temperature = GetDiskTemperature(),
                    HealthStatus = GetDiskHealthStatus()
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect disk metrics");
                return new DiskMetrics { UsagePercent = 0 };
            }
        });
    }

    private async Task<NetworkMetrics> CollectNetworkMetricsAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var totalBytesPerSec = _networkCounter.NextValue();
                
                return new NetworkMetrics
                {
                    TotalBytesPerSec = totalBytesPerSec,
                    UploadBytesPerSec = GetNetworkUploadBytesPerSec(),
                    DownloadBytesPerSec = GetNetworkDownloadBytesPerSec(),
                    PacketsPerSec = GetNetworkPacketsPerSec(),
                    ErrorsPerSec = GetNetworkErrorsPerSec(),
                    ConnectionCount = GetActiveConnectionCount(),
                    Latency = GetNetworkLatency(),
                    SignalStrength = GetWirelessSignalStrength()
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect network metrics");
                return new NetworkMetrics { TotalBytesPerSec = 0 };
            }
        });
    }

    private async Task<GpuMetrics> CollectGpuMetricsAsync()
    {
        return await Task.Run(async () =>
        {
            try
            {
                // Get GPU information from hardware detection service
                var gpus = await _hardwareDetectionService.DetectAllGpuHardwareAsync();
                var primaryGpu = gpus.FirstOrDefault();
                
                if (primaryGpu == null)
                {
                    return new GpuMetrics { UsagePercent = 0 };
                }

                return new GpuMetrics
                {
                    UsagePercent = GetGpuUsagePercent(),
                    MemoryUsagePercent = GetGpuMemoryUsagePercent(),
                    Temperature = GetGpuTemperature(),
                    ClockSpeed = GetGpuClockSpeed(),
                    MemoryClockSpeed = GetGpuMemoryClockSpeed(),
                    PowerConsumption = GetGpuPowerConsumption(),
                    FanSpeed = GetGpuFanSpeed(),
                    VramUsageMB = GetGpuVramUsage()
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect GPU metrics");
                return new GpuMetrics { UsagePercent = 0 };
            }
        });
    }

    private async Task<ThermalMetrics> CollectThermalMetricsAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                return new ThermalMetrics
                {
                    CpuTemperature = GetCpuTemperature(),
                    GpuTemperature = GetGpuTemperature(),
                    MotherboardTemperature = GetMotherboardTemperature(),
                    AmbientTemperature = GetAmbientTemperature(),
                    CpuFanSpeed = GetCpuFanSpeed(),
                    SystemFanSpeed = GetSystemFanSpeed(),
                    ThermalThrottling = IsThermalThrottling(),
                    CoolingEfficiency = CalculateCoolingEfficiency()
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect thermal metrics");
                return new ThermalMetrics();
            }
        });
    }

    private async Task<ProcessMetrics> CollectProcessMetricsAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var processes = Process.GetProcesses();
                var topProcesses = processes
                    .Where(p => !p.HasExited)
                    .OrderByDescending(p => GetProcessCpuUsage(p))
                    .Take(10)
                    .Select(p => new ProcessInfo
                    {
                        Name = p.ProcessName,
                        ProcessId = p.Id,
                        CPUUsage = GetProcessCpuUsage(p),
                        MemoryUsage = (ulong)p.WorkingSet64,
                        Status = p.Responding ? "Running" : "Not Responding",
                        StartTime = p.StartTime,
                        ExecutablePath = GetProcessExecutablePath(p)
                    })
                    .ToList();

                return new ProcessMetrics
                {
                    TotalProcessCount = processes.Length,
                    TopProcesses = topProcesses,
                    TotalThreadCount = processes.Sum(p => p.Threads.Count),
                    TotalHandleCount = (ulong)processes.Sum(p => p.HandleCount),
                    SystemIdlePercent = GetSystemIdlePercent()
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect process metrics");
                return new ProcessMetrics();
            }
        });
    }

    private async Task<SystemHealth> CollectSystemHealthAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                return new SystemHealth
                {
                    OverallScore = CalculateOverallHealthScore(),
                    CpuHealth = CalculateCpuHealth(),
                    MemoryHealth = CalculateMemoryHealth(),
                    DiskHealth = CalculateDiskHealth(),
                    NetworkHealth = CalculateNetworkHealth(),
                    ThermalHealth = CalculateThermalHealth(),
                    PowerHealth = CalculatePowerHealth(),
                    SecurityHealth = CalculateSecurityHealth(),
                    Recommendations = GenerateHealthRecommendations()
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect system health metrics");
                return new SystemHealth();
            }
        });
    }

    // Helper methods (placeholder implementations)
    private string GetPrimaryNetworkInterface() => "Ethernet"; // Placeholder
    private double GetCurrentCpuClockSpeed() => 3800; // Placeholder
    private double GetCpuTemperature() => 45.0; // Placeholder
    private double GetCpuPowerConsumption() => 65.0; // Placeholder
    private double GetPageFaultRate() => 100.0; // Placeholder
    private string CalculateMemoryPressure(double usagePercent) => usagePercent > 80 ? "High" : "Normal"; // Placeholder
    private double GetDiskReadBytesPerSec() => 1024 * 1024; // Placeholder
    private double GetDiskWriteBytesPerSec() => 512 * 1024; // Placeholder
    private double GetDiskQueueLength() => 0.5; // Placeholder
    private double GetDiskResponseTime() => 5.0; // Placeholder
    private double GetDiskTemperature() => 35.0; // Placeholder
    private string GetDiskHealthStatus() => "Good"; // Placeholder
    private double GetNetworkUploadBytesPerSec() => 1024; // Placeholder
    private double GetNetworkDownloadBytesPerSec() => 2048; // Placeholder
    private double GetNetworkPacketsPerSec() => 100; // Placeholder
    private double GetNetworkErrorsPerSec() => 0; // Placeholder
    private int GetActiveConnectionCount() => 25; // Placeholder
    private double GetNetworkLatency() => 15.0; // Placeholder
    private int GetWirelessSignalStrength() => 85; // Placeholder
    private double GetGpuUsagePercent() => 25.0; // Placeholder
    private double GetGpuMemoryUsagePercent() => 40.0; // Placeholder
    private double GetGpuTemperature() => 55.0; // Placeholder
    private double GetGpuClockSpeed() => 1800; // Placeholder
    private double GetGpuMemoryClockSpeed() => 7000; // Placeholder
    private double GetGpuPowerConsumption() => 150.0; // Placeholder
    private int GetGpuFanSpeed() => 1500; // Placeholder
    private long GetGpuVramUsage() => 2048; // Placeholder
    private double GetMotherboardTemperature() => 40.0; // Placeholder
    private double GetAmbientTemperature() => 22.0; // Placeholder
    private int GetCpuFanSpeed() => 2000; // Placeholder
    private int GetSystemFanSpeed() => 1200; // Placeholder
    private bool IsThermalThrottling() => false; // Placeholder
    private double CalculateCoolingEfficiency() => 85.0; // Placeholder
    private double GetProcessCpuUsage(Process process) => 1.0; // Placeholder
    private string GetProcessExecutablePath(Process process) => ""; // Placeholder
    private double GetSystemIdlePercent() => 75.0; // Placeholder
    private double CalculateOverallHealthScore() => 85.0; // Placeholder
    private double CalculateCpuHealth() => 90.0; // Placeholder
    private double CalculateMemoryHealth() => 85.0; // Placeholder
    private double CalculateDiskHealth() => 80.0; // Placeholder
    private double CalculateNetworkHealth() => 95.0; // Placeholder
    private double CalculateThermalHealth() => 88.0; // Placeholder
    private double CalculatePowerHealth() => 92.0; // Placeholder
    private double CalculateSecurityHealth() => 75.0; // Placeholder
    private List<string> GenerateHealthRecommendations() => new() { "System running optimally" }; // Placeholder

    private async Task InitializeHubConnectionAsync(CancellationToken cancellationToken)
    {
        // Initialize SignalR hub connection for real-time updates
        // This would connect to the main application's hub
        _logger.LogDebug("Hub connection initialization skipped (placeholder)");
    }

    private async Task SendToHubAsync(RealTimeSystemData systemData)
    {
        // Send real-time data to connected clients via SignalR
        _logger.LogDebug("Sending real-time data to hub (placeholder)");
    }

    public void Dispose()
    {
        if (_disposed) return;

        _monitoringTimer?.Dispose();
        _cpuCounter?.Dispose();
        _memoryCounter?.Dispose();
        _diskCounter?.Dispose();
        _networkCounter?.Dispose();
        _hubConnection?.DisposeAsync();

        _disposed = true;
    }
}

// Data models for real-time monitoring
public class RealTimeSystemData
{
    public DateTime Timestamp { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public CpuMetrics CpuMetrics { get; set; } = new();
    public MemoryMetrics MemoryMetrics { get; set; } = new();
    public DiskMetrics DiskMetrics { get; set; } = new();
    public NetworkMetrics NetworkMetrics { get; set; } = new();
    public GpuMetrics GpuMetrics { get; set; } = new();
    public ThermalMetrics ThermalMetrics { get; set; } = new();
    public ProcessMetrics ProcessMetrics { get; set; } = new();
    public SystemHealth SystemHealth { get; set; } = new();
}

public class CpuMetrics
{
    public double UsagePercent { get; set; }
    public long IdleTime { get; set; }
    public long KernelTime { get; set; }
    public long UserTime { get; set; }
    public int CoreCount { get; set; }
    public double ClockSpeed { get; set; }
    public double Temperature { get; set; }
    public double PowerConsumption { get; set; }
}

public class MemoryMetrics
{
    public double UsagePercent { get; set; }
    public long TotalMemoryMB { get; set; }
    public long UsedMemoryMB { get; set; }
    public long AvailableMemoryMB { get; set; }
    public long CommittedMemoryMB { get; set; }
    public long CachedMemoryMB { get; set; }
    public double PageFaultRate { get; set; }
    public string MemoryPressure { get; set; } = string.Empty;
}

public class DiskMetrics
{
    public double UsagePercent { get; set; }
    public double ReadBytesPerSec { get; set; }
    public double WriteBytesPerSec { get; set; }
    public double QueueLength { get; set; }
    public double ResponseTime { get; set; }
    public double Temperature { get; set; }
    public string HealthStatus { get; set; } = string.Empty;
}

public class NetworkMetrics
{
    public double TotalBytesPerSec { get; set; }
    public double UploadBytesPerSec { get; set; }
    public double DownloadBytesPerSec { get; set; }
    public double PacketsPerSec { get; set; }
    public double ErrorsPerSec { get; set; }
    public int ConnectionCount { get; set; }
    public double Latency { get; set; }
    public int SignalStrength { get; set; }
}

public class GpuMetrics
{
    public double UsagePercent { get; set; }
    public double MemoryUsagePercent { get; set; }
    public double Temperature { get; set; }
    public double ClockSpeed { get; set; }
    public double MemoryClockSpeed { get; set; }
    public double PowerConsumption { get; set; }
    public int FanSpeed { get; set; }
    public long VramUsageMB { get; set; }
}

public class ThermalMetrics
{
    public double CpuTemperature { get; set; }
    public double GpuTemperature { get; set; }
    public double MotherboardTemperature { get; set; }
    public double AmbientTemperature { get; set; }
    public int CpuFanSpeed { get; set; }
    public int SystemFanSpeed { get; set; }
    public bool ThermalThrottling { get; set; }
    public double CoolingEfficiency { get; set; }
}

public class ProcessMetrics
{
    public int TotalProcessCount { get; set; }
    public List<ProcessInfo> TopProcesses { get; set; } = new();
    public int TotalThreadCount { get; set; }
    public ulong TotalHandleCount { get; set; }
    public double SystemIdlePercent { get; set; }
}

public class SystemHealth
{
    public double OverallScore { get; set; }
    public double CpuHealth { get; set; }
    public double MemoryHealth { get; set; }
    public double DiskHealth { get; set; }
    public double NetworkHealth { get; set; }
    public double ThermalHealth { get; set; }
    public double PowerHealth { get; set; }
    public double SecurityHealth { get; set; }
    public List<string> Recommendations { get; set; } = new();
}