using System;
using System.Diagnostics;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using GGs.Shared.Tweaks;

namespace GGs.Agent.Tweaks;

/// <summary>
/// Security Health Module: surface Defender and Firewall status via supported APIs;
/// do not disable protections—only report and remediate within approved bounds.
/// </summary>
public sealed class SecurityHealthTweakModule : ITweakModule
{
    private readonly ILogger<SecurityHealthTweakModule> _logger;
    private readonly ActivitySource _activity = new("GGs.Agent.Tweak.SecurityHealth");

    public string ModuleName => "SecurityHealth";

    public SecurityHealthTweakModule(ILogger<SecurityHealthTweakModule> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TweakPreflightResult> PreflightAsync(
        TweakDefinition tweak,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activity.StartActivity("security.preflight");
        activity?.SetTag("tweak.id", tweak.Id);

        try
        {
            // Get current security state
            var securityState = await GetSecurityStateAsync(cancellationToken);
            var beforeJson = TweakStateSerializer.Serialize(securityState);

            // Policy: Never allow disabling security features
            if (tweak.Name?.Contains("disable", StringComparison.OrdinalIgnoreCase) == true &&
                (tweak.Name.Contains("defender", StringComparison.OrdinalIgnoreCase) ||
                 tweak.Name.Contains("firewall", StringComparison.OrdinalIgnoreCase)))
            {
                return new TweakPreflightResult
                {
                    CanApply = false,
                    Reason = "Security feature disablement blocked by policy",
                    PolicyViolation = "Cannot disable Windows Defender or Firewall",
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
            _logger.LogError(ex, "Security preflight failed");
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
        using var activity = _activity.StartActivity("security.apply");
        activity?.SetTag("tweak.id", tweak.Id);

        var appliedAt = DateTime.UtcNow;

        try
        {
            // Get before state
            var beforeState = await GetSecurityStateAsync(cancellationToken);
            var beforeJson = TweakStateSerializer.Serialize(beforeState);

            // This module is read-only by design - it reports status but doesn't modify
            _logger.LogInformation("Security health check performed: {TweakName}", tweak.Name);

            // Get after state (should be same as before for read-only operations)
            var afterState = await GetSecurityStateAsync(cancellationToken);
            var afterJson = TweakStateSerializer.Serialize(afterState);

            activity?.SetTag("success", true);

            return new TweakApplicationResult
            {
                Success = true,
                BeforeState = beforeJson,
                AfterState = afterJson,
                AppliedAtUtc = appliedAt,
                DetailedDiff = "Security health status retrieved"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Security apply failed: {TweakName}", tweak.Name);

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
        using var activity = _activity.StartActivity("security.verify");
        activity?.SetTag("tweak.id", tweak.Id);

        try
        {
            var currentState = await GetSecurityStateAsync(cancellationToken);
            var currentJson = TweakStateSerializer.Serialize(currentState);

            // Verify that critical security features are enabled
            var defenderRunning = currentState.DefenderServiceStatus == "Running";
            var firewallEnabled = currentState.FirewallEnabled;

            var verified = defenderRunning && firewallEnabled;
            var discrepancy = verified ? null :
                $"Defender: {currentState.DefenderServiceStatus}, Firewall: {(firewallEnabled ? "Enabled" : "Disabled")}";

            return new TweakVerificationResult
            {
                Verified = verified,
                CurrentState = currentJson,
                ExpectedState = "{\"DefenderRunning\":true,\"FirewallEnabled\":true}",
                Discrepancy = discrepancy
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Security verification failed");

            return new TweakVerificationResult
            {
                Verified = false,
                CurrentState = "{}",
                ExpectedState = "{}",
                Discrepancy = $"Verification error: {ex.Message}"
            };
        }
    }

    public Task<TweakRollbackResult> RollbackAsync(
        TweakApplicationLog applicationLog,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activity.StartActivity("security.rollback");
        activity?.SetTag("tweak.id", applicationLog.TweakId);

        var rolledBackAt = DateTime.UtcNow;

        // Security health module is read-only, so rollback is a no-op
        _logger.LogInformation("Security health rollback (no-op): {TweakId}", applicationLog.TweakId);

        return Task.FromResult(new TweakRollbackResult
        {
            Success = true,
            RestoredState = applicationLog.BeforeState ?? "{}",
            RolledBackAtUtc = rolledBackAt
        });
    }

    // Helper methods
    private async Task<GGs.Shared.Tweaks.SecurityHealthState> GetSecurityStateAsync(CancellationToken cancellationToken)
    {
        var state = new GGs.Shared.Tweaks.SecurityHealthState
        {
            DefenderServiceStatus = "Unknown",
            FirewallEnabled = false,
            RealTimeProtectionEnabled = false,
            Timestamp = DateTime.UtcNow
        };

        try
        {
            // Check Windows Defender service status
            state.DefenderServiceStatus = await GetServiceStatusAsync("WinDefend", cancellationToken);

            // Check Firewall status via WMI
            state.FirewallEnabled = await GetFirewallStatusAsync(cancellationToken);

            // Check Real-Time Protection via WMI
            state.RealTimeProtectionEnabled = await GetRealTimeProtectionStatusAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve complete security state");
        }

        return state;
    }

    private async Task<string> GetServiceStatusAsync(string serviceName, CancellationToken cancellationToken)
    {
        try
        {
            using var sc = new System.ServiceProcess.ServiceController(serviceName);
            var status = await Task.Run(() => sc.Status.ToString(), cancellationToken);
            return status ?? "Unknown";
        }
        catch
        {
            return "NotFound";
        }
    }

    private async Task<bool> GetFirewallStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await Task.Run(() =>
            {
                using var searcher = new ManagementObjectSearcher(
                    @"root\StandardCimv2",
                    "SELECT * FROM MSFT_NetFirewallProfile WHERE Name='Domain'");

                foreach (ManagementObject obj in searcher.Get())
                {
                    var enabled = obj["Enabled"];
                    return enabled != null && Convert.ToBoolean(enabled);
                }

                return false;
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query firewall status");
            return false;
        }
    }

    private async Task<bool> GetRealTimeProtectionStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await Task.Run(() =>
            {
                using var searcher = new ManagementObjectSearcher(
                    @"root\Microsoft\Windows\Defender",
                    "SELECT * FROM MSFT_MpPreference");

                foreach (ManagementObject obj in searcher.Get())
                {
                    var rtpEnabled = obj["DisableRealtimeMonitoring"];
                    // Note: DisableRealtimeMonitoring = false means RTP is enabled
                    return rtpEnabled != null && !Convert.ToBoolean(rtpEnabled);
                }

                return false;
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query real-time protection status");
            return false;
        }
    }
}

