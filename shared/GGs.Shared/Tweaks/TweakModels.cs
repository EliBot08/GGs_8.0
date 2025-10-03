using GGs.Shared.Enums;

namespace GGs.Shared.Tweaks;

public sealed class TweakDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }

    public CommandType CommandType { get; set; }

    // Registry
    public string? RegistryPath { get; set; }
    public string? RegistryValueName { get; set; }
    public string? RegistryValueType { get; set; } // String, DWord, QWord, MultiString, Binary
    public string? RegistryValueData { get; set; }

    // Service
    public string? ServiceName { get; set; }
    public ServiceAction? ServiceAction { get; set; }

    // Script
    public string? ScriptContent { get; set; } // PowerShell by default

    public SafetyLevel Safety { get; set; } = SafetyLevel.Medium;
    public RiskLevel Risk { get; set; } = RiskLevel.Medium;

    public bool RequiresAdmin { get; set; } = true;
    public bool AllowUndo { get; set; } = true;
    public string? UndoScriptContent { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedUtc { get; set; }
    public string? CreatedBy { get; set; }
}

public sealed class TweakApplicationLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TweakId { get; set; }
    public string? TweakName { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public DateTime AppliedUtc { get; set; } = DateTime.UtcNow;
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? BeforeState { get; set; }
    public string? AfterState { get; set; }
    public int ExecutionTimeMs { get; set; }

    // Prompt 4: Enhanced Telemetry, Correlation, and Trace Depth
    /// <summary>
    /// Unique operation identifier for this specific execution.
    /// Used for distributed tracing and correlation across systems.
    /// </summary>
    public string OperationId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Correlation identifier linking related operations together.
    /// Passed from the initiating request through all downstream operations.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Synchronized timestamp when the operation was initiated (UTC).
    /// Used for accurate time-series analysis and event ordering.
    /// </summary>
    public DateTime InitiatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Synchronized timestamp when the operation completed (UTC).
    /// Used for duration calculation and performance analysis.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// Standardized reason code for policy decisions and errors.
    /// Format: CATEGORY.ACTION.Context.Detail
    /// Examples: POLICY.DENY.ServiceStop.WinDefend, VALIDATION.FAILED.RegistryPath.Blocked
    /// </summary>
    public string? ReasonCode { get; set; }

    /// <summary>
    /// Policy decision context for audit trail.
    /// Captures why an operation was allowed, denied, or modified.
    /// </summary>
    public string? PolicyDecision { get; set; }

    // Enriched audit fields (explicit for ops and compliance)
    // Registry
    public string? RegistryPath { get; set; }
    public string? RegistryValueName { get; set; }
    public string? RegistryValueType { get; set; }
    public string? OriginalValue { get; set; }
    public string? NewValue { get; set; }

    // Service
    public string? ServiceName { get; set; }
    public string? ActionApplied { get; set; }

    // Script
    public string? ScriptApplied { get; set; }
    public string? UndoScript { get; set; }
}
