using System.Threading;
using System.Threading.Tasks;

namespace GGs.Shared.Tweaks;

/// <summary>
/// Base interface for all tweak capability modules.
/// Each module implements safe-by-default, powerful-on-consent operations
/// with preflight validation, atomic apply, verify, and rollback capabilities.
/// </summary>
public interface ITweakModule
{
    /// <summary>
    /// Module name for logging and telemetry.
    /// </summary>
    string ModuleName { get; }

    /// <summary>
    /// Validates whether the tweak can be applied without actually applying it.
    /// Checks existence, type, policy constraints, and permissions.
    /// </summary>
    Task<TweakPreflightResult> PreflightAsync(
        TweakDefinition tweak,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies the tweak atomically. Should be idempotent.
    /// </summary>
    Task<TweakApplicationResult> ApplyAsync(
        TweakDefinition tweak,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies that the tweak was applied successfully.
    /// </summary>
    Task<TweakVerificationResult> VerifyAsync(
        TweakDefinition tweak,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the tweak to its previous state.
    /// </summary>
    Task<TweakRollbackResult> RollbackAsync(
        TweakApplicationLog applicationLog,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of preflight validation.
/// </summary>
public sealed record TweakPreflightResult
{
    public required bool CanApply { get; init; }
    public required string Reason { get; init; }
    public string? PolicyViolation { get; init; }
    public string? PermissionIssue { get; init; }
    public string? ValidationError { get; init; }
    public required string BeforeState { get; init; }
}

/// <summary>
/// Result of tweak application.
/// </summary>
public sealed record TweakApplicationResult
{
    public required bool Success { get; init; }
    public required string BeforeState { get; init; }
    public required string AfterState { get; init; }
    public string? Error { get; init; }
    public required DateTime AppliedAtUtc { get; init; }
    public string? DetailedDiff { get; init; }
}

/// <summary>
/// Result of tweak verification.
/// </summary>
public sealed record TweakVerificationResult
{
    public required bool Verified { get; init; }
    public required string CurrentState { get; init; }
    public required string ExpectedState { get; init; }
    public string? Discrepancy { get; init; }
}

/// <summary>
/// Result of tweak rollback.
/// </summary>
public sealed record TweakRollbackResult
{
    public required bool Success { get; init; }
    public required string RestoredState { get; init; }
    public string? Error { get; init; }
    public required DateTime RolledBackAtUtc { get; init; }
}

