using System;
using System.Threading;
using System.Threading.Tasks;

namespace GGs.Shared.Elevation;

/// <summary>
/// Contract for consent-gated elevation bridge.
/// Provides discrete, audited privileged operations with declared intent, input contracts, and rollback plans.
/// Never chains arbitrary commands; each action requires explicit declaration.
/// </summary>
public interface IElevationBridge
{
    /// <summary>
    /// Executes an elevated operation with full audit trail and rollback support.
    /// </summary>
    /// <param name="request">The elevation request with operation details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the elevated operation</returns>
    Task<ElevationExecutionResult> ExecuteElevatedAsync(
        ElevationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the current process is running with elevated privileges.
    /// </summary>
    /// <returns>True if elevated, false otherwise</returns>
    Task<bool> IsElevatedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates an elevation request without executing it.
    /// Checks permissions, policy compliance, and input validation.
    /// </summary>
    /// <param name="request">The elevation request to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with any issues found</returns>
    Task<ElevationValidationResult> ValidateRequestAsync(
        ElevationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back a previously executed elevated operation.
    /// </summary>
    /// <param name="executionLog">The execution log from the original operation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the rollback operation</returns>
    Task<ElevationRollbackResult> RollbackAsync(
        ElevationExecutionLog executionLog,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Request for an elevated operation.
/// </summary>
public sealed class ElevationRequest
{
    /// <summary>
    /// Unique identifier for this request.
    /// </summary>
    public required Guid RequestId { get; init; }

    /// <summary>
    /// Correlation ID for tracing across systems.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// Type of elevated operation (e.g., "RegistrySet", "ServiceAction", "FlushDns").
    /// </summary>
    public required string OperationType { get; init; }

    /// <summary>
    /// Human-readable operation name for logging and consent UI.
    /// </summary>
    public required string OperationName { get; init; }

    /// <summary>
    /// Detailed reason for requiring elevation.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// Risk level of this operation.
    /// </summary>
    public required ElevationRiskLevel RiskLevel { get; init; }

    /// <summary>
    /// Whether this operation requires a system restart.
    /// </summary>
    public required bool RequiresRestart { get; init; }

    /// <summary>
    /// Estimated duration of the operation.
    /// </summary>
    public required TimeSpan EstimatedDuration { get; init; }

    /// <summary>
    /// Structured payload for the operation (JSON-serializable).
    /// </summary>
    public required object Payload { get; init; }

    /// <summary>
    /// Whether this operation supports rollback.
    /// </summary>
    public required bool SupportsRollback { get; init; }

    /// <summary>
    /// Timestamp when the request was created.
    /// </summary>
    public DateTime RequestedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// User who requested the operation (for audit trail).
    /// </summary>
    public string? RequestedBy { get; init; }
}

/// <summary>
/// Risk level for elevation requests.
/// </summary>
public enum ElevationRiskLevel
{
    /// <summary>
    /// Low risk - read-only or easily reversible operations.
    /// </summary>
    Low = 0,

    /// <summary>
    /// Medium risk - configuration changes that can be rolled back.
    /// </summary>
    Medium = 1,

    /// <summary>
    /// High risk - system-level changes that may require restart.
    /// </summary>
    High = 2,

    /// <summary>
    /// Critical risk - operations that could affect system stability.
    /// </summary>
    Critical = 3
}

/// <summary>
/// Result of an elevated operation execution.
/// </summary>
public sealed class ElevationExecutionResult
{
    /// <summary>
    /// Whether the operation succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// The original request ID.
    /// </summary>
    public required Guid RequestId { get; init; }

    /// <summary>
    /// Correlation ID for tracing.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// Exit code from the elevated process (0 = success).
    /// </summary>
    public required int ExitCode { get; init; }

    /// <summary>
    /// Output message from the operation.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Timestamp when execution started.
    /// </summary>
    public required DateTime ExecutedAtUtc { get; init; }

    /// <summary>
    /// Timestamp when execution completed.
    /// </summary>
    public required DateTime CompletedAtUtc { get; init; }

    /// <summary>
    /// Actual duration of the operation.
    /// </summary>
    public TimeSpan Duration => CompletedAtUtc - ExecutedAtUtc;

    /// <summary>
    /// Execution log for audit trail and rollback.
    /// </summary>
    public ElevationExecutionLog? ExecutionLog { get; init; }
}

/// <summary>
/// Execution log for audit trail and rollback support.
/// </summary>
public sealed class ElevationExecutionLog
{
    /// <summary>
    /// The original request.
    /// </summary>
    public required ElevationRequest Request { get; init; }

    /// <summary>
    /// State before the operation (for rollback).
    /// </summary>
    public string? BeforeState { get; init; }

    /// <summary>
    /// State after the operation.
    /// </summary>
    public string? AfterState { get; init; }

    /// <summary>
    /// Detailed changes made by the operation.
    /// </summary>
    public string? ChangeDetails { get; init; }

    /// <summary>
    /// Whether the operation was actually elevated.
    /// </summary>
    public required bool WasElevated { get; init; }

    /// <summary>
    /// User who executed the operation.
    /// </summary>
    public string? ExecutedBy { get; init; }

    /// <summary>
    /// Machine where the operation was executed.
    /// </summary>
    public string? MachineName { get; init; }

    /// <summary>
    /// Timestamp of execution.
    /// </summary>
    public required DateTime ExecutedAtUtc { get; init; }
}

/// <summary>
/// Result of elevation request validation.
/// </summary>
public sealed class ElevationValidationResult
{
    /// <summary>
    /// Whether the request is valid.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Validation errors if any.
    /// </summary>
    public required string[] ValidationErrors { get; init; }

    /// <summary>
    /// Policy violations if any.
    /// </summary>
    public required string[] PolicyViolations { get; init; }

    /// <summary>
    /// Whether elevation is required for this operation.
    /// </summary>
    public required bool RequiresElevation { get; init; }

    /// <summary>
    /// Reason code for validation result (e.g., "POLICY.DENY.ServiceStop.WinDefend").
    /// </summary>
    public string? ReasonCode { get; init; }
}

/// <summary>
/// Result of a rollback operation.
/// </summary>
public sealed class ElevationRollbackResult
{
    /// <summary>
    /// Whether the rollback succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// The original request ID that was rolled back.
    /// </summary>
    public required Guid OriginalRequestId { get; init; }

    /// <summary>
    /// Restored state after rollback.
    /// </summary>
    public string? RestoredState { get; init; }

    /// <summary>
    /// Error message if rollback failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Timestamp when rollback was performed.
    /// </summary>
    public required DateTime RolledBackAtUtc { get; init; }
}

