using System;
using System.Collections.Generic;

namespace GGs.Shared.Models;

/// <summary>
/// Ultra-deep diagnostics report containing expert-level Windows internals analysis
/// </summary>
public class UltraDeepDiagnosticsReport
{
    public DateTime ScanStartTime { get; set; }
    public DateTime ScanEndTime { get; set; }
    public TimeSpan ScanDuration { get; set; }
    public string MachineName { get; set; } = string.Empty;

    public KernelInformation? KernelInfo { get; set; }
    public RegistryHealthReport? RegistryHealth { get; set; }
    public DriverAnalysisReport? DriverAnalysis { get; set; }
    public ServiceAnalysisReport? ServiceAnalysis { get; set; }
    public BootConfigurationReport? BootConfig { get; set; }
    public SecurityPolicyReport? SecurityPolicy { get; set; }
    public MemoryDiagnosticsReport? MemoryDiagnostics { get; set; }
    public FileSystemAnalysisReport? FileSystemAnalysis { get; set; }
    public NetworkStackReport? NetworkStack { get; set; }
    public FirmwareInformation? FirmwareInfo { get; set; }
    public ProcessIntegrityReport? ProcessIntegrity { get; set; }
    public ThreadAnalysisReport? ThreadAnalysis { get; set; }
}

#region Kernel Information

public class KernelInformation
{
    public string KernelVersion { get; set; } = string.Empty;
    public string NTVersion { get; set; } = string.Empty;
    public string BuildLab { get; set; } = string.Empty;
    public string BuildLabEx { get; set; } = string.Empty;
    public string ProcessAffinityMask { get; set; } = string.Empty;
    public string SystemAffinityMask { get; set; } = string.Empty;
    public int AvailableProcessors { get; set; }
    public bool IsVirtualized { get; set; }
    public bool HyperVEnabled { get; set; }
    public DateTime SystemBootTime { get; set; }
    public bool DEPEnabled { get; set; }
    public bool ASLREnabled { get; set; }
    public List<PageFileInfo> PageFiles { get; set; } = new();
}

public class PageFileInfo
{
    public string Name { get; set; } = string.Empty;
    public ulong AllocatedSize { get; set; }
    public ulong CurrentUsage { get; set; }
    public ulong PeakUsage { get; set; }
}

#endregion

#region Registry Health

public class RegistryHealthReport
{
    public Dictionary<string, RegistryHiveStatus> HiveStatus { get; set; } = new();
    public List<string> OrphanedKeys { get; set; } = new();
    public List<StartupProgramInfo> StartupPrograms { get; set; } = new();
    public List<string> SuspiciousEntries { get; set; } = new();
    public int HealthScore { get; set; }
}

public class RegistryHiveStatus
{
    public string HiveName { get; set; } = string.Empty;
    public bool IsAccessible { get; set; }
    public int SubKeyCount { get; set; }
    public List<string> AccessErrors { get; set; } = new();
}

public class StartupProgramInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string RegistryPath { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}

#endregion

#region Driver Analysis

public class DriverAnalysisReport
{
    public List<DriverInfo> Drivers { get; set; } = new();
    public int TotalDrivers { get; set; }
    public int RunningDrivers { get; set; }
    public int StoppedDrivers { get; set; }
    public int UnsignedDrivers { get; set; }
    public int MissingDriverFiles { get; set; }
}

public class DriverInfo
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PathName { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StartMode { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool FileExists { get; set; }
    public long FileSize { get; set; }
    public string FileVersion { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsSigned { get; set; }
}

#endregion

#region Service Analysis

public class ServiceAnalysisReport
{
    public List<ServiceInfo> Services { get; set; } = new();
    public int TotalServices { get; set; }
    public int RunningServices { get; set; }
    public int StoppedServices { get; set; }
    public int AutoStartServices { get; set; }
    public int ManualStartServices { get; set; }
    public int DisabledServices { get; set; }
    public int CriticalServices { get; set; }
}

// ServiceInfo class defined in SystemInformation.cs

#endregion

#region Boot Configuration

public class BootConfigurationReport
{
    public List<BootEntry> BootEntries { get; set; } = new();
    public bool SecureBootEnabled { get; set; }
    public bool IsUEFI { get; set; }
    public List<string> BootOrder { get; set; } = new();
    public bool FastStartupEnabled { get; set; }
    public bool HibernationEnabled { get; set; }
}

public class BootEntry
{
    public string Identifier { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Device { get; set; } = string.Empty;
    public string OSDevice { get; set; } = string.Empty;
    public string SystemRoot { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}

#endregion

#region Security Policy

public class SecurityPolicyReport
{
    public List<SecurityPolicySetting> Policies { get; set; } = new();
    public List<UserRight> UserRights { get; set; } = new();
    public FirewallStatus? FirewallStatus { get; set; }
    public AntivirusStatus? AntivirusStatus { get; set; }
    public bool UACEnabled { get; set; }
    public int SecurityScore { get; set; }
}

public class SecurityPolicySetting
{
    public string Category { get; set; } = string.Empty;
    public string PolicyName { get; set; } = string.Empty;
    public string CurrentValue { get; set; } = string.Empty;
    public string RecommendedValue { get; set; } = string.Empty;
    public bool IsCompliant { get; set; }
}

public class UserRight
{
    public string RightName { get; set; } = string.Empty;
    public List<string> AssignedTo { get; set; } = new();
}

public class FirewallStatus
{
    public bool DomainProfileEnabled { get; set; }
    public bool PrivateProfileEnabled { get; set; }
    public bool PublicProfileEnabled { get; set; }
    public int ActiveRules { get; set; }
}

public class AntivirusStatus
{
    public string ProductName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool IsUpToDate { get; set; }
    public DateTime? LastUpdate { get; set; }
}

#endregion

#region Memory Diagnostics

public class MemoryDiagnosticsReport
{
    public List<MemoryRegion> Regions { get; set; } = new();
    public ulong TotalCommittedMemory { get; set; }
    public ulong TotalReservedMemory { get; set; }
    public int PageFaults { get; set; }
    public List<MemoryLeak> PotentialLeaks { get; set; } = new();
}

public class MemoryRegion
{
    public ulong BaseAddress { get; set; }
    public ulong Size { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Protection { get; set; } = string.Empty;
}

public class MemoryLeak
{
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public ulong MemoryGrowth { get; set; }
    public TimeSpan ObservationPeriod { get; set; }
}

#endregion

#region File System Analysis

public class FileSystemAnalysisReport
{
    public List<VolumeInfo> Volumes { get; set; } = new();
    public List<FileSystemIssue> Issues { get; set; } = new();
    public int FragmentationLevel { get; set; }
}

public class VolumeInfo
{
    public string DriveLetter { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string FileSystem { get; set; } = string.Empty;
    public ulong TotalSize { get; set; }
    public ulong FreeSpace { get; set; }
    public bool SupportsEncryption { get; set; }
    public bool SupportsCompression { get; set; }
    public bool IsNTFS { get; set; }
    public int ClusterSize { get; set; }
}

public class FileSystemIssue
{
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}

#endregion

#region Network Stack

public class NetworkStackReport
{
    public List<NetworkProtocol> Protocols { get; set; } = new();
    public List<OpenPort> OpenPorts { get; set; } = new();
    public List<ActiveConnection> ActiveConnections { get; set; } = new();
    public List<NetworkDriver> Drivers { get; set; } = new();
}

public class NetworkProtocol
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}

public class OpenPort
{
    public int PortNumber { get; set; }
    public string Protocol { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public int ProcessId { get; set; }
}

public class ActiveConnection
{
    public string LocalAddress { get; set; } = string.Empty;
    public int LocalPort { get; set; }
    public string RemoteAddress { get; set; } = string.Empty;
    public int RemotePort { get; set; }
    public string State { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
}

public class NetworkDriver
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
}

#endregion

#region Firmware Information

public class FirmwareInformation
{
    public string BIOSVendor { get; set; } = string.Empty;
    public string BIOSVersion { get; set; } = string.Empty;
    public DateTime? BIOSReleaseDate { get; set; }
    public string SMBIOSVersion { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string UUID { get; set; } = string.Empty;
}

#endregion

#region Process Integrity

public class ProcessIntegrityReport
{
    public List<ProcessDetails> Processes { get; set; } = new();
    public List<SuspiciousProcess> SuspiciousProcesses { get; set; } = new();
    public int TotalProcesses { get; set; }
    public int UnsignedProcesses { get; set; }
}

public class ProcessDetails
{
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public bool IsSigned { get; set; }
    public string Signer { get; set; } = string.Empty;
    public long WorkingSet { get; set; }
    public int ThreadCount { get; set; }
    public int HandleCount { get; set; }
}

public class SuspiciousProcess
{
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public List<string> Reasons { get; set; } = new();
    public string SeverityLevel { get; set; } = string.Empty;
}

#endregion

#region Thread Analysis

public class ThreadAnalysisReport
{
    public int TotalThreads { get; set; }
    public int RunningThreads { get; set; }
    public int WaitingThreads { get; set; }
    public List<ThreadInfo> TopThreadsByTime { get; set; } = new();
}

public class ThreadInfo
{
    public int ThreadId { get; set; }
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public TimeSpan TotalProcessorTime { get; set; }
    public int Priority { get; set; }
}

#endregion
