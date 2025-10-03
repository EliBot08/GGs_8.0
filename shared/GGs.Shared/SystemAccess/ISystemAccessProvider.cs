using System.Diagnostics;

namespace GGs.Shared.SystemAccess;

/// <summary>
/// Defines the contract for deep system access capabilities.
/// All operations respect privilege boundaries and consent requirements.
/// </summary>
public interface ISystemAccessProvider
{
    /// <summary>
    /// Gets WMI/CIM inventory and health data.
    /// Non-admin safe. Returns comprehensive hardware, driver, storage, network, power, and security status.
    /// </summary>
    Task<WmiInventoryResult> GetWmiInventoryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to Windows Event Log channels.
    /// Non-admin safe for Application/System and most Operational channels.
    /// </summary>
    Task<EventLogSubscriptionResult> SubscribeToEventLogsAsync(
        EventLogSubscriptionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries Event Log entries with filtering.
    /// Non-admin safe for accessible channels.
    /// </summary>
    Task<EventLogQueryResult> QueryEventLogsAsync(
        EventLogQueryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts ETW (Event Tracing for Windows) session for user-mode providers.
    /// Kernel providers require elevation and explicit consent.
    /// </summary>
    Task<EtwSessionResult> StartEtwSessionAsync(
        EtwSessionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops an active ETW session.
    /// </summary>
    Task<EtwSessionResult> StopEtwSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Collects performance counter data (CPU, memory, disk, network, per-process).
    /// Non-admin safe.
    /// </summary>
    Task<PerformanceDataResult> CollectPerformanceDataAsync(
        PerformanceDataRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Monitors registry key for changes using RegNotifyChangeKeyValue.
    /// Non-admin safe for HKCU; HKLM requires appropriate permissions.
    /// </summary>
    Task<RegistryMonitorResult> StartRegistryMonitorAsync(
        RegistryMonitorRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops an active registry monitor.
    /// </summary>
    Task<RegistryMonitorResult> StopRegistryMonitorAsync(
        string monitorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries Windows services with detailed status and configuration.
    /// Non-admin safe for queries; modifications require elevation.
    /// </summary>
    Task<ServiceQueryResult> QueryServicesAsync(
        ServiceQueryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets network adapter information, DNS configuration, and socket state.
    /// Uses IP Helper APIs (GetAdaptersAddresses, GetExtendedTcpTable).
    /// Non-admin safe.
    /// </summary>
    Task<NetworkInfoResult> GetNetworkInfoAsync(
        NetworkInfoRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Monitors X509 certificate stores (CurrentUser) for enrollment and trust changes.
    /// Non-admin safe for CurrentUser store.
    /// </summary>
    Task<CertificateMonitorResult> StartCertificateMonitorAsync(
        CertificateMonitorRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops an active certificate monitor.
    /// </summary>
    Task<CertificateMonitorResult> StopCertificateMonitorAsync(
        string monitorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets Windows Update status, history, and last error using WUAPI COM.
    /// Non-admin safe for queries; no forced scans without consent.
    /// </summary>
    Task<WindowsUpdateResult> GetWindowsUpdateStatusAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets power and storage information (battery, thermal, volume health).
    /// Uses powercfg integration and Storage WMI.
    /// Non-admin safe.
    /// </summary>
    Task<PowerStorageResult> GetPowerStorageInfoAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests consent-gated elevation for privileged operations.
    /// Returns elevation result with user decision and reason logging.
    /// </summary>
    Task<ElevationConsentResult> RequestElevationConsentAsync(
        ElevationConsentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks current privilege level and capabilities.
    /// </summary>
    Task<PrivilegeCheckResult> CheckPrivilegesAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of privilege check operation.
/// </summary>
public sealed class PrivilegeCheckResult
{
    public required bool IsElevated { get; init; }
    public required bool IsAdministrator { get; init; }
    public required string UserName { get; init; }
    public required string UserDomain { get; init; }
    public required List<string> EnabledPrivileges { get; init; }
    public required DateTime CheckedAtUtc { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Request for elevation consent.
/// </summary>
public sealed class ElevationConsentRequest
{
    public required string OperationName { get; init; }
    public required string Reason { get; init; }
    public required string DetailedDescription { get; init; }
    public required ElevationRiskLevel RiskLevel { get; init; }
    public required TimeSpan EstimatedDuration { get; init; }
    public required bool RequiresRestart { get; init; }
    public required string CorrelationId { get; init; }
}

/// <summary>
/// Result of elevation consent request.
/// </summary>
public sealed class ElevationConsentResult
{
    public required bool Granted { get; init; }
    public required string Reason { get; init; }
    public required DateTime RequestedAtUtc { get; init; }
    public required DateTime RespondedAtUtc { get; init; }
    public required string CorrelationId { get; init; }
    public string? UserResponse { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Risk level for elevation requests.
/// </summary>
public enum ElevationRiskLevel
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

