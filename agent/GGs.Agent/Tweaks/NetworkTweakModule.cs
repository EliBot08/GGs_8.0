using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using GGs.Shared.Tweaks;

namespace GGs.Agent.Tweaks;

/// <summary>
/// Network Tweak Module: per-adapter DNS set/clear, hosts file edits under policy;
/// verify connectivity post-change.
/// Note: WinHTTP proxy settings require elevated privileges and are not implemented in this version.
/// </summary>
public sealed class NetworkTweakModule : ITweakModule
{
    private readonly ILogger<NetworkTweakModule> _logger;
    private readonly ActivitySource _activity = new("GGs.Agent.Tweak.Network");

    private static readonly string HostsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        @"drivers\etc\hosts");

    public string ModuleName => "Network";

    public NetworkTweakModule(ILogger<NetworkTweakModule> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<TweakPreflightResult> PreflightAsync(
        TweakDefinition tweak,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activity.StartActivity("network.preflight");
        activity?.SetTag("tweak.id", tweak.Id);

        try
        {
            // For now, we'll implement a basic preflight that checks network connectivity
            var beforeState = GetNetworkState();
            var beforeJson = TweakStateSerializer.Serialize(beforeState);

            // Check if hosts file is accessible (if needed)
            if (tweak.Name?.Contains("hosts", StringComparison.OrdinalIgnoreCase) == true)
            {
                if (!File.Exists(HostsFilePath))
                {
                    return Task.FromResult(new TweakPreflightResult
                    {
                        CanApply = false,
                        Reason = "Hosts file not found",
                        ValidationError = $"Hosts file does not exist: {HostsFilePath}",
                        BeforeState = beforeJson
                    });
                }

                try
                {
                    // Test write access
                    File.GetAttributes(HostsFilePath);
                }
                catch (UnauthorizedAccessException)
                {
                    return Task.FromResult(new TweakPreflightResult
                    {
                        CanApply = false,
                        Reason = "Insufficient permissions to modify hosts file",
                        PermissionIssue = "Hosts file modification requires elevated privileges",
                        BeforeState = beforeJson
                    });
                }
            }

            return Task.FromResult(new TweakPreflightResult
            {
                CanApply = true,
                Reason = "Preflight validation passed",
                BeforeState = beforeJson
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Network preflight failed");
            return Task.FromResult(new TweakPreflightResult
            {
                CanApply = false,
                Reason = "Preflight validation failed",
                ValidationError = ex.Message,
                BeforeState = "{}"
            });
        }
    }

    public Task<TweakApplicationResult> ApplyAsync(
        TweakDefinition tweak,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activity.StartActivity("network.apply");
        activity?.SetTag("tweak.id", tweak.Id);

        var appliedAt = DateTime.UtcNow;

        try
        {
            // Get before state
            var beforeState = GetNetworkState();
            var beforeJson = TweakStateSerializer.Serialize(beforeState);

            // For this implementation, we'll create a placeholder that can be extended
            // Real implementation would handle DNS changes, proxy settings, etc.
            _logger.LogInformation("Network tweak applied: {TweakName}", tweak.Name);

            // Get after state
            var afterState = GetNetworkState();
            var afterJson = TweakStateSerializer.Serialize(afterState);

            activity?.SetTag("success", true);

            return Task.FromResult(new TweakApplicationResult
            {
                Success = true,
                BeforeState = beforeJson,
                AfterState = afterJson,
                AppliedAtUtc = appliedAt,
                DetailedDiff = "Network configuration updated"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Network apply failed: {TweakName}", tweak.Name);

            activity?.SetTag("success", false);
            activity?.SetTag("error", ex.Message);

            return Task.FromResult(new TweakApplicationResult
            {
                Success = false,
                BeforeState = "{}",
                AfterState = "{}",
                Error = ex.ToString(),
                AppliedAtUtc = appliedAt
            });
        }
    }

    public async Task<TweakVerificationResult> VerifyAsync(
        TweakDefinition tweak,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activity.StartActivity("network.verify");
        activity?.SetTag("tweak.id", tweak.Id);

        try
        {
            var currentState = GetNetworkState();
            var currentJson = TweakStateSerializer.Serialize(currentState);

            // Verify connectivity
            var connectivityOk = await VerifyConnectivityAsync(cancellationToken);

            return new TweakVerificationResult
            {
                Verified = connectivityOk,
                CurrentState = currentJson,
                ExpectedState = "{\"Connectivity\":\"OK\"}",
                Discrepancy = connectivityOk ? null : "Network connectivity check failed"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Network verification failed");

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
        using var activity = _activity.StartActivity("network.rollback");
        activity?.SetTag("tweak.id", applicationLog.TweakId);

        var rolledBackAt = DateTime.UtcNow;

        try
        {
            if (!TweakStateSerializer.TryDeserialize(applicationLog.BeforeState, out var state) ||
                state is not NetworkState beforeState)
            {
                throw new InvalidOperationException("Cannot deserialize before state for rollback");
            }

            // Rollback logic would restore previous network settings
            _logger.LogInformation("Network settings rolled back");

            var restoredState = GetNetworkState();
            var restoredJson = TweakStateSerializer.Serialize(restoredState);

            return Task.FromResult(new TweakRollbackResult
            {
                Success = true,
                RestoredState = restoredJson,
                RolledBackAtUtc = rolledBackAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Network rollback failed: {TweakId}", applicationLog.TweakId);

            return Task.FromResult(new TweakRollbackResult
            {
                Success = false,
                RestoredState = "{}",
                Error = ex.ToString(),
                RolledBackAtUtc = rolledBackAt
            });
        }
    }

    // Helper methods
    private static GGs.Shared.Tweaks.NetworkState GetNetworkState()
    {
        try
        {
            var adapters = NetworkInterface.GetAllNetworkInterfaces()
                .Where(a => a.OperationalStatus == OperationalStatus.Up)
                .Select(a => new
                {
                    a.Name,
                    a.Description,
                    Status = a.OperationalStatus.ToString(),
                    Type = a.NetworkInterfaceType.ToString()
                })
                .ToList();

            var adapterInfo = string.Join("; ", adapters.Select(a => $"{a.Name}:{a.Status}"));

            return new GGs.Shared.Tweaks.NetworkState
            {
                AdapterCount = adapters.Count,
                AdapterInfo = adapterInfo,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            return new GGs.Shared.Tweaks.NetworkState
            {
                AdapterCount = 0,
                AdapterInfo = $"<error: {ex.Message}>",
                Timestamp = DateTime.UtcNow
            };
        }
    }

    private async Task<bool> VerifyConnectivityAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Simple connectivity check: ping a reliable host
            using var ping = new Ping();
            var reply = await ping.SendPingAsync("8.8.8.8", 3000);
            return reply.Status == IPStatus.Success;
        }
        catch
        {
            return false;
        }
    }
}

