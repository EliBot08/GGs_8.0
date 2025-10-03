namespace GGs.Shared.Tweaks;

/// <summary>
/// Standardized reason codes for telemetry, audit logging, and policy decisions.
/// Format: CATEGORY.ACTION.Context.Detail
/// 
/// This provides stable, machine-readable codes for long-term analytics and compliance.
/// All codes follow a hierarchical structure for easy filtering and aggregation.
/// </summary>
public static class ReasonCodes
{
    // ═══════════════════════════════════════════════════════════════════════════
    // POLICY - Policy enforcement decisions
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Policy denied the operation</summary>
    public const string POLICY_DENY = "POLICY.DENY";

    /// <summary>Policy allowed the operation</summary>
    public const string POLICY_ALLOW = "POLICY.ALLOW";

    /// <summary>Policy warning issued but operation proceeded</summary>
    public const string POLICY_WARN = "POLICY.WARN";

    // Service-specific policy codes
    public static string PolicyDenyServiceStop(string serviceName) => $"POLICY.DENY.ServiceStop.{serviceName}";
    public static string PolicyDenyServiceDisable(string serviceName) => $"POLICY.DENY.ServiceDisable.{serviceName}";
    public static string PolicyDenyServiceStart(string serviceName) => $"POLICY.DENY.ServiceStart.{serviceName}";
    public static string PolicyAllowServiceAction(string serviceName, string action) => $"POLICY.ALLOW.Service{action}.{serviceName}";

    // Registry-specific policy codes
    public static string PolicyDenyRegistryPath(string path) => $"POLICY.DENY.RegistryPath.{SanitizeForCode(path)}";
    public static string PolicyDenyRegistryRoot(string root) => $"POLICY.DENY.RegistryRoot.{root}";
    public static string PolicyAllowRegistryWrite(string path) => $"POLICY.ALLOW.RegistryWrite.{SanitizeForCode(path)}";

    // Script-specific policy codes
    public static string PolicyDenyScriptContent(string reason) => $"POLICY.DENY.ScriptContent.{reason}";
    public static string PolicyAllowScript(string mode) => $"POLICY.ALLOW.Script.{mode}";

    // Security-specific policy codes
    public static string PolicyDenySecurityFeatureDisable(string feature) => $"POLICY.DENY.SecurityDisable.{feature}";
    public static string PolicyWarnRiskLevel(string level) => $"POLICY.WARN.RiskLevel.{level}";

    // ═══════════════════════════════════════════════════════════════════════════
    // VALIDATION - Input validation failures
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Validation failed</summary>
    public const string VALIDATION_FAILED = "VALIDATION.FAILED";

    public static string ValidationFailedMissingField(string fieldName) => $"VALIDATION.FAILED.MissingField.{fieldName}";
    public static string ValidationFailedInvalidFormat(string fieldName) => $"VALIDATION.FAILED.InvalidFormat.{fieldName}";
    public static string ValidationFailedOutOfRange(string fieldName) => $"VALIDATION.FAILED.OutOfRange.{fieldName}";
    public static string ValidationFailedPathBlocked(string path) => $"VALIDATION.FAILED.PathBlocked.{SanitizeForCode(path)}";

    // ═══════════════════════════════════════════════════════════════════════════
    // EXECUTION - Operation execution results
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Execution succeeded</summary>
    public const string EXECUTION_SUCCESS = "EXECUTION.SUCCESS";

    /// <summary>Execution failed</summary>
    public const string EXECUTION_FAILED = "EXECUTION.FAILED";

    /// <summary>Execution timed out</summary>
    public const string EXECUTION_TIMEOUT = "EXECUTION.TIMEOUT";

    /// <summary>Execution was cancelled</summary>
    public const string EXECUTION_CANCELLED = "EXECUTION.CANCELLED";

    public static string ExecutionFailedPermissionDenied(string resource) => $"EXECUTION.FAILED.PermissionDenied.{resource}";
    public static string ExecutionFailedResourceNotFound(string resource) => $"EXECUTION.FAILED.NotFound.{resource}";
    public static string ExecutionFailedException(string exceptionType) => $"EXECUTION.FAILED.Exception.{exceptionType}";

    // ═══════════════════════════════════════════════════════════════════════════
    // ELEVATION - Privilege elevation results
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Elevation was granted</summary>
    public const string ELEVATION_GRANTED = "ELEVATION.GRANTED";

    /// <summary>Elevation was denied by user</summary>
    public const string ELEVATION_DENIED_USER = "ELEVATION.DENIED.User";

    /// <summary>Elevation was denied by policy</summary>
    public const string ELEVATION_DENIED_POLICY = "ELEVATION.DENIED.Policy";

    /// <summary>Elevation was not required</summary>
    public const string ELEVATION_NOT_REQUIRED = "ELEVATION.NOT_REQUIRED";

    /// <summary>Elevation failed due to error</summary>
    public static string ElevationFailed(string reason) => $"ELEVATION.FAILED.{reason}";

    // ═══════════════════════════════════════════════════════════════════════════
    // CONSENT - User consent decisions
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>User granted consent</summary>
    public const string CONSENT_GRANTED = "CONSENT.GRANTED";

    /// <summary>User denied consent</summary>
    public const string CONSENT_DENIED = "CONSENT.DENIED";

    /// <summary>Consent timed out</summary>
    public const string CONSENT_TIMEOUT = "CONSENT.TIMEOUT";

    /// <summary>Consent not required</summary>
    public const string CONSENT_NOT_REQUIRED = "CONSENT.NOT_REQUIRED";

    // ═══════════════════════════════════════════════════════════════════════════
    // PREFLIGHT - Pre-execution checks
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Preflight checks passed</summary>
    public const string PREFLIGHT_PASSED = "PREFLIGHT.PASSED";

    /// <summary>Preflight checks failed</summary>
    public const string PREFLIGHT_FAILED = "PREFLIGHT.FAILED";

    public static string PreflightFailedPolicyViolation(string detail) => $"PREFLIGHT.FAILED.PolicyViolation.{detail}";
    public static string PreflightFailedValidation(string detail) => $"PREFLIGHT.FAILED.Validation.{detail}";
    public static string PreflightFailedPrerequisite(string detail) => $"PREFLIGHT.FAILED.Prerequisite.{detail}";

    // ═══════════════════════════════════════════════════════════════════════════
    // ROLLBACK - Rollback operations
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Rollback succeeded</summary>
    public const string ROLLBACK_SUCCESS = "ROLLBACK.SUCCESS";

    /// <summary>Rollback failed</summary>
    public const string ROLLBACK_FAILED = "ROLLBACK.FAILED";

    /// <summary>Rollback not available</summary>
    public const string ROLLBACK_NOT_AVAILABLE = "ROLLBACK.NOT_AVAILABLE";

    public static string RollbackFailed(string reason) => $"ROLLBACK.FAILED.{reason}";

    // ═══════════════════════════════════════════════════════════════════════════
    // AUDIT - Audit logging results
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Audit log sent successfully</summary>
    public const string AUDIT_SUCCESS = "AUDIT.SUCCESS";

    /// <summary>Audit log failed to send</summary>
    public const string AUDIT_FAILED = "AUDIT.FAILED";

    /// <summary>Audit log queued for offline retry</summary>
    public const string AUDIT_QUEUED = "AUDIT.QUEUED";

    public static string AuditFailedEndpoint(string endpoint) => $"AUDIT.FAILED.Endpoint.{endpoint}";
    public static string AuditSuccessEndpoint(string endpoint) => $"AUDIT.SUCCESS.Endpoint.{endpoint}";

    // ═══════════════════════════════════════════════════════════════════════════
    // NETWORK - Network operation results
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Network operation succeeded</summary>
    public const string NETWORK_SUCCESS = "NETWORK.SUCCESS";

    /// <summary>Network operation failed</summary>
    public const string NETWORK_FAILED = "NETWORK.FAILED";

    /// <summary>Network timeout</summary>
    public const string NETWORK_TIMEOUT = "NETWORK.TIMEOUT";

    /// <summary>Network unavailable</summary>
    public const string NETWORK_UNAVAILABLE = "NETWORK.UNAVAILABLE";

    public static string NetworkFailedStatusCode(int statusCode) => $"NETWORK.FAILED.StatusCode.{statusCode}";
    public static string NetworkFailedException(string exceptionType) => $"NETWORK.FAILED.Exception.{exceptionType}";

    // ═══════════════════════════════════════════════════════════════════════════
    // QUEUE - Offline queue operations
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Item enqueued successfully</summary>
    public const string QUEUE_ENQUEUED = "QUEUE.ENQUEUED";

    /// <summary>Item dispatched successfully</summary>
    public const string QUEUE_DISPATCHED = "QUEUE.DISPATCHED";

    /// <summary>Item dispatch failed</summary>
    public const string QUEUE_DISPATCH_FAILED = "QUEUE.DISPATCH_FAILED";

    /// <summary>Item dropped due to policy</summary>
    public const string QUEUE_DROPPED = "QUEUE.DROPPED";

    public static string QueueDispatchFailedRetry(int attemptCount) => $"QUEUE.DISPATCH_FAILED.Retry.Attempt{attemptCount}";

    // ═══════════════════════════════════════════════════════════════════════════
    // HELPER METHODS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sanitizes a string for use in a reason code by removing invalid characters.
    /// Keeps only alphanumeric characters, underscores, and hyphens.
    /// </summary>
    private static string SanitizeForCode(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "Unknown";

        // Take last segment if it's a path
        var lastSegment = input.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? input;

        // Remove invalid characters
        var sanitized = new string(lastSegment
            .Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-')
            .ToArray());

        // Limit length
        if (sanitized.Length > 50)
            sanitized = sanitized.Substring(0, 50);

        return string.IsNullOrWhiteSpace(sanitized) ? "Unknown" : sanitized;
    }

    /// <summary>
    /// Parses a reason code into its components.
    /// Returns (Category, Action, Context, Detail) or nulls if parsing fails.
    /// </summary>
    public static (string? Category, string? Action, string? Context, string? Detail) Parse(string? reasonCode)
    {
        if (string.IsNullOrWhiteSpace(reasonCode))
            return (null, null, null, null);

        var parts = reasonCode.Split('.');
        return parts.Length switch
        {
            >= 4 => (parts[0], parts[1], parts[2], string.Join(".", parts.Skip(3))),
            3 => (parts[0], parts[1], parts[2], null),
            2 => (parts[0], parts[1], null, null),
            1 => (parts[0], null, null, null),
            _ => (null, null, null, null)
        };
    }

    /// <summary>
    /// Checks if a reason code indicates success.
    /// </summary>
    public static bool IsSuccess(string? reasonCode)
    {
        if (string.IsNullOrWhiteSpace(reasonCode))
            return false;

        return reasonCode.Contains(".SUCCESS") ||
               reasonCode.Contains(".ALLOW") ||
               reasonCode.Contains(".GRANTED") ||
               reasonCode.Contains(".PASSED");
    }

    /// <summary>
    /// Checks if a reason code indicates a policy denial.
    /// </summary>
    public static bool IsPolicyDenial(string? reasonCode)
    {
        if (string.IsNullOrWhiteSpace(reasonCode))
            return false;

        return reasonCode.StartsWith("POLICY.DENY", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a reason code indicates a validation failure.
    /// </summary>
    public static bool IsValidationFailure(string? reasonCode)
    {
        if (string.IsNullOrWhiteSpace(reasonCode))
            return false;

        return reasonCode.StartsWith("VALIDATION.FAILED", StringComparison.OrdinalIgnoreCase);
    }
}

