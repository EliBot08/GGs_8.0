namespace GGs.Shared.Tweaks;

/// <summary>
/// Telemetry context that flows through all operations for distributed tracing and correlation.
/// Implements Prompt 4 requirements: DeviceId + OperationId + CorrelationId with synchronized timestamps.
/// </summary>
public sealed class TelemetryContext
{
    /// <summary>
    /// Unique identifier for the device executing the operation.
    /// Stable across reboots and reinstalls.
    /// </summary>
    public required string DeviceId { get; init; }

    /// <summary>
    /// Unique identifier for this specific operation instance.
    /// Generated once per operation and flows through all related activities.
    /// </summary>
    public required string OperationId { get; init; }

    /// <summary>
    /// Correlation identifier linking related operations together.
    /// Passed from the initiating request through all downstream operations.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// Synchronized timestamp when the operation was initiated (UTC).
    /// Used for accurate time-series analysis and event ordering.
    /// </summary>
    public DateTime InitiatedUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// User identifier if available (for audit trail).
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Session identifier if available (for grouping related operations).
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// Parent operation ID if this is a child operation.
    /// Used for building operation hierarchies.
    /// </summary>
    public string? ParentOperationId { get; init; }

    /// <summary>
    /// Creates a new telemetry context with a new operation ID.
    /// </summary>
    public static TelemetryContext Create(string deviceId, string? correlationId = null, string? userId = null)
    {
        return new TelemetryContext
        {
            DeviceId = deviceId,
            OperationId = Guid.NewGuid().ToString("N"),
            CorrelationId = correlationId ?? Guid.NewGuid().ToString("N"),
            InitiatedUtc = DateTime.UtcNow,
            UserId = userId
        };
    }

    /// <summary>
    /// Creates a child telemetry context that inherits correlation but has a new operation ID.
    /// </summary>
    public TelemetryContext CreateChild()
    {
        return new TelemetryContext
        {
            DeviceId = DeviceId,
            OperationId = Guid.NewGuid().ToString("N"),
            CorrelationId = CorrelationId,
            InitiatedUtc = DateTime.UtcNow,
            UserId = UserId,
            SessionId = SessionId,
            ParentOperationId = OperationId
        };
    }

    /// <summary>
    /// Converts the telemetry context to a dictionary for logging and tracing.
    /// </summary>
    public Dictionary<string, object> ToDictionary()
    {
        var dict = new Dictionary<string, object>
        {
            ["DeviceId"] = DeviceId,
            ["OperationId"] = OperationId,
            ["CorrelationId"] = CorrelationId,
            ["InitiatedUtc"] = InitiatedUtc.ToString("O")
        };

        if (!string.IsNullOrWhiteSpace(UserId))
            dict["UserId"] = UserId;

        if (!string.IsNullOrWhiteSpace(SessionId))
            dict["SessionId"] = SessionId;

        if (!string.IsNullOrWhiteSpace(ParentOperationId))
            dict["ParentOperationId"] = ParentOperationId;

        return dict;
    }

    /// <summary>
    /// Returns a formatted string for logging.
    /// </summary>
    public override string ToString()
    {
        var parts = new List<string>
        {
            $"DeviceId={DeviceId}",
            $"OperationId={OperationId}",
            $"CorrelationId={CorrelationId}"
        };

        if (!string.IsNullOrWhiteSpace(UserId))
            parts.Add($"UserId={UserId}");

        if (!string.IsNullOrWhiteSpace(ParentOperationId))
            parts.Add($"ParentOperationId={ParentOperationId}");

        return string.Join(" | ", parts);
    }
}

/// <summary>
/// Enhanced heartbeat data with comprehensive system health monitoring.
/// Implements Prompt 4 requirements for detailed health telemetry.
/// </summary>
public sealed class EnhancedHeartbeatData
{
    /// <summary>
    /// Telemetry context for correlation.
    /// </summary>
    public required TelemetryContext Context { get; init; }

    /// <summary>
    /// Timestamp of the heartbeat (UTC).
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Agent version string.
    /// </summary>
    public required string AgentVersion { get; init; }

    /// <summary>
    /// Operating system version.
    /// </summary>
    public required string OSVersion { get; init; }

    /// <summary>
    /// Machine name hash (privacy-sanitized).
    /// Implements Prompt 5: Privacy tiering with hashing for confidential data.
    /// </summary>
    public required string MachineNameHash { get; init; }

    /// <summary>
    /// Number of processor cores.
    /// </summary>
    public int ProcessorCount { get; init; }

    /// <summary>
    /// System uptime duration.
    /// </summary>
    public TimeSpan SystemUptime { get; init; }

    /// <summary>
    /// Agent process uptime duration.
    /// </summary>
    public TimeSpan ProcessUptime { get; init; }

    /// <summary>
    /// Connection state (Connected, Disconnected, Reconnecting, etc.).
    /// </summary>
    public required string ConnectionState { get; init; }

    // Detailed health data (opt-in via Agent:SendDetailedHealthData=true)

    /// <summary>
    /// Working set size in bytes (memory used by the agent process).
    /// </summary>
    public long? WorkingSetBytes { get; init; }

    /// <summary>
    /// Private memory size in bytes.
    /// </summary>
    public long? PrivateMemoryBytes { get; init; }

    /// <summary>
    /// Virtual memory size in bytes.
    /// </summary>
    public long? VirtualMemoryBytes { get; init; }

    /// <summary>
    /// CPU usage percentage (0-100).
    /// </summary>
    public double? CpuUsagePercent { get; init; }

    /// <summary>
    /// Total physical memory in bytes.
    /// </summary>
    public long? TotalPhysicalMemoryBytes { get; init; }

    /// <summary>
    /// Available physical memory in bytes.
    /// </summary>
    public long? AvailablePhysicalMemoryBytes { get; init; }

    /// <summary>
    /// Number of threads in the agent process.
    /// </summary>
    public int? ThreadCount { get; init; }

    /// <summary>
    /// Number of handles in the agent process.
    /// </summary>
    public int? HandleCount { get; init; }

    /// <summary>
    /// .NET GC total memory in bytes.
    /// </summary>
    public long? GCTotalMemoryBytes { get; init; }

    /// <summary>
    /// .NET GC generation 0 collection count.
    /// </summary>
    public int? GCGen0Collections { get; init; }

    /// <summary>
    /// .NET GC generation 1 collection count.
    /// </summary>
    public int? GCGen1Collections { get; init; }

    /// <summary>
    /// .NET GC generation 2 collection count.
    /// </summary>
    public int? GCGen2Collections { get; init; }

    /// <summary>
    /// Number of operations executed since last heartbeat.
    /// </summary>
    public int? OperationsExecuted { get; init; }

    /// <summary>
    /// Number of operations succeeded since last heartbeat.
    /// </summary>
    public int? OperationsSucceeded { get; init; }

    /// <summary>
    /// Number of operations failed since last heartbeat.
    /// </summary>
    public int? OperationsFailed { get; init; }

    /// <summary>
    /// Number of items in the offline queue.
    /// </summary>
    public int? OfflineQueueDepth { get; init; }

    /// <summary>
    /// Health score (0-100, where 100 is perfect health).
    /// </summary>
    public int? HealthScore { get; init; }

    /// <summary>
    /// Additional custom metrics as key-value pairs.
    /// </summary>
    public Dictionary<string, object>? CustomMetrics { get; init; }
}

