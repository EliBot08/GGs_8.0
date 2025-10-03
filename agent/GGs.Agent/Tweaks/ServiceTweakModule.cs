using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using GGs.Shared.Tweaks;
using GGs.Shared.Enums;

namespace GGs.Agent.Tweaks;

/// <summary>
/// Service Tweak Module: start/stop/restart/enable/disable with policy allow/deny sets;
/// timeouts + reason-coded results; never touch blocked critical services.
/// </summary>
public sealed class ServiceTweakModule : ITweakModule
{
    private readonly ILogger<ServiceTweakModule> _logger;
    private readonly ActivitySource _activity = new("GGs.Agent.Tweak.Service");

    // Policy: block critical services from Stop/Disable
    private static readonly HashSet<string> CriticalServices = new(StringComparer.OrdinalIgnoreCase)
    {
        "WinDefend",           // Windows Defender
        "wuauserv",            // Windows Update
        "TrustedInstaller",    // Windows Modules Installer
        "RpcSs",               // Remote Procedure Call
        "EventLog",            // Windows Event Log
        "Winmgmt",             // Windows Management Instrumentation
        "LSM",                 // Local Session Manager
        "TermService",         // Remote Desktop Services
        "LanmanWorkstation",   // Workstation (SMB client)
        "LanmanServer",        // Server (SMB server)
        "Dhcp",                // DHCP Client
        "Dnscache",            // DNS Client
        "BFE",                 // Base Filtering Engine (Firewall)
        "MpsSvc",              // Windows Firewall
        "CryptSvc",            // Cryptographic Services
        "BITS",                // Background Intelligent Transfer Service
        "Schedule",            // Task Scheduler
        "ProfSvc",             // User Profile Service
        "Themes",              // Themes
        "AudioSrv",            // Windows Audio
        "AudioEndpointBuilder" // Windows Audio Endpoint Builder
    };

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    public string ModuleName => "Service";

    public ServiceTweakModule(ILogger<ServiceTweakModule> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<TweakPreflightResult> PreflightAsync(
        TweakDefinition tweak,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activity.StartActivity("service.preflight");
        activity?.SetTag("tweak.id", tweak.Id);
        activity?.SetTag("service.name", tweak.ServiceName);
        activity?.SetTag("service.action", tweak.ServiceAction?.ToString());

        try
        {
            // Validate service name
            if (string.IsNullOrWhiteSpace(tweak.ServiceName))
            {
                return Task.FromResult(new TweakPreflightResult
                {
                    CanApply = false,
                    Reason = "Service name is required",
                    ValidationError = "ServiceName is null or empty",
                    BeforeState = "{}"
                });
            }

            // Validate action
            if (!tweak.ServiceAction.HasValue)
            {
                return Task.FromResult(new TweakPreflightResult
                {
                    CanApply = false,
                    Reason = "Service action is required",
                    ValidationError = "ServiceAction is null",
                    BeforeState = "{}"
                });
            }

            // Check policy: critical service protection
            var action = tweak.ServiceAction.Value;
            if ((action == ServiceAction.Stop || action == ServiceAction.Disable) &&
                CriticalServices.Contains(tweak.ServiceName))
            {
                return Task.FromResult(new TweakPreflightResult
                {
                    CanApply = false,
                    Reason = "Service is protected by policy",
                    PolicyViolation = $"Cannot {action} critical service: {tweak.ServiceName}",
                    BeforeState = "{}"
                });
            }

            // Check if service exists and get current state
            try
            {
                using var sc = new ServiceController(tweak.ServiceName);
                var status = sc.Status; // This will throw if service doesn't exist

                var beforeState = GetServiceState(tweak.ServiceName);
                var beforeJson = TweakStateSerializer.Serialize(beforeState);

                // Check if action is meaningful
                var actionNeeded = IsActionNeeded(status, action);
                if (!actionNeeded)
                {
                    _logger.LogInformation(
                        "Service action not needed: {Service} is already in desired state for {Action}",
                        tweak.ServiceName, action);
                }

                return Task.FromResult(new TweakPreflightResult
                {
                    CanApply = true,
                    Reason = actionNeeded ? "Preflight validation passed" : "Service already in desired state (idempotent)",
                    BeforeState = beforeJson
                });
            }
            catch (InvalidOperationException)
            {
                return Task.FromResult(new TweakPreflightResult
                {
                    CanApply = false,
                    Reason = "Service not found",
                    ValidationError = $"Service does not exist: {tweak.ServiceName}",
                    BeforeState = "{}"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Service preflight failed: {Service}", tweak.ServiceName);
            return Task.FromResult(new TweakPreflightResult
            {
                CanApply = false,
                Reason = "Preflight validation failed",
                ValidationError = ex.Message,
                BeforeState = "{}"
            });
        }
    }

    public async Task<TweakApplicationResult> ApplyAsync(
        TweakDefinition tweak,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activity.StartActivity("service.apply");
        activity?.SetTag("tweak.id", tweak.Id);
        activity?.SetTag("service.name", tweak.ServiceName);
        activity?.SetTag("service.action", tweak.ServiceAction?.ToString());

        var appliedAt = DateTime.UtcNow;

        try
        {
            // Get before state
            var beforeState = GetServiceState(tweak.ServiceName!);
            var beforeJson = TweakStateSerializer.Serialize(beforeState);

            // Apply the action
            await ApplyServiceActionAsync(tweak.ServiceName!, tweak.ServiceAction!.Value, cancellationToken);

            // Get after state
            var afterState = GetServiceState(tweak.ServiceName!);
            afterState.ActionApplied = tweak.ServiceAction.Value.ToString();
            var afterJson = TweakStateSerializer.Serialize(afterState);

            // Generate diff
            var diff = $"Status: {beforeState.Status} → {afterState.Status}";

            _logger.LogInformation(
                "Service action applied: {Service} | Action: {Action} | {Diff}",
                tweak.ServiceName, tweak.ServiceAction, diff);

            activity?.SetTag("success", true);

            return new TweakApplicationResult
            {
                Success = true,
                BeforeState = beforeJson,
                AfterState = afterJson,
                AppliedAtUtc = appliedAt,
                DetailedDiff = diff
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Service apply failed: {Service} | Action: {Action}",
                tweak.ServiceName, tweak.ServiceAction);

            activity?.SetTag("success", false);
            activity?.SetTag("error", ex.Message);

            return new TweakApplicationResult
            {
                Success = false,
                BeforeState = "{}",
                AfterState = "{}",
                Error = ex.ToString(),
                AppliedAtUtc = appliedAt
            };
        }
    }

    public Task<TweakVerificationResult> VerifyAsync(
        TweakDefinition tweak,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activity.StartActivity("service.verify");
        activity?.SetTag("tweak.id", tweak.Id);

        try
        {
            if (string.IsNullOrWhiteSpace(tweak.ServiceName) || !tweak.ServiceAction.HasValue)
            {
                throw new ArgumentException("Service name and action are required");
            }

            var currentState = GetServiceState(tweak.ServiceName);
            var currentJson = TweakStateSerializer.Serialize(currentState);

            // Determine expected status based on action
            var expectedStatus = GetExpectedStatus(tweak.ServiceAction.Value);
            var verified = currentState.Status?.Equals(expectedStatus, StringComparison.OrdinalIgnoreCase) ?? false;

            var discrepancy = verified ? null :
                $"Expected status: {expectedStatus}, Got: {currentState.Status}";

            return Task.FromResult(new TweakVerificationResult
            {
                Verified = verified,
                CurrentState = currentJson,
                ExpectedState = $"{{\"Status\":\"{expectedStatus}\"}}",
                Discrepancy = discrepancy
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Service verification failed: {Service}", tweak.ServiceName);

            return Task.FromResult(new TweakVerificationResult
            {
                Verified = false,
                CurrentState = "{}",
                ExpectedState = "{}",
                Discrepancy = $"Verification error: {ex.Message}"
            });
        }
    }

    public async Task<TweakRollbackResult> RollbackAsync(
        TweakApplicationLog applicationLog,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activity.StartActivity("service.rollback");
        activity?.SetTag("tweak.id", applicationLog.TweakId);

        var rolledBackAt = DateTime.UtcNow;

        try
        {
            if (!TweakStateSerializer.TryDeserialize(applicationLog.BeforeState, out var state) ||
                state is not ServiceState beforeState)
            {
                throw new InvalidOperationException("Cannot deserialize before state for rollback");
            }

            if (string.IsNullOrWhiteSpace(beforeState.ServiceName) || string.IsNullOrWhiteSpace(beforeState.Status))
            {
                throw new InvalidOperationException("Invalid service state: name or status is null");
            }

            // Determine rollback action based on before state
            var rollbackAction = DetermineRollbackAction(beforeState.Status, applicationLog.ActionApplied);

            if (rollbackAction.HasValue)
            {
                await ApplyServiceActionAsync(beforeState.ServiceName, rollbackAction.Value, cancellationToken);
            }

            var restoredState = GetServiceState(beforeState.ServiceName);
            var restoredJson = TweakStateSerializer.Serialize(restoredState);

            _logger.LogInformation("Service rolled back: {Service} | Status: {Status}",
                beforeState.ServiceName, restoredState.Status);

            return new TweakRollbackResult
            {
                Success = true,
                RestoredState = restoredJson,
                RolledBackAtUtc = rolledBackAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Service rollback failed: {TweakId}", applicationLog.TweakId);

            return new TweakRollbackResult
            {
                Success = false,
                RestoredState = "{}",
                Error = ex.ToString(),
                RolledBackAtUtc = rolledBackAt
            };
        }
    }

    // Helper methods
    private static ServiceState GetServiceState(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            return new ServiceState
            {
                ServiceName = serviceName,
                Status = sc.Status.ToString()
            };
        }
        catch
        {
            return new ServiceState
            {
                ServiceName = serviceName,
                Status = "<notfound>"
            };
        }
    }

    private async Task ApplyServiceActionAsync(
        string serviceName,
        ServiceAction action,
        CancellationToken cancellationToken)
    {
        using var sc = new ServiceController(serviceName);

        switch (action)
        {
            case ServiceAction.Start:
                if (sc.Status != ServiceControllerStatus.Running)
                {
                    _logger.LogInformation("Starting service: {Service}", serviceName);
                    sc.Start();
                    await WaitForStatusAsync(sc, ServiceControllerStatus.Running, cancellationToken);
                }
                break;

            case ServiceAction.Stop:
                if (sc.Status != ServiceControllerStatus.Stopped)
                {
                    _logger.LogInformation("Stopping service: {Service}", serviceName);
                    sc.Stop();
                    await WaitForStatusAsync(sc, ServiceControllerStatus.Stopped, cancellationToken);
                }
                break;

            case ServiceAction.Restart:
                _logger.LogInformation("Restarting service: {Service}", serviceName);
                if (sc.Status == ServiceControllerStatus.Running)
                {
                    sc.Stop();
                    await WaitForStatusAsync(sc, ServiceControllerStatus.Stopped, cancellationToken);
                }
                sc.Start();
                await WaitForStatusAsync(sc, ServiceControllerStatus.Running, cancellationToken);
                break;

            case ServiceAction.Enable:
                _logger.LogInformation("Enabling service: {Service}", serviceName);
                await SetServiceStartupTypeAsync(serviceName, "auto", cancellationToken);
                break;

            case ServiceAction.Disable:
                _logger.LogInformation("Disabling service: {Service}", serviceName);
                await SetServiceStartupTypeAsync(serviceName, "disabled", cancellationToken);
                break;

            default:
                throw new NotSupportedException($"Unsupported service action: {action}");
        }
    }

    private async Task WaitForStatusAsync(
        ServiceController sc,
        ServiceControllerStatus desiredStatus,
        CancellationToken cancellationToken)
    {
        var timeout = DefaultTimeout;
        var startTime = DateTime.UtcNow;

        while (sc.Status != desiredStatus)
        {
            if (DateTime.UtcNow - startTime > timeout)
            {
                throw new System.TimeoutException(
                    $"Service {sc.ServiceName} did not reach {desiredStatus} within {timeout.TotalSeconds}s");
            }

            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(500, cancellationToken);
            sc.Refresh();
        }
    }

    private async Task SetServiceStartupTypeAsync(
        string serviceName,
        string startType,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = $"config \"{serviceName}\" start= {startType}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi) ??
            throw new InvalidOperationException("Failed to start sc.exe");

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            throw new InvalidOperationException(
                $"sc.exe failed with exit code {process.ExitCode}: {error}");
        }
    }

    private static bool IsActionNeeded(ServiceControllerStatus currentStatus, ServiceAction action)
    {
        return action switch
        {
            ServiceAction.Start => currentStatus != ServiceControllerStatus.Running,
            ServiceAction.Stop => currentStatus != ServiceControllerStatus.Stopped,
            ServiceAction.Restart => true, // Always meaningful
            ServiceAction.Enable => true,  // Can't easily check startup type without additional API calls
            ServiceAction.Disable => true, // Can't easily check startup type without additional API calls
            _ => true
        };
    }

    private static string GetExpectedStatus(ServiceAction action)
    {
        return action switch
        {
            ServiceAction.Start => "Running",
            ServiceAction.Stop => "Stopped",
            ServiceAction.Restart => "Running",
            ServiceAction.Enable => "Running", // Enabling doesn't change current status
            ServiceAction.Disable => "Stopped", // Disabling doesn't change current status
            _ => "Unknown"
        };
    }

    private static ServiceAction? DetermineRollbackAction(string beforeStatus, string? actionApplied)
    {
        // Parse the before status
        if (!Enum.TryParse<ServiceControllerStatus>(beforeStatus, out var status))
        {
            return null; // Can't determine rollback action
        }

        // Parse the action that was applied
        if (string.IsNullOrWhiteSpace(actionApplied) ||
            !Enum.TryParse<ServiceAction>(actionApplied, out var appliedAction))
        {
            return null;
        }

        // Determine inverse action
        return appliedAction switch
        {
            ServiceAction.Start => ServiceAction.Stop,
            ServiceAction.Stop => ServiceAction.Start,
            ServiceAction.Restart => null, // Can't meaningfully rollback a restart
            ServiceAction.Enable => ServiceAction.Disable,
            ServiceAction.Disable => ServiceAction.Enable,
            _ => null
        };
    }
}

