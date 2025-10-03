namespace GGs.Shared.SystemAccess;

/// <summary>
/// WMI/CIM inventory request parameters.
/// </summary>
public sealed class WmiInventoryRequest
{
    public bool IncludeHardware { get; init; } = true;
    public bool IncludeDrivers { get; init; } = true;
    public bool IncludeStorage { get; init; } = true;
    public bool IncludeNetwork { get; init; } = true;
    public bool IncludePower { get; init; } = true;
    public bool IncludeSecurity { get; init; } = true;
    public bool IncludeHyperV { get; init; } = false; // May require elevation
}

/// <summary>
/// Result of WMI/CIM inventory collection.
/// </summary>
public sealed class WmiInventoryResult
{
    public required bool Success { get; init; }
    public required DateTime CollectedAtUtc { get; init; }
    public required string DeviceId { get; init; }
    public required string CorrelationId { get; init; }
    
    public HardwareInventory? Hardware { get; init; }
    public DriverInventory? Drivers { get; init; }
    public StorageInventory? Storage { get; init; }
    public NetworkInventory? Network { get; init; }
    public PowerInventory? Power { get; init; }
    public SecurityInventory? Security { get; init; }
    
    public string? ErrorMessage { get; init; }
    public List<string> Warnings { get; init; } = new();
}

public sealed class HardwareInventory
{
    public required string Manufacturer { get; init; }
    public required string Model { get; init; }
    public required string SerialNumber { get; init; }
    public required string BiosVersion { get; init; }
    public required string BiosDate { get; init; }
    public required List<CpuInfo> Processors { get; init; }
    public required List<MemoryInfo> Memory { get; init; }
    public required List<DiskInfo> Disks { get; init; }
    public required List<GpuInfo> Graphics { get; init; }
}

public sealed class CpuInfo
{
    public required string Name { get; init; }
    public required string Manufacturer { get; init; }
    public required int Cores { get; init; }
    public required int LogicalProcessors { get; init; }
    public required int MaxClockSpeed { get; init; }
    public required string Architecture { get; init; }
    public required string Family { get; init; }
    public required string Model { get; init; }
    public required string Stepping { get; init; }
    public List<string> Features { get; init; } = new();
}

public sealed class MemoryInfo
{
    public required string Manufacturer { get; init; }
    public required long CapacityBytes { get; init; }
    public required int SpeedMHz { get; init; }
    public required string FormFactor { get; init; }
    public required string MemoryType { get; init; }
    public required string DeviceLocator { get; init; }
}

public sealed class DiskInfo
{
    public required string Model { get; init; }
    public required string SerialNumber { get; init; }
    public required long SizeBytes { get; init; }
    public required string InterfaceType { get; init; }
    public required string MediaType { get; init; }
    public required int PartitionCount { get; init; }
    public required string HealthStatus { get; init; }
}

public sealed class GpuInfo
{
    public required string Name { get; init; }
    public required string Manufacturer { get; init; }
    public required long VideoMemoryBytes { get; init; }
    public required string DriverVersion { get; init; }
    public required string DriverDate { get; init; }
}

public sealed class DriverInventory
{
    public required List<DriverInfo> Drivers { get; init; }
    public required int TotalCount { get; init; }
    public required int SignedCount { get; init; }
    public required int UnsignedCount { get; init; }
}

public sealed class DriverInfo
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string Date { get; init; }
    public required string Provider { get; init; }
    public required bool IsSigned { get; init; }
    public required string DeviceClass { get; init; }
}

public sealed class StorageInventory
{
    public required List<VolumeInfo> Volumes { get; init; }
    public required long TotalCapacityBytes { get; init; }
    public required long TotalFreeBytes { get; init; }
}

public sealed class VolumeInfo
{
    public required string DriveLetter { get; init; }
    public required string Label { get; init; }
    public required string FileSystem { get; init; }
    public required long CapacityBytes { get; init; }
    public required long FreeBytes { get; init; }
    public required string HealthStatus { get; init; }
    public required bool IsSystemVolume { get; init; }
    public required bool IsBootVolume { get; init; }
}

public sealed class NetworkInventory
{
    public required List<NetworkAdapterInfo> Adapters { get; init; }
    public required string DomainName { get; init; }
    public required string WorkgroupName { get; init; }
    public required List<string> DnsServers { get; init; }
}

public sealed class NetworkAdapterInfo
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string MacAddress { get; init; }
    public required string Status { get; init; }
    public required long Speed { get; init; }
    public required List<string> IpAddresses { get; init; }
    public required List<string> Gateways { get; init; }
    public required bool DhcpEnabled { get; init; }
}

public sealed class PowerInventory
{
    public required string PowerPlan { get; init; }
    public required bool OnBattery { get; init; }
    public required int? BatteryPercentage { get; init; }
    public required string? BatteryStatus { get; init; }
    public required int? EstimatedRuntime { get; init; }
    public required bool LidPresent { get; init; }
}

public sealed class SecurityInventory
{
    public required bool BitLockerEnabled { get; init; }
    public required bool DeviceGuardEnabled { get; init; }
    public required bool SecureBootEnabled { get; init; }
    public required bool TpmPresent { get; init; }
    public required string? TpmVersion { get; init; }
    public required bool WindowsDefenderRunning { get; init; }
    public required bool FirewallEnabled { get; init; }
}

/// <summary>
/// Event log subscription request.
/// </summary>
public sealed class EventLogSubscriptionRequest
{
    public required string SubscriptionId { get; init; }
    public required List<string> LogNames { get; init; }
    public required EventLogLevel MinimumLevel { get; init; }
    public required int MaxEventsPerPoll { get; init; }
    public required TimeSpan PollingInterval { get; init; }
    public List<int>? EventIds { get; init; }
    public List<string>? Sources { get; init; }
    public DateTime? StartTime { get; init; }
}

public enum EventLogLevel
{
    Verbose = 0,
    Information = 1,
    Warning = 2,
    Error = 3,
    Critical = 4
}

/// <summary>
/// Result of event log subscription.
/// </summary>
public sealed class EventLogSubscriptionResult
{
    public required bool Success { get; init; }
    public required string SubscriptionId { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required int LogCount { get; init; }
    public string? ErrorMessage { get; init; }
    public List<string> Warnings { get; init; } = new();
}

/// <summary>
/// Event log query request.
/// </summary>
public sealed class EventLogQueryRequest
{
    public required string LogName { get; init; }
    public required EventLogLevel MinimumLevel { get; init; }
    public required int MaxResults { get; init; }
    public DateTime? StartTime { get; init; }
    public DateTime? EndTime { get; init; }
    public List<int>? EventIds { get; init; }
    public List<string>? Sources { get; init; }
    public string? MessageFilter { get; init; }
}

/// <summary>
/// Result of event log query.
/// </summary>
public sealed class EventLogQueryResult
{
    public required bool Success { get; init; }
    public required string LogName { get; init; }
    public required List<EventLogEntry> Entries { get; init; }
    public required int TotalMatched { get; init; }
    public required DateTime QueriedAtUtc { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class EventLogEntry
{
    public required int EventId { get; init; }
    public required string Level { get; init; }
    public required string Source { get; init; }
    public required DateTime TimeCreated { get; init; }
    public required string Message { get; init; }
    public string? MachineName { get; init; }
    public string? UserName { get; init; }
    public Dictionary<string, string> Properties { get; init; } = new();
}

/// <summary>
/// ETW session request.
/// </summary>
public sealed class EtwSessionRequest
{
    public required string SessionId { get; init; }
    public required List<string> ProviderNames { get; init; }
    public required EtwTraceLevel Level { get; init; }
    public required TimeSpan MaxDuration { get; init; }
    public required bool RequiresElevation { get; init; }
    public required string Reason { get; init; }
}

public enum EtwTraceLevel
{
    Critical = 1,
    Error = 2,
    Warning = 3,
    Information = 4,
    Verbose = 5
}

/// <summary>
/// Result of ETW session operation.
/// </summary>
public sealed class EtwSessionResult
{
    public required bool Success { get; init; }
    public required string SessionId { get; init; }
    public required DateTime OperationTimeUtc { get; init; }
    public required int EventCount { get; init; }
    public string? ErrorMessage { get; init; }
    public List<EtwEvent> Events { get; init; } = new();
}

public sealed class EtwEvent
{
    public required string ProviderName { get; init; }
    public required int EventId { get; init; }
    public required string Level { get; init; }
    public required DateTime TimeStamp { get; init; }
    public required string Message { get; init; }
    public Dictionary<string, object> Payload { get; init; } = new();
}

/// <summary>
/// Performance data collection request.
/// </summary>
public sealed class PerformanceDataRequest
{
    public required bool IncludeCpu { get; init; }
    public required bool IncludeMemory { get; init; }
    public required bool IncludeDisk { get; init; }
    public required bool IncludeNetwork { get; init; }
    public required bool IncludePerProcess { get; init; }
    public required int TopProcessCount { get; init; }
    public required TimeSpan SamplingDuration { get; init; }
}

/// <summary>
/// Result of performance data collection.
/// </summary>
public sealed class PerformanceDataResult
{
    public required bool Success { get; init; }
    public required DateTime CollectedAtUtc { get; init; }
    public required TimeSpan SamplingDuration { get; init; }

    public CpuPerformance? Cpu { get; init; }
    public MemoryPerformance? Memory { get; init; }
    public DiskPerformance? Disk { get; init; }
    public NetworkPerformance? Network { get; init; }
    public List<ProcessPerformance> TopProcesses { get; init; } = new();

    public string? ErrorMessage { get; init; }
}

public sealed class CpuPerformance
{
    public required double TotalUsagePercent { get; init; }
    public required List<double> PerCoreUsagePercent { get; init; }
    public required int ProcessorQueueLength { get; init; }
    public required int ContextSwitchesPerSec { get; init; }
}

public sealed class MemoryPerformance
{
    public required long TotalBytes { get; init; }
    public required long AvailableBytes { get; init; }
    public required long CommittedBytes { get; init; }
    public required long CachedBytes { get; init; }
    public required double UsagePercent { get; init; }
    public required long PageFaultsPerSec { get; init; }
}

public sealed class DiskPerformance
{
    public required double ReadBytesPerSec { get; init; }
    public required double WriteBytesPerSec { get; init; }
    public required double ReadOpsPerSec { get; init; }
    public required double WriteOpsPerSec { get; init; }
    public required double AvgDiskQueueLength { get; init; }
    public required double UsagePercent { get; init; }
}

public sealed class NetworkPerformance
{
    public required double BytesSentPerSec { get; init; }
    public required double BytesReceivedPerSec { get; init; }
    public required int CurrentConnections { get; init; }
    public required int PacketsSentPerSec { get; init; }
    public required int PacketsReceivedPerSec { get; init; }
}

public sealed class ProcessPerformance
{
    public required int ProcessId { get; init; }
    public required string ProcessName { get; init; }
    public required double CpuPercent { get; init; }
    public required long WorkingSetBytes { get; init; }
    public required long PrivateBytes { get; init; }
    public required int ThreadCount { get; init; }
    public required int HandleCount { get; init; }
}

/// <summary>
/// Registry monitoring request.
/// </summary>
public sealed class RegistryMonitorRequest
{
    public required string MonitorId { get; init; }
    public required string RegistryPath { get; init; }
    public required bool WatchSubtree { get; init; }
    public required RegistryChangeFilter ChangeFilter { get; init; }
    public required string Reason { get; init; }
}

[Flags]
public enum RegistryChangeFilter
{
    None = 0,
    Name = 1,
    Attributes = 2,
    Value = 4,
    Security = 8,
    All = Name | Attributes | Value | Security
}

/// <summary>
/// Result of registry monitoring operation.
/// </summary>
public sealed class RegistryMonitorResult
{
    public required bool Success { get; init; }
    public required string MonitorId { get; init; }
    public required DateTime OperationTimeUtc { get; init; }
    public required int ChangeCount { get; init; }
    public string? ErrorMessage { get; init; }
    public List<RegistryChange> Changes { get; init; } = new();
}

public sealed class RegistryChange
{
    public required DateTime TimeStamp { get; init; }
    public required string Path { get; init; }
    public required string ValueName { get; init; }
    public required string ChangeType { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
}

/// <summary>
/// Service query request.
/// </summary>
public sealed class ServiceQueryRequest
{
    public string? ServiceName { get; init; }
    public string? DisplayNameFilter { get; init; }
    public ServiceStateFilter? StateFilter { get; init; }
    public bool IncludeConfiguration { get; init; }
    public bool IncludeDependencies { get; init; }
}

public enum ServiceStateFilter
{
    All = 0,
    Running = 1,
    Stopped = 2,
    Paused = 3
}

/// <summary>
/// Result of service query.
/// </summary>
public sealed class ServiceQueryResult
{
    public required bool Success { get; init; }
    public required List<ServiceInfo> Services { get; init; }
    public required DateTime QueriedAtUtc { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class ServiceInfo
{
    public required string ServiceName { get; init; }
    public required string DisplayName { get; init; }
    public required string Status { get; init; }
    public required string StartType { get; init; }
    public required string ServiceType { get; init; }
    public string? Description { get; init; }
    public string? BinaryPath { get; init; }
    public string? Account { get; init; }
    public List<string> Dependencies { get; init; } = new();
    public List<string> DependentServices { get; init; } = new();
}

/// <summary>
/// Network information request.
/// </summary>
public sealed class NetworkInfoRequest
{
    public required bool IncludeAdapters { get; init; }
    public required bool IncludeDnsConfiguration { get; init; }
    public required bool IncludeRoutingTable { get; init; }
    public required bool IncludeActiveSockets { get; init; }
    public required bool IncludeProxySettings { get; init; }
}

/// <summary>
/// Result of network information query.
/// </summary>
public sealed class NetworkInfoResult
{
    public required bool Success { get; init; }
    public required DateTime CollectedAtUtc { get; init; }

    public List<NetworkAdapterDetails> Adapters { get; init; } = new();
    public DnsConfiguration? DnsConfig { get; init; }
    public List<RouteEntry> RoutingTable { get; init; } = new();
    public List<SocketInfo> ActiveSockets { get; init; } = new();
    public ProxyConfiguration? ProxyConfig { get; init; }

    public string? ErrorMessage { get; init; }
}

public sealed class NetworkAdapterDetails
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string MacAddress { get; init; }
    public required string Status { get; init; }
    public required long Speed { get; init; }
    public required string Type { get; init; }
    public required List<IpAddressInfo> IpAddresses { get; init; }
    public required List<string> DnsServers { get; init; }
    public required List<string> Gateways { get; init; }
    public required bool DhcpEnabled { get; init; }
    public string? DhcpServer { get; init; }
}

public sealed class IpAddressInfo
{
    public required string Address { get; init; }
    public required string SubnetMask { get; init; }
    public required string AddressFamily { get; init; }
}

public sealed class DnsConfiguration
{
    public required List<string> DnsServers { get; init; }
    public required string DnsSuffix { get; init; }
    public required bool DnsOverHttpsEnabled { get; init; }
}

public sealed class RouteEntry
{
    public required string Destination { get; init; }
    public required string Netmask { get; init; }
    public required string Gateway { get; init; }
    public required string Interface { get; init; }
    public required int Metric { get; init; }
}

public sealed class SocketInfo
{
    public required string Protocol { get; init; }
    public required string LocalAddress { get; init; }
    public required int LocalPort { get; init; }
    public required string RemoteAddress { get; init; }
    public required int RemotePort { get; init; }
    public required string State { get; init; }
    public required int ProcessId { get; init; }
    public string? ProcessName { get; init; }
}

public sealed class ProxyConfiguration
{
    public required bool ProxyEnabled { get; init; }
    public string? ProxyServer { get; init; }
    public string? ProxyBypass { get; init; }
    public required bool AutoDetect { get; init; }
    public string? AutoConfigUrl { get; init; }
}

/// <summary>
/// Certificate monitoring request.
/// </summary>
public sealed class CertificateMonitorRequest
{
    public required string MonitorId { get; init; }
    public required CertificateStoreLocation StoreLocation { get; init; }
    public required string StoreName { get; init; }
    public required string Reason { get; init; }
}

public enum CertificateStoreLocation
{
    CurrentUser = 1,
    LocalMachine = 2
}

/// <summary>
/// Result of certificate monitoring operation.
/// </summary>
public sealed class CertificateMonitorResult
{
    public required bool Success { get; init; }
    public required string MonitorId { get; init; }
    public required DateTime OperationTimeUtc { get; init; }
    public required int ChangeCount { get; init; }
    public string? ErrorMessage { get; init; }
    public List<CertificateChange> Changes { get; init; } = new();
}

public sealed class CertificateChange
{
    public required DateTime TimeStamp { get; init; }
    public required string ChangeType { get; init; }
    public required string Thumbprint { get; init; }
    public required string Subject { get; init; }
    public required string Issuer { get; init; }
    public required DateTime NotBefore { get; init; }
    public required DateTime NotAfter { get; init; }
}

/// <summary>
/// Result of Windows Update status query.
/// </summary>
public sealed class WindowsUpdateResult
{
    public required bool Success { get; init; }
    public required DateTime QueriedAtUtc { get; init; }

    public required string UpdateServiceStatus { get; init; }
    public required DateTime? LastSearchTime { get; init; }
    public required DateTime? LastInstallTime { get; init; }
    public required int PendingUpdateCount { get; init; }
    public required int InstalledUpdateCount { get; init; }
    public required int FailedUpdateCount { get; init; }

    public List<WindowsUpdateInfo> PendingUpdates { get; init; } = new();
    public List<WindowsUpdateInfo> RecentUpdates { get; init; } = new();

    public string? LastError { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class WindowsUpdateInfo
{
    public required string UpdateId { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Severity { get; init; }
    public required long SizeBytes { get; init; }
    public required bool IsDownloaded { get; init; }
    public required bool IsInstalled { get; init; }
    public required bool IsMandatory { get; init; }
    public required bool RequiresReboot { get; init; }
    public DateTime? InstallDate { get; init; }
}

/// <summary>
/// Result of power and storage information query.
/// </summary>
public sealed class PowerStorageResult
{
    public required bool Success { get; init; }
    public required DateTime CollectedAtUtc { get; init; }

    public PowerStatus? Power { get; init; }
    public StorageStatus? Storage { get; init; }

    public string? ErrorMessage { get; init; }
}

public sealed class PowerStatus
{
    public required string ActivePowerPlan { get; init; }
    public required Guid ActivePowerPlanGuid { get; init; }
    public required List<PowerPlanInfo> AvailablePlans { get; init; }
    public required bool OnBattery { get; init; }
    public required int? BatteryPercentage { get; init; }
    public required string? BatteryStatus { get; init; }
    public required int? EstimatedRuntimeMinutes { get; init; }
    public required bool LidPresent { get; init; }
    public required int? ThermalZoneTemperature { get; init; }
}

public sealed class PowerPlanInfo
{
    public required Guid Guid { get; init; }
    public required string Name { get; init; }
    public required bool IsActive { get; init; }
}

public sealed class StorageStatus
{
    public required List<VolumeHealthInfo> Volumes { get; init; }
    public required long TotalCapacityBytes { get; init; }
    public required long TotalFreeBytes { get; init; }
    public required double TotalUsagePercent { get; init; }
}

public sealed class VolumeHealthInfo
{
    public required string DriveLetter { get; init; }
    public required string Label { get; init; }
    public required string FileSystem { get; init; }
    public required long CapacityBytes { get; init; }
    public required long FreeBytes { get; init; }
    public required double UsagePercent { get; init; }
    public required string HealthStatus { get; init; }
    public required bool IsSystemVolume { get; init; }
    public required bool IsBootVolume { get; init; }
    public required int? SmartStatus { get; init; }
    public string? SmartStatusDescription { get; init; }
}

