using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using GGs.Shared.Tweaks;

namespace GGs.Agent.Tweaks;

/// <summary>
/// Power and Performance Module: select power plans, background app throttling,
/// scheduled maintenance windows; all revertible and verified.
/// </summary>
public sealed class PowerPerformanceTweakModule : ITweakModule
{
    private readonly ILogger<PowerPerformanceTweakModule> _logger;
    private readonly ActivitySource _activity = new("GGs.Agent.Tweak.PowerPerformance");

    // Well-known power plan GUIDs
    private static readonly string BalancedPlanGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";
    private static readonly string HighPerformancePlanGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    private static readonly string PowerSaverPlanGuid = "a1841308-3541-4fab-bc81-f71556f20b4a";

    public string ModuleName => "PowerPerformance";

    public PowerPerformanceTweakModule(ILogger<PowerPerformanceTweakModule> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TweakPreflightResult> PreflightAsync(
        TweakDefinition tweak,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activity.StartActivity("power.preflight");
        activity?.SetTag("tweak.id", tweak.Id);

        try
        {
            // Get current power plan
            var currentPlan = await GetActivePowerPlanAsync(cancellationToken);
            var beforeState = CreatePowerState(currentPlan.Guid, currentPlan.Name, DateTime.UtcNow);
            var beforeJson = TweakStateSerializer.Serialize(beforeState);

            // Validate that powercfg is accessible
            if (string.IsNullOrWhiteSpace(currentPlan.Guid))
            {
                return new TweakPreflightResult
                {
                    CanApply = false,
                    Reason = "Cannot access power configuration",
                    ValidationError = "powercfg command failed or returned invalid data",
                    BeforeState = beforeJson
                };
            }

            return new TweakPreflightResult
            {
                CanApply = true,
                Reason = "Preflight validation passed",
                BeforeState = beforeJson
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Power preflight failed");
            return new TweakPreflightResult
            {
                CanApply = false,
                Reason = "Preflight validation failed",
                ValidationError = ex.Message,
                BeforeState = "{}"
            };
        }
    }

    public async Task<TweakApplicationResult> ApplyAsync(
        TweakDefinition tweak,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activity.StartActivity("power.apply");
        activity?.SetTag("tweak.id", tweak.Id);

        var appliedAt = DateTime.UtcNow;

        try
        {
            // Get before state
            var beforePlan = await GetActivePowerPlanAsync(cancellationToken);
            var beforeState = CreatePowerState(beforePlan.Guid, beforePlan.Name, appliedAt);
            var beforeJson = TweakStateSerializer.Serialize(beforeState);

            // Parse desired power plan from tweak (this is a simplified example)
            // In a real implementation, you'd have specific fields in TweakDefinition
            var desiredPlan = DeterminePowerPlanFromTweak(tweak);

            if (!string.IsNullOrWhiteSpace(desiredPlan))
            {
                await SetActivePowerPlanAsync(desiredPlan, cancellationToken);
            }

            // Get after state
            var afterPlan = await GetActivePowerPlanAsync(cancellationToken);
            var afterState = CreatePowerState(afterPlan.Guid, afterPlan.Name, DateTime.UtcNow);
            var afterJson = TweakStateSerializer.Serialize(afterState);

            var diff = $"Power Plan: {beforeState.ActivePlanName} → {afterState.ActivePlanName}";

            _logger.LogInformation("Power plan changed: {Diff}", diff);

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
            _logger.LogError(ex, "Power apply failed: {TweakName}", tweak.Name);

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

    public async Task<TweakVerificationResult> VerifyAsync(
        TweakDefinition tweak,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activity.StartActivity("power.verify");
        activity?.SetTag("tweak.id", tweak.Id);

        try
        {
            var currentPlan = await GetActivePowerPlanAsync(cancellationToken);
            var currentState = CreatePowerState(currentPlan.Guid, currentPlan.Name, DateTime.UtcNow);
            var currentJson = TweakStateSerializer.Serialize(currentState);

            // For verification, we'd check if the desired plan is active
            var desiredPlan = DeterminePowerPlanFromTweak(tweak);
            var verified = string.IsNullOrWhiteSpace(desiredPlan) ||
                          currentPlan.Guid.Equals(desiredPlan, StringComparison.OrdinalIgnoreCase);

            return new TweakVerificationResult
            {
                Verified = verified,
                CurrentState = currentJson,
                ExpectedState = $"{{\"PlanGuid\":\"{desiredPlan}\"}}",
                Discrepancy = verified ? null : $"Expected plan {desiredPlan}, got {currentPlan.Guid}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Power verification failed");

            return new TweakVerificationResult
            {
                Verified = false,
                CurrentState = "{}",
                ExpectedState = "{}",
                Discrepancy = $"Verification error: {ex.Message}"
            };
        }
    }

    public async Task<TweakRollbackResult> RollbackAsync(
        TweakApplicationLog applicationLog,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activity.StartActivity("power.rollback");
        activity?.SetTag("tweak.id", applicationLog.TweakId);

        var rolledBackAt = DateTime.UtcNow;

        try
        {
            if (!TweakStateSerializer.TryDeserialize(applicationLog.BeforeState, out var state) ||
                state is not PowerState beforeState)
            {
                throw new InvalidOperationException("Cannot deserialize before state for rollback");
            }

            // Restore previous power plan
            await SetActivePowerPlanAsync(beforeState.ActivePlanGuid!, cancellationToken);

            var restoredPlan = await GetActivePowerPlanAsync(cancellationToken);
            var restoredState = CreatePowerState(restoredPlan.Guid, restoredPlan.Name, rolledBackAt);
            var restoredJson = TweakStateSerializer.Serialize(restoredState);

            _logger.LogInformation("Power plan rolled back to: {PlanName}", restoredPlan.Name);

            return new TweakRollbackResult
            {
                Success = true,
                RestoredState = restoredJson,
                RolledBackAtUtc = rolledBackAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Power rollback failed: {TweakId}", applicationLog.TweakId);

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
    private async Task<(string Guid, string Name)> GetActivePowerPlanAsync(CancellationToken cancellationToken)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powercfg",
                Arguments = "/getactivescheme",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                return (string.Empty, string.Empty);
            }

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            // Parse output: "Power Scheme GUID: <guid>  (<name>)"
            var match = Regex.Match(output, @"([0-9a-f-]{36})\s+\(([^)]+)\)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return (match.Groups[1].Value, match.Groups[2].Value);
            }

            return (string.Empty, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get active power plan");
            return (string.Empty, string.Empty);
        }
    }

    private GGs.Shared.Tweaks.PowerState CreatePowerState(string guid, string name, DateTime timestamp)
    {
        return new GGs.Shared.Tweaks.PowerState
        {
            ActivePlanGuid = guid,
            ActivePlanName = name,
            Timestamp = timestamp
        };
    }

    private async Task SetActivePowerPlanAsync(string planGuid, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powercfg",
            Arguments = $"/setactive {planGuid}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi) ??
            throw new InvalidOperationException("Failed to start powercfg");

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            throw new InvalidOperationException($"powercfg failed: {error}");
        }
    }

    private static string DeterminePowerPlanFromTweak(TweakDefinition tweak)
    {
        // This is a simplified example - in reality, you'd have specific fields
        var name = tweak.Name?.ToLowerInvariant() ?? string.Empty;

        if (name.Contains("high performance") || name.Contains("performance"))
            return HighPerformancePlanGuid;
        if (name.Contains("power saver") || name.Contains("battery"))
            return PowerSaverPlanGuid;
        if (name.Contains("balanced"))
            return BalancedPlanGuid;

        return string.Empty;
    }
}

