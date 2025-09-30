using System.Text.Json.Serialization;

namespace GGs.Shared.Models;

/// <summary>
/// Comprehensive system information model for enterprise-grade hardware detection
/// </summary>
public class SystemInformation
{
    public DateTime CollectionTimestamp { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public double CollectionDurationMs { get; set; }
    
    public BasicSystemInfo BasicInfo { get; set; } = new();
    public CpuInformation CpuInfo { get; set; } = new();
    public GpuInformation GpuInfo { get; set; } = new();
    public MemoryInformation MemoryInfo { get; set; } = new();
    public StorageInformation StorageInfo { get; set; } = new();
    public NetworkInformation NetworkInfo { get; set; } = new();
    public MotherboardInformation MotherboardInfo { get; set; } = new();
    public PowerInformation PowerInfo { get; set; } = new();
    public ThermalInformation ThermalInfo { get; set; } = new();
    public SecurityInformation SecurityInfo { get; set; } = new();
    public PerformanceMetrics PerformanceMetrics { get; set; } = new();
    public RegistryInformation RegistryInfo { get; set; } = new();
}

public class BasicSystemInfo
{
    public string OperatingSystem { get; set; } = string.Empty;
    public string OSVersion { get; set; } = string.Empty;
    public bool Is64BitOS { get; set; }
    public bool Is64BitProcess { get; set; }
    public string MachineName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UserDomainName { get; set; } = string.Empty;
    public int ProcessorCount { get; set; }
    public string SystemDirectory { get; set; } = string.Empty;
    public long WorkingSet { get; set; }
    public long TickCount { get; set; }
    public string? WindowsProductName { get; set; }
    public string? WindowsDisplayVersion { get; set; }
    public string? WindowsBuildNumber { get; set; }
    public string? WindowsReleaseId { get; set; }
    public string? WindowsInstallDate { get; set; }
    public TimeSpan SystemUptime { get; set; }
    public string TimeZone { get; set; } = string.Empty;
    public string UtcOffset { get; set; } = string.Empty;
}

public class CpuInformation
{
    public List<CpuDetails> Processors { get; set; } = new();
    public string CpuBrandString { get; set; } = string.Empty;
    public List<string> SupportedInstructionSets { get; set; } = new();
}

public class CpuDetails
{
    public string Name { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string Family { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Stepping { get; set; } = string.Empty;
    public string ProcessorId { get; set; } = string.Empty;
    public uint MaxClockSpeed { get; set; }
    public uint CurrentClockSpeed { get; set; }
    public uint NumberOfCores { get; set; }
    public uint NumberOfLogicalProcessors { get; set; }
    public uint L2CacheSize { get; set; }
    public uint L3CacheSize { get; set; }
    public string SocketDesignation { get; set; } = string.Empty;
    public string Voltage { get; set; } = string.Empty;
    public ushort DataWidth { get; set; }
    public ushort AddressWidth { get; set; }
    
    // Enhanced CPU information
    public List<string> Features { get; set; } = new();
    public List<string> CacheHierarchy { get; set; } = new();
    public string ThermalDesignPower { get; set; } = string.Empty;
    public string MicroarchitectureName { get; set; } = string.Empty;
}

public class GpuInformation
{
    public List<GpuDetails> GraphicsAdapters { get; set; } = new();
    public string DirectXVersion { get; set; } = string.Empty;
    public string OpenGLVersion { get; set; } = string.Empty;
    public bool VulkanSupport { get; set; }
}

public class GpuDetails
{
    public string Name { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Vendor { get; set; } = string.Empty;
    public string DriverVersion { get; set; } = string.Empty;
    public string DriverDate { get; set; } = string.Empty;
    public ulong VideoMemorySize { get; set; }
    public string VideoProcessor { get; set; } = string.Empty;
    public string VideoArchitecture { get; set; } = string.Empty;
    public string VideoMemoryType { get; set; } = string.Empty;
    public uint CurrentHorizontalResolution { get; set; }
    public uint CurrentVerticalResolution { get; set; }
    public uint CurrentRefreshRate { get; set; }
    public uint CurrentBitsPerPixel { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    
    // Enhanced GPU information
    public string Architecture { get; set; } = string.Empty;
    public string ComputeCapability { get; set; } = string.Empty;
    public List<string> SupportedAPIs { get; set; } = new();
    public string MemoryBandwidth { get; set; } = string.Empty;
    public string ThermalDesignPower { get; set; } = string.Empty;
    public bool IsLegacyHardware { get; set; }
    public List<string> LegacyDriverRecommendations { get; set; } = new();
}

public class MemoryInformation
{
    public List<MemoryModule> Modules { get; set; } = new();
    public ulong TotalPhysicalMemory { get; set; }
    public ulong AvailablePhysicalMemory { get; set; }
    public ulong TotalVirtualMemory { get; set; }
    public ulong AvailableVirtualMemory { get; set; }
    public ulong PageFileSize { get; set; }
    public string MemoryType { get; set; } = string.Empty;
    public uint MemorySpeed { get; set; }
    public string MemoryFormFactor { get; set; } = string.Empty;
}

public class MemoryModule
{
    public string Manufacturer { get; set; } = string.Empty;
    public string PartNumber { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public ulong Capacity { get; set; }
    public uint Speed { get; set; }
    public string MemoryType { get; set; } = string.Empty;
    public string FormFactor { get; set; } = string.Empty;
    public string BankLabel { get; set; } = string.Empty;
    public string DeviceLocator { get; set; } = string.Empty;
    public uint DataWidth { get; set; }
    public uint TotalWidth { get; set; }
    public string Voltage { get; set; } = string.Empty;
}

public class StorageInformation
{
    public List<StorageDevice> Devices { get; set; } = new();
    public ulong TotalStorageCapacity { get; set; }
    public ulong UsedStorageSpace { get; set; }
    public ulong FreeStorageSpace { get; set; }
}

public class StorageDevice
{
    public string Model { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string FirmwareVersion { get; set; } = string.Empty;
    public ulong Size { get; set; }
    public string InterfaceType { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public uint RotationalSpeed { get; set; }
    public string HealthStatus { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public ulong PowerOnHours { get; set; }
    public ulong PowerCycleCount { get; set; }
    public List<string> Partitions { get; set; } = new();
    public bool IsSSD { get; set; }
    public bool IsNVMe { get; set; }
    public string FormFactor { get; set; } = string.Empty;
}

public class NetworkInformation
{
    public List<NetworkAdapter> Adapters { get; set; } = new();
    public string DefaultGateway { get; set; } = string.Empty;
    public List<string> DnsServers { get; set; } = new();
    public string DomainName { get; set; } = string.Empty;
    public bool InternetConnectivity { get; set; }
}

public class NetworkAdapter
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string MACAddress { get; set; } = string.Empty;
    public List<string> IPAddresses { get; set; } = new();
    public List<string> SubnetMasks { get; set; } = new();
    public string AdapterType { get; set; } = string.Empty;
    public ulong Speed { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool DHCPEnabled { get; set; }
    public string DHCPServer { get; set; } = string.Empty;
    public ulong BytesSent { get; set; }
    public ulong BytesReceived { get; set; }
    public bool IsWireless { get; set; }
    public string WirelessSSID { get; set; } = string.Empty;
    public int WirelessSignalStrength { get; set; }
}

public class MotherboardInformation
{
    public string Manufacturer { get; set; } = string.Empty;
    public string Product { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string BIOSManufacturer { get; set; } = string.Empty;
    public string BIOSVersion { get; set; } = string.Empty;
    public string BIOSReleaseDate { get; set; } = string.Empty;
    public string ChipsetManufacturer { get; set; } = string.Empty;
    public string ChipsetModel { get; set; } = string.Empty;
    public List<string> ExpansionSlots { get; set; } = new();
    public List<string> ConnectedDevices { get; set; } = new();
    public bool UEFISupport { get; set; }
    public bool SecureBootEnabled { get; set; }
    public bool TPMPresent { get; set; }
    public string TPMVersion { get; set; } = string.Empty;
}

public class PowerInformation
{
    public string PowerSupplyManufacturer { get; set; } = string.Empty;
    public string PowerSupplyModel { get; set; } = string.Empty;
    public uint PowerSupplyWattage { get; set; }
    public string PowerSupplyEfficiency { get; set; } = string.Empty;
    public bool BatteryPresent { get; set; }
    public string BatteryStatus { get; set; } = string.Empty;
    public uint BatteryChargeLevel { get; set; }
    public string PowerPlan { get; set; } = string.Empty;
    public List<string> PowerStates { get; set; } = new();
    public double CurrentPowerConsumption { get; set; }
    public bool UPSConnected { get; set; }
}

public class ThermalInformation
{
    public List<TemperatureSensor> Sensors { get; set; } = new();
    public List<FanSensor> Fans { get; set; } = new();
    public double AmbientTemperature { get; set; }
    public double MaxOperatingTemperature { get; set; }
    public bool ThermalThrottling { get; set; }
    public string CoolingSystem { get; set; } = string.Empty;
}

public class TemperatureSensor
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public double CurrentTemperature { get; set; }
    public double MaxTemperature { get; set; }
    public double MinTemperature { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class FanSensor
{
    public string Name { get; set; } = string.Empty;
    public uint CurrentSpeed { get; set; }
    public uint MaxSpeed { get; set; }
    public uint MinSpeed { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool PWMControlled { get; set; }
}

public class SecurityInformation
{
    public bool WindowsDefenderEnabled { get; set; }
    public string AntivirusProduct { get; set; } = string.Empty;
    public string FirewallStatus { get; set; } = string.Empty;
    public bool BitLockerEnabled { get; set; }
    public List<string> SecurityFeatures { get; set; } = new();
    public bool VirtualizationEnabled { get; set; }
    public bool HyperVEnabled { get; set; }
    public string UAC_Level { get; set; } = string.Empty;
    public List<string> InstalledCertificates { get; set; } = new();
    public bool DEPEnabled { get; set; }
    public bool ASLREnabled { get; set; }
}

public class PerformanceMetrics
{
    public double CPUUsagePercent { get; set; }
    public double MemoryUsagePercent { get; set; }
    public double DiskUsagePercent { get; set; }
    public double NetworkUsagePercent { get; set; }
    public double GPUUsagePercent { get; set; }
    public List<ProcessInfo> TopProcesses { get; set; } = new();
    public List<ServiceInfo> RunningServices { get; set; } = new();
    public double SystemResponseTime { get; set; }
    public ulong HandleCount { get; set; }
    public ulong ThreadCount { get; set; }
    public ulong ProcessCount { get; set; }
}

public class ProcessInfo
{
    public string Name { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public double CPUUsage { get; set; }
    public ulong MemoryUsage { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public string ExecutablePath { get; set; } = string.Empty;
}

public class ServiceInfo
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StartType { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
}

public class RegistryInformation
{
    public List<RegistryKey> ImportantKeys { get; set; } = new();
    public List<string> StartupPrograms { get; set; } = new();
    public List<string> InstalledSoftware { get; set; } = new();
    public List<string> SystemPolicies { get; set; } = new();
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
    public List<string> SystemTweaks { get; set; } = new();
    public List<string> PerformanceSettings { get; set; } = new();
}

public class RegistryKey
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsSystemCritical { get; set; }
}