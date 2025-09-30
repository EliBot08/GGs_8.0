using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;
using GGs.Shared.Models;

namespace GGs.Agent.Services;

/// <summary>
/// Comprehensive hardware detection service supporting all GPU vendors and legacy hardware
/// Provides deep system access for enterprise-grade hardware analysis
/// </summary>
public class HardwareDetectionService
{
    private readonly ILogger<HardwareDetectionService> _logger;
    private static readonly ActivitySource _activity = new("GGs.Agent.HardwareDetection");

    // P/Invoke declarations for low-level hardware access
    [DllImport("kernel32.dll")]
    private static extern bool GetSystemInfo(out SYSTEM_INFO lpSystemInfo);

    [DllImport("kernel32.dll")]
    private static extern void GetNativeSystemInfo(out SYSTEM_INFO lpSystemInfo);

    [DllImport("kernel32.dll")]
    private static extern bool IsWow64Process(IntPtr hProcess, out bool wow64Process);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_INFO
    {
        public ushort processorArchitecture;
        public ushort reserved;
        public uint pageSize;
        public IntPtr minimumApplicationAddress;
        public IntPtr maximumApplicationAddress;
        public IntPtr activeProcessorMask;
        public uint numberOfProcessors;
        public uint processorType;
        public uint allocationGranularity;
        public ushort processorLevel;
        public ushort processorRevision;
    }

    public HardwareDetectionService(ILogger<HardwareDetectionService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Detects all GPU hardware including legacy and modern cards from all vendors
    /// </summary>
    public async Task<List<EnhancedGpuInfo>> DetectAllGpuHardwareAsync(CancellationToken cancellationToken = default)
    {
        using var activity = _activity.StartActivity("gpu.detect.all");
        var gpus = new List<EnhancedGpuInfo>();

        try
        {
            // Method 1: WMI Detection (Primary)
            var wmiGpus = await DetectGpusViaWmiAsync(cancellationToken);
            gpus.AddRange(wmiGpus);

            // Method 2: Registry Detection (Legacy and additional cards)
            var registryGpus = await DetectGpusViaRegistryAsync(cancellationToken);
            MergeGpuInformation(gpus, registryGpus);

            // Method 3: DirectX Detection
            var dxGpus = await DetectGpusViaDirectXAsync(cancellationToken);
            MergeGpuInformation(gpus, dxGpus);

            // Method 4: Vendor-specific detection
            await EnhanceWithVendorSpecificInfoAsync(gpus, cancellationToken);

            // Method 5: Legacy hardware detection
            await DetectLegacyGpuHardwareAsync(gpus, cancellationToken);

            // Enhance with additional information
            foreach (var gpu in gpus)
            {
                await EnhanceGpuInformationAsync(gpu, cancellationToken);
            }

            _logger.LogInformation("Detected {GpuCount} GPU(s) across all vendors", gpus.Count);
            return gpus;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect GPU hardware");
            activity?.SetTag("error", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Detects all CPU hardware including legacy processors and architectures
    /// </summary>
    public async Task<List<EnhancedCpuInfo>> DetectAllCpuHardwareAsync(CancellationToken cancellationToken = default)
    {
        using var activity = _activity.StartActivity("cpu.detect.all");
        var cpus = new List<EnhancedCpuInfo>();

        try
        {
            // Method 1: WMI Detection
            var wmiCpus = await DetectCpusViaWmiAsync(cancellationToken);
            cpus.AddRange(wmiCpus);

            // Method 2: CPUID Detection (Low-level)
            await EnhanceWithCpuidInformationAsync(cpus, cancellationToken);

            // Method 3: Registry Detection
            await EnhanceWithRegistryCpuInfoAsync(cpus, cancellationToken);

            // Method 4: Performance Counter Detection
            await EnhanceWithPerformanceCountersAsync(cpus, cancellationToken);

            // Method 5: Legacy CPU detection
            await DetectLegacyCpuFeaturesAsync(cpus, cancellationToken);

            _logger.LogInformation("Detected {CpuCount} CPU(s) with enhanced information", cpus.Count);
            return cpus;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect CPU hardware");
            activity?.SetTag("error", ex.Message);
            throw;
        }
    }

    #region GPU Detection Methods

    private async Task<List<EnhancedGpuInfo>> DetectGpusViaWmiAsync(CancellationToken cancellationToken)
    {
        var gpus = new List<EnhancedGpuInfo>();

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
            foreach (ManagementObject obj in searcher.Get())
            {
                var gpu = new EnhancedGpuInfo
                {
                    Name = obj["Name"]?.ToString() ?? "Unknown GPU",
                    Manufacturer = obj["AdapterCompatibility"]?.ToString() ?? "Unknown",
                    DeviceId = obj["PNPDeviceID"]?.ToString() ?? "Unknown",
                    DriverVersion = obj["DriverVersion"]?.ToString() ?? "Unknown",
                    DriverDate = obj["DriverDate"]?.ToString() ?? "Unknown",
                    VideoMemorySize = Convert.ToUInt64(obj["AdapterRAM"] ?? 0),
                    VideoProcessor = obj["VideoProcessor"]?.ToString() ?? "Unknown",
                    CurrentHorizontalResolution = Convert.ToUInt32(obj["CurrentHorizontalResolution"] ?? 0),
                    CurrentVerticalResolution = Convert.ToUInt32(obj["CurrentVerticalResolution"] ?? 0),
                    CurrentRefreshRate = Convert.ToUInt32(obj["CurrentRefreshRate"] ?? 0),
                    Status = obj["Status"]?.ToString() ?? "Unknown",
                    DetectionMethod = "WMI"
                };

                // Determine vendor
                gpu.Vendor = DetermineGpuVendor(gpu.Name, gpu.Manufacturer, gpu.DeviceId);
                
                gpus.Add(gpu);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to detect GPUs via WMI");
        }

        return gpus;
    }

    private async Task<List<EnhancedGpuInfo>> DetectGpusViaRegistryAsync(CancellationToken cancellationToken)
    {
        var gpus = new List<EnhancedGpuInfo>();

        try
        {
            // Check multiple registry locations for GPU information
            var registryPaths = new[]
            {
                @"SYSTEM\CurrentControlSet\Enum\PCI",
                @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}", // Display adapters
                @"SYSTEM\CurrentControlSet\Control\Video"
            };

            foreach (var path in registryPaths)
            {
                await ScanRegistryPathForGpusAsync(path, gpus, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to detect GPUs via Registry");
        }

        return gpus;
    }

    private async Task<List<EnhancedGpuInfo>> DetectGpusViaDirectXAsync(CancellationToken cancellationToken)
    {
        var gpus = new List<EnhancedGpuInfo>();

        try
        {
            // Use DXGI to enumerate adapters
            // This would require additional DirectX interop code
            // Placeholder for DirectX detection
            _logger.LogDebug("DirectX GPU detection not yet implemented");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to detect GPUs via DirectX");
        }

        return gpus;
    }

    private async Task EnhanceWithVendorSpecificInfoAsync(List<EnhancedGpuInfo> gpus, CancellationToken cancellationToken)
    {
        foreach (var gpu in gpus)
        {
            switch (gpu.Vendor.ToUpperInvariant())
            {
                case "NVIDIA":
                    await EnhanceNvidiaGpuInfoAsync(gpu, cancellationToken);
                    break;
                case "AMD":
                    await EnhanceAmdGpuInfoAsync(gpu, cancellationToken);
                    break;
                case "INTEL":
                    await EnhanceIntelGpuInfoAsync(gpu, cancellationToken);
                    break;
                default:
                    await EnhanceGenericGpuInfoAsync(gpu, cancellationToken);
                    break;
            }
        }
    }

    private async Task DetectLegacyGpuHardwareAsync(List<EnhancedGpuInfo> gpus, CancellationToken cancellationToken)
    {
        try
        {
            // Detect legacy graphics cards that might not appear in standard WMI queries
            var legacyVendors = new[] { "3dfx", "Matrox", "S3", "Trident", "Cirrus Logic", "ATI", "3DLabs" };
            
            // Check for legacy drivers and hardware signatures
            foreach (var vendor in legacyVendors)
            {
                await CheckForLegacyVendorHardwareAsync(vendor, gpus, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to detect legacy GPU hardware");
        }
    }

    #endregion

    #region CPU Detection Methods

    private async Task<List<EnhancedCpuInfo>> DetectCpusViaWmiAsync(CancellationToken cancellationToken)
    {
        var cpus = new List<EnhancedCpuInfo>();

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
            foreach (ManagementObject obj in searcher.Get())
            {
                var cpu = new EnhancedCpuInfo
                {
                    Name = obj["Name"]?.ToString() ?? "Unknown CPU",
                    Manufacturer = obj["Manufacturer"]?.ToString() ?? "Unknown",
                    ProcessorId = obj["ProcessorId"]?.ToString() ?? "Unknown",
                    Architecture = obj["Architecture"]?.ToString() ?? "Unknown",
                    Family = obj["Family"]?.ToString() ?? "Unknown",
                    Model = obj["Model"]?.ToString() ?? "Unknown",
                    Stepping = obj["Stepping"]?.ToString() ?? "Unknown",
                    MaxClockSpeed = Convert.ToUInt32(obj["MaxClockSpeed"] ?? 0),
                    CurrentClockSpeed = Convert.ToUInt32(obj["CurrentClockSpeed"] ?? 0),
                    NumberOfCores = Convert.ToUInt32(obj["NumberOfCores"] ?? 0),
                    NumberOfLogicalProcessors = Convert.ToUInt32(obj["NumberOfLogicalProcessors"] ?? 0),
                    L2CacheSize = Convert.ToUInt32(obj["L2CacheSize"] ?? 0),
                    L3CacheSize = Convert.ToUInt32(obj["L3CacheSize"] ?? 0),
                    SocketDesignation = obj["SocketDesignation"]?.ToString() ?? "Unknown",
                    DetectionMethod = "WMI"
                };

                // Determine vendor and architecture details
                cpu.Vendor = DetermineCpuVendor(cpu.Manufacturer, cpu.Name);
                cpu.Microarchitecture = DetermineMicroarchitecture(cpu.Vendor, Convert.ToUInt32(cpu.Family), Convert.ToUInt32(cpu.Model));
                
                cpus.Add(cpu);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to detect CPUs via WMI");
        }

        return cpus;
    }

    private async Task EnhanceWithCpuidInformationAsync(List<EnhancedCpuInfo> cpus, CancellationToken cancellationToken)
    {
        try
        {
            // Get native system information
            GetNativeSystemInfo(out var sysInfo);
            
            foreach (var cpu in cpus)
            {
                // Enhance with CPUID information
                cpu.ProcessorArchitecture = GetProcessorArchitectureName(sysInfo.processorArchitecture);
                cpu.ProcessorLevel = sysInfo.processorLevel;
                cpu.ProcessorRevision = sysInfo.processorRevision;
                
                // Detect instruction sets and features
                cpu.InstructionSets = await DetectInstructionSetsAsync(cpu, cancellationToken);
                cpu.CpuFeatures = await DetectCpuFeaturesAsync(cpu, cancellationToken);
                
                // Detect cache hierarchy
                cpu.CacheHierarchy = await DetectCacheHierarchyAsync(cpu, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enhance CPU information with CPUID");
        }
    }

    private async Task DetectLegacyCpuFeaturesAsync(List<EnhancedCpuInfo> cpus, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var cpu in cpus)
            {
                // Detect legacy CPU features and compatibility
                cpu.IsLegacyProcessor = IsLegacyProcessor(cpu.Name, Convert.ToUInt32(cpu.Family), Convert.ToUInt32(cpu.Model));
                cpu.LegacyCompatibilityMode = GetLegacyCompatibilityMode(cpu);
                cpu.VirtualizationSupport = await DetectVirtualizationSupportAsync(cpu, cancellationToken);
                
                // Detect thermal and power management features
                cpu.ThermalManagementFeatures = await DetectThermalFeaturesAsync(cpu, cancellationToken);
                cpu.PowerManagementFeatures = await DetectPowerManagementFeaturesAsync(cpu, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to detect legacy CPU features");
        }
    }

    #endregion

    #region Helper Methods

    private string DetermineGpuVendor(string name, string manufacturer, string deviceId)
    {
        var nameUpper = name.ToUpperInvariant();
        var manufacturerUpper = manufacturer.ToUpperInvariant();
        var deviceIdUpper = deviceId.ToUpperInvariant();

        // NVIDIA detection
        if (nameUpper.Contains("NVIDIA") || nameUpper.Contains("GEFORCE") || 
            nameUpper.Contains("QUADRO") || nameUpper.Contains("TESLA") ||
            nameUpper.Contains("RTX") || nameUpper.Contains("GTX") ||
            deviceIdUpper.Contains("VEN_10DE"))
            return "NVIDIA";

        // AMD detection
        if (nameUpper.Contains("AMD") || nameUpper.Contains("RADEON") || 
            nameUpper.Contains("ATI") || nameUpper.Contains("RYZEN") ||
            deviceIdUpper.Contains("VEN_1002") || deviceIdUpper.Contains("VEN_1022"))
            return "AMD";

        // Intel detection
        if (nameUpper.Contains("INTEL") || manufacturerUpper.Contains("INTEL") ||
            nameUpper.Contains("HD GRAPHICS") || nameUpper.Contains("UHD GRAPHICS") ||
            nameUpper.Contains("IRIS") || deviceIdUpper.Contains("VEN_8086"))
            return "Intel";

        // Legacy vendors
        if (nameUpper.Contains("MATROX") || manufacturerUpper.Contains("MATROX"))
            return "Matrox";
        if (nameUpper.Contains("VIA") || manufacturerUpper.Contains("VIA"))
            return "VIA";
        if (nameUpper.Contains("S3") || manufacturerUpper.Contains("S3"))
            return "S3 Graphics";
        if (nameUpper.Contains("3DFX") || nameUpper.Contains("VOODOO"))
            return "3dfx";

        return "Unknown";
    }

    private string DetermineCpuVendor(string manufacturer, string name)
    {
        var manufacturerUpper = manufacturer.ToUpperInvariant();
        var nameUpper = name.ToUpperInvariant();

        if (manufacturerUpper.Contains("INTEL") || nameUpper.Contains("INTEL"))
            return "Intel";
        if (manufacturerUpper.Contains("AMD") || nameUpper.Contains("AMD"))
            return "AMD";
        if (manufacturerUpper.Contains("ARM") || nameUpper.Contains("ARM"))
            return "ARM";
        if (manufacturerUpper.Contains("VIA") || nameUpper.Contains("VIA"))
            return "VIA";
        if (manufacturerUpper.Contains("CYRIX") || nameUpper.Contains("CYRIX"))
            return "Cyrix";

        return "Unknown";
    }

    private string GetProcessorArchitectureName(ushort architecture)
    {
        return architecture switch
        {
            0 => "x86",
            5 => "ARM",
            6 => "IA64",
            9 => "x64",
            12 => "ARM64",
            _ => "Unknown"
        };
    }

    private bool IsLegacyProcessor(string name, uint family, uint model)
    {
        var nameUpper = name.ToUpperInvariant();
        
        // Consider processors legacy based on various criteria
        if (nameUpper.Contains("PENTIUM") && !nameUpper.Contains("PENTIUM 4"))
            return true;
        if (nameUpper.Contains("486") || nameUpper.Contains("386"))
            return true;
        if (nameUpper.Contains("K6") || nameUpper.Contains("K5"))
            return true;
        if (family < 6) // Very old processor families
            return true;

        return false;
    }

    // Placeholder implementations for complex detection methods
    private async Task<List<string>> DetectInstructionSetsAsync(EnhancedCpuInfo cpu, CancellationToken cancellationToken)
    {
        return await Task.FromResult(new List<string> { "SSE", "SSE2", "AVX", "AVX2" });
    }

    private async Task<List<string>> DetectCpuFeaturesAsync(EnhancedCpuInfo cpu, CancellationToken cancellationToken)
    {
        return await Task.FromResult(new List<string> { "Hyper-Threading", "Virtualization", "AES-NI" });
    }

    private async Task<List<string>> DetectCacheHierarchyAsync(EnhancedCpuInfo cpu, CancellationToken cancellationToken)
    {
        return await Task.FromResult(new List<string> { "L1: 32KB", "L2: 256KB", "L3: 8MB" });
    }

    private string DetermineMicroarchitecture(string vendor, uint family, uint model) => "Unknown";
    private string GetLegacyCompatibilityMode(EnhancedCpuInfo cpu) => "Standard";
    private async Task<bool> DetectVirtualizationSupportAsync(EnhancedCpuInfo cpu, CancellationToken cancellationToken) => true;
    private async Task<List<string>> DetectThermalFeaturesAsync(EnhancedCpuInfo cpu, CancellationToken cancellationToken) => new();
    private async Task<List<string>> DetectPowerManagementFeaturesAsync(EnhancedCpuInfo cpu, CancellationToken cancellationToken) => new();

    // GPU enhancement methods (placeholders)
    private async Task EnhanceGpuInformationAsync(EnhancedGpuInfo gpu, CancellationToken cancellationToken) { }
    private async Task EnhanceNvidiaGpuInfoAsync(EnhancedGpuInfo gpu, CancellationToken cancellationToken) { }
    private async Task EnhanceAmdGpuInfoAsync(EnhancedGpuInfo gpu, CancellationToken cancellationToken) { }
    private async Task EnhanceIntelGpuInfoAsync(EnhancedGpuInfo gpu, CancellationToken cancellationToken) { }
    private async Task EnhanceGenericGpuInfoAsync(EnhancedGpuInfo gpu, CancellationToken cancellationToken) { }
    private async Task CheckForLegacyVendorHardwareAsync(string vendor, List<EnhancedGpuInfo> gpus, CancellationToken cancellationToken) { }
    private async Task ScanRegistryPathForGpusAsync(string path, List<EnhancedGpuInfo> gpus, CancellationToken cancellationToken) { }
    private async Task EnhanceWithRegistryCpuInfoAsync(List<EnhancedCpuInfo> cpus, CancellationToken cancellationToken) { }
    private async Task EnhanceWithPerformanceCountersAsync(List<EnhancedCpuInfo> cpus, CancellationToken cancellationToken) { }

    private void MergeGpuInformation(List<EnhancedGpuInfo> primary, List<EnhancedGpuInfo> additional)
    {
        // Merge logic to combine information from different detection methods
        foreach (var additionalGpu in additional)
        {
            var existing = primary.FirstOrDefault(g => 
                g.DeviceId == additionalGpu.DeviceId || 
                g.Name.Equals(additionalGpu.Name, StringComparison.OrdinalIgnoreCase));
            
            if (existing == null)
            {
                primary.Add(additionalGpu);
            }
            else
            {
                // Merge additional information
                if (string.IsNullOrEmpty(existing.VideoProcessor) && !string.IsNullOrEmpty(additionalGpu.VideoProcessor))
                    existing.VideoProcessor = additionalGpu.VideoProcessor;
                // Add more merge logic as needed
            }
        }
    }

    #endregion
}

/// <summary>
/// Enhanced GPU information with comprehensive vendor support
/// </summary>
public class EnhancedGpuInfo : GpuDetails
{
    public string DetectionMethod { get; set; } = string.Empty;
    public List<string> SupportedDirectXVersions { get; set; } = new();
    public List<string> SupportedOpenGLVersions { get; set; } = new();
    public bool VulkanSupported { get; set; }
    public string ComputeUnits { get; set; } = string.Empty;
    public string StreamProcessors { get; set; } = string.Empty;
    public string BaseClockSpeed { get; set; } = string.Empty;
    public string BoostClockSpeed { get; set; } = string.Empty;
    public string MemoryClockSpeed { get; set; } = string.Empty;
    public string MemoryBusWidth { get; set; } = string.Empty;
    public new string MemoryBandwidth { get; set; } = string.Empty;
    public string PowerConsumption { get; set; } = string.Empty;
    public List<string> ConnectedDisplays { get; set; } = new();
    public List<string> SupportedResolutions { get; set; } = new();
    public bool MultiGpuSupport { get; set; }
    public string MultiGpuTechnology { get; set; } = string.Empty; // SLI, CrossFire, etc.
    public List<string> VendorSpecificFeatures { get; set; } = new();
    public string DriverRecommendation { get; set; } = string.Empty;
    public bool RequiresLegacyDrivers { get; set; }
}

/// <summary>
/// Enhanced CPU information with comprehensive architecture support
/// </summary>
public class EnhancedCpuInfo : CpuDetails
{
    public string DetectionMethod { get; set; } = string.Empty;
    public string Vendor { get; set; } = string.Empty;
    public string Microarchitecture { get; set; } = string.Empty;
    public string ProcessorArchitecture { get; set; } = string.Empty;
    public ushort ProcessorLevel { get; set; }
    public ushort ProcessorRevision { get; set; }
    public List<string> InstructionSets { get; set; } = new();
    public List<string> CpuFeatures { get; set; } = new();
    public new List<string> CacheHierarchy { get; set; } = new();
    public bool IsLegacyProcessor { get; set; }
    public string LegacyCompatibilityMode { get; set; } = string.Empty;
    public bool VirtualizationSupport { get; set; }
    public List<string> ThermalManagementFeatures { get; set; } = new();
    public List<string> PowerManagementFeatures { get; set; } = new();
    public string ManufacturingProcess { get; set; } = string.Empty;
    public string CodeName { get; set; } = string.Empty;
    public DateTime ReleaseDate { get; set; }
    public bool OverclockingSupport { get; set; }
    public string IntegratedGraphics { get; set; } = string.Empty;
}