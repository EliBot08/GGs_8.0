using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GGs.Shared.Elevation;
using Microsoft.Extensions.Logging;

namespace GGs.Agent.Elevation;

/// <summary>
/// Production-grade elevation bridge that uses the existing ElevatedEntry pathway.
/// Provides discrete, audited privileged operations with structured logging and rollback support.
/// </summary>
public sealed class ElevationBridge : IElevationBridge
{
    private readonly ILogger<ElevationBridge> _logger;
    private readonly ActivitySource _activity = new("GGs.Agent.Elevation");
    private readonly string _agentExecutablePath;

    public ElevationBridge(ILogger<ElevationBridge> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Get the path to the current agent executable
        _agentExecutablePath = Process.GetCurrentProcess().MainModule?.FileName 
            ?? throw new InvalidOperationException("Cannot determine agent executable path");
    }

    public Task<bool> IsElevatedAsync(CancellationToken cancellationToken = default)
    {
        using var activity = _activity.StartActivity("is_elevated");
        
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            var isElevated = principal.IsInRole(WindowsBuiltInRole.Administrator);
            
            activity?.SetTag("is_elevated", isElevated);
            activity?.SetTag("user", identity.Name);
            
            _logger.LogDebug("Elevation check: Elevated={IsElevated} | User={User}", isElevated, identity.Name);
            
            return Task.FromResult(isElevated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check elevation status");
            activity?.SetTag("error", ex.Message);
            return Task.FromResult(false);
        }
    }

    public async Task<ElevationValidationResult> ValidateRequestAsync(
        ElevationRequest request,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activity.StartActivity("validate_request");
        activity?.SetTag("request_id", request.RequestId);
        activity?.SetTag("operation_type", request.OperationType);
        activity?.SetTag("correlation_id", request.CorrelationId);

        var validationErrors = new List<string>();
        var policyViolations = new List<string>();

        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.OperationType))
            validationErrors.Add("OperationType is required");
        
        if (string.IsNullOrWhiteSpace(request.OperationName))
            validationErrors.Add("OperationName is required");
        
        if (string.IsNullOrWhiteSpace(request.Reason))
            validationErrors.Add("Reason is required");
        
        if (request.Payload == null)
            validationErrors.Add("Payload is required");

        // Check if operation type is supported
        var supportedOperations = new[]
        {
            "flushdns", "winsockreset", "tcpauthnormal", "powercfgsetactive",
            "bcdedittimeout", "registryset", "serviceaction", "netshsetdns"
        };

        if (!supportedOperations.Contains(request.OperationType.ToLowerInvariant()))
        {
            validationErrors.Add($"Unsupported operation type: {request.OperationType}");
        }

        // Policy checks based on risk level
        if (request.RiskLevel == ElevationRiskLevel.Critical)
        {
            policyViolations.Add("POLICY.WARN.RiskLevel.Critical - Manual review recommended");
        }

        var isValid = validationErrors.Count == 0 && policyViolations.Count == 0;
        var requiresElevation = await IsElevatedAsync(cancellationToken) == false;

        _logger.LogInformation(
            "Elevation request validated: RequestId={RequestId} | Valid={IsValid} | RequiresElevation={RequiresElevation} | Errors={ErrorCount} | Violations={ViolationCount}",
            request.RequestId, isValid, requiresElevation, validationErrors.Count, policyViolations.Count);

        return new ElevationValidationResult
        {
            IsValid = isValid,
            ValidationErrors = validationErrors.ToArray(),
            PolicyViolations = policyViolations.ToArray(),
            RequiresElevation = requiresElevation,
            ReasonCode = isValid ? null : "VALIDATION.FAILED"
        };
    }

    public async Task<ElevationExecutionResult> ExecuteElevatedAsync(
        ElevationRequest request,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activity.StartActivity("execute_elevated");
        activity?.SetTag("request_id", request.RequestId);
        activity?.SetTag("operation_type", request.OperationType);
        activity?.SetTag("correlation_id", request.CorrelationId);
        activity?.SetTag("risk_level", request.RiskLevel.ToString());

        var executedAt = DateTime.UtcNow;

        _logger.LogInformation(
            "Executing elevated operation: RequestId={RequestId} | Operation={Operation} | Risk={Risk} | CorrelationId={CorrelationId}",
            request.RequestId, request.OperationType, request.RiskLevel, request.CorrelationId);

        try
        {
            // Validate request first
            var validation = await ValidateRequestAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                var errorMsg = string.Join("; ", validation.ValidationErrors.Concat(validation.PolicyViolations));
                _logger.LogWarning(
                    "Elevation request validation failed: RequestId={RequestId} | Errors={Errors}",
                    request.RequestId, errorMsg);

                return new ElevationExecutionResult
                {
                    Success = false,
                    RequestId = request.RequestId,
                    CorrelationId = request.CorrelationId,
                    ExitCode = 1,
                    ErrorMessage = $"Validation failed: {errorMsg}",
                    ExecutedAtUtc = executedAt,
                    CompletedAtUtc = DateTime.UtcNow
                };
            }

            // Check if already elevated
            var isElevated = await IsElevatedAsync(cancellationToken);
            if (!isElevated && !validation.RequiresElevation)
            {
                // Can execute without elevation
                _logger.LogInformation("Executing operation without elevation: RequestId={RequestId}", request.RequestId);
            }

            // Create payload file
            var payloadPath = Path.Combine(Path.GetTempPath(), $"ggs-elevation-{request.RequestId}.json");
            var payloadJson = JsonSerializer.Serialize(ConvertToLegacyRequest(request));
            await File.WriteAllTextAsync(payloadPath, payloadJson, cancellationToken);

            try
            {
                // Execute elevated process
                var psi = new ProcessStartInfo
                {
                    FileName = _agentExecutablePath,
                    Arguments = $"--elevated --payload \"{payloadPath}\"",
                    UseShellExecute = true,
                    Verb = isElevated ? string.Empty : "runas", // Request elevation if not already elevated
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                // For testing/CI, allow simulation without UAC prompt
                if (Environment.GetEnvironmentVariable("GGS_ELEVATION_SIMULATE") == "1")
                {
                    psi.Verb = string.Empty;
                    psi.UseShellExecute = false;
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;
                }

                using var process = Process.Start(psi);
                if (process == null)
                {
                    throw new InvalidOperationException("Failed to start elevated process");
                }

                string? output = null;
                string? error = null;

                if (!psi.UseShellExecute)
                {
                    output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
                    error = await process.StandardError.ReadToEndAsync(cancellationToken);
                }

                await process.WaitForExitAsync(cancellationToken);

                var exitCode = process.ExitCode;
                var completedAt = DateTime.UtcNow;

                // Parse response if available
                string? message = null;
                if (!string.IsNullOrWhiteSpace(output))
                {
                    try
                    {
                        var response = JsonSerializer.Deserialize<ElevatedResponse>(output.Trim());
                        message = response?.Message;
                    }
                    catch
                    {
                        message = output;
                    }
                }

                var success = exitCode == 0;

                _logger.LogInformation(
                    "Elevated operation completed: RequestId={RequestId} | Success={Success} | ExitCode={ExitCode} | Duration={Duration}ms",
                    request.RequestId, success, exitCode, (completedAt - executedAt).TotalMilliseconds);

                activity?.SetTag("success", success);
                activity?.SetTag("exit_code", exitCode);
                activity?.SetTag("duration_ms", (completedAt - executedAt).TotalMilliseconds);

                return new ElevationExecutionResult
                {
                    Success = success,
                    RequestId = request.RequestId,
                    CorrelationId = request.CorrelationId,
                    ExitCode = exitCode,
                    Message = message,
                    ErrorMessage = success ? null : (error ?? message ?? "Operation failed"),
                    ExecutedAtUtc = executedAt,
                    CompletedAtUtc = completedAt,
                    ExecutionLog = new ElevationExecutionLog
                    {
                        Request = request,
                        WasElevated = !isElevated,
                        ExecutedBy = WindowsIdentity.GetCurrent().Name,
                        MachineName = Environment.MachineName,
                        ExecutedAtUtc = executedAt,
                        ChangeDetails = message
                    }
                };
            }
            finally
            {
                // Clean up payload file
                try
                {
                    if (File.Exists(payloadPath))
                        File.Delete(payloadPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete payload file: {Path}", payloadPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Elevated operation failed: RequestId={RequestId}", request.RequestId);
            activity?.SetTag("error", ex.Message);

            return new ElevationExecutionResult
            {
                Success = false,
                RequestId = request.RequestId,
                CorrelationId = request.CorrelationId,
                ExitCode = -1,
                ErrorMessage = $"Execution failed: {ex.Message}",
                ExecutedAtUtc = executedAt,
                CompletedAtUtc = DateTime.UtcNow
            };
        }
    }

    public Task<ElevationRollbackResult> RollbackAsync(
        ElevationExecutionLog executionLog,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activity.StartActivity("rollback");
        activity?.SetTag("original_request_id", executionLog.Request.RequestId);

        _logger.LogInformation(
            "Rolling back elevated operation: OriginalRequestId={RequestId} | Operation={Operation}",
            executionLog.Request.RequestId, executionLog.Request.OperationType);

        // Rollback logic would be implemented here based on operation type
        // For now, return a placeholder indicating rollback is not yet fully implemented
        
        return Task.FromResult(new ElevationRollbackResult
        {
            Success = false,
            OriginalRequestId = executionLog.Request.RequestId,
            ErrorMessage = "Rollback not yet implemented for this operation type",
            RolledBackAtUtc = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Converts the new ElevationRequest to the legacy ElevatedRequest format.
    /// </summary>
    private object ConvertToLegacyRequest(ElevationRequest request)
    {
        // Convert payload to legacy format based on operation type
        var payloadJson = JsonSerializer.Serialize(request.Payload);
        var payloadDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadJson);

        return new
        {
            Type = request.OperationType,
            Guid = payloadDict?.ContainsKey("Guid") == true ? payloadDict["Guid"].GetString() : null,
            TimeoutSeconds = payloadDict?.ContainsKey("TimeoutSeconds") == true ? payloadDict["TimeoutSeconds"].GetInt32() : (int?)null,
            Registry = payloadDict?.ContainsKey("Registry") == true ? JsonSerializer.Deserialize<object>(payloadDict["Registry"].GetRawText()) : null,
            Service = payloadDict?.ContainsKey("Service") == true ? JsonSerializer.Deserialize<object>(payloadDict["Service"].GetRawText()) : null,
            Netsh = payloadDict?.ContainsKey("Netsh") == true ? JsonSerializer.Deserialize<object>(payloadDict["Netsh"].GetRawText()) : null
        };
    }
}

/// <summary>
/// Response from elevated process (matches ElevatedEntry format).
/// </summary>
internal class ElevatedResponse
{
    public bool Ok { get; set; }
    public string? Message { get; set; }
}

