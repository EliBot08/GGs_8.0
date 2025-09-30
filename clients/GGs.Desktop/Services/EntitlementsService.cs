using System;
using System.Collections.Generic;
using System.Linq;
using GGs.Shared.Enums;
using GGs.Shared.Api;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace GGs.Desktop.Services;

// Using shared Capability enum from GGs.Shared.Enums

public static class EntitlementsService
{
    private static readonly object _gate = new();
    private static string[] _roles = Array.Empty<string>();
    private static LicenseTier _tier = LicenseTier.Basic;
    private static GGs.Shared.Api.Entitlements? _serverEntitlements;

    public static event EventHandler<GGs.Shared.Api.Entitlements>? Changed;
    public static event EventHandler<GGs.Shared.Api.Entitlements?>? ServerEntitlementsChanged;

    public static void Initialize(LicenseTier? initialTier = null, string[]? initialRoles = null)
    {
        bool dirty = false;
        lock (_gate)
        {
            if (initialTier.HasValue && initialTier.Value != _tier)
            {
                _tier = initialTier.Value; dirty = true;
            }
            if (initialRoles != null)
            {
                var norm = Normalize(initialRoles);
                // Enterprise demo override: force Owner role if env var set
                try
                {
                    var demoOwner = Environment.GetEnvironmentVariable("GGS_DEMO_OWNER");
                    if (!string.IsNullOrWhiteSpace(demoOwner) && demoOwner.Equals("true", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!Array.Exists(norm, r => string.Equals(r, "Owner", StringComparison.OrdinalIgnoreCase)))
                        {
                            norm = norm.Concat(new[] { "Owner" }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                        }
                    }
                }
                catch { }
                if (!SequenceEqual(norm, _roles)) { _roles = norm; dirty = true; }
            }
        }
        if (dirty) RaiseChanged();
    }

    public static void UpdateRoles(IEnumerable<string>? roles)
    {
        var arr = Normalize(roles);
        bool dirty = false;
        lock (_gate)
        {
            if (!SequenceEqual(arr, _roles)) { _roles = arr; dirty = true; }
        }
        if (dirty) RaiseChanged();
        try { GatingService.UpdateRoles(arr); } catch { }
    }

    public static void UpdateTier(LicenseTier tier)
    {
        bool dirty = false;
        lock (_gate)
        {
            if (_tier != tier) { _tier = tier; dirty = true; }
        }
        if (dirty) RaiseChanged();
    }

    public static async Task RefreshFromServerAsync(HttpClient http, string accessToken, CancellationToken ct = default)
    {
        try
        {
            var client = new EntitlementsClient(http);
            var ent = await client.FetchAsync(accessToken, ct);
            _serverEntitlements = ent;
            
            // Update local state from server entitlements
            if (ent != null)
            {
                UpdateFromServerEntitlements(ent);
            }
            
            try { ServerEntitlementsChanged?.Invoke(null, ent); } catch { }
        }
        catch { }
    }

    private static void UpdateFromServerEntitlements(GGs.Shared.Api.Entitlements serverEntitlements)
    {
        bool dirty = false;
        lock (_gate)
        {
            // Update tier based on server response
            if (serverEntitlements.LicenseTier != _tier)
            {
                _tier = serverEntitlements.LicenseTier;
                dirty = true;
            }

            // Update roles based on server response
            var serverRoles = serverEntitlements.Roles ?? Array.Empty<string>();
            var normalizedRoles = Normalize(serverRoles);
            if (!SequenceEqual(normalizedRoles, _roles))
            {
                _roles = normalizedRoles;
                dirty = true;
            }
        }
        
        if (dirty) RaiseChanged();
    }

    public static GGs.Shared.Api.Entitlements? GetServerEntitlements()
    {
        return _serverEntitlements;
    }

    public static bool HasRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role)) return false;
        var r = role.Trim();
        lock (_gate)
        {
            return Array.Exists(_roles, rr => string.Equals(rr, r, StringComparison.OrdinalIgnoreCase));
        }
    }

    public static bool IsAdmin => HasRole("Admin") || HasRole("Administrator");
    public static bool IsModerator => HasRole("Moderator");
    public static bool IsManager => HasRole("Manager");
    public static bool IsSupport => HasRole("Support");

    public static LicenseTier CurrentTier { get { lock (_gate) return _tier; } }
    public static string[] CurrentRoles { get { lock (_gate) return _roles.ToArray(); } }

    public static bool HasCapability(GGs.Shared.Enums.Capability cap)
    {
        // Always allow viewing the dashboard
        if (cap == GGs.Shared.Enums.Capability.ViewDashboard) return true;
        // Admins can do everything regardless of tier.
        if (IsAdmin) return true;
        var tier = CurrentTier;
        // Role-based coarse gating
        if (IsManager)
        {
            return cap switch
            {
                GGs.Shared.Enums.Capability.ManageUsers => false, // Managers cannot manage users
                GGs.Shared.Enums.Capability.ManageTweaks => true,
                GGs.Shared.Enums.Capability.ExecuteTweaks => true,
                GGs.Shared.Enums.Capability.ManageLicenses => true,
                GGs.Shared.Enums.Capability.RemoteManagement => tier == LicenseTier.Enterprise, // Enterprise only
                GGs.Shared.Enums.Capability.ViewAnalytics => tier != LicenseTier.Basic, // Basic hides analytics
                GGs.Shared.Enums.Capability.ApplyNetworkProfile => true,
                GGs.Shared.Enums.Capability.UseProfiles => true,
                _ => false
            };
        }
        if (IsSupport)
        {
            return cap switch
            {
                GGs.Shared.Enums.Capability.ViewDashboard => true,
                GGs.Shared.Enums.Capability.ViewAnalytics => tier != LicenseTier.Basic,
                _ => false
            };
        }
        // Regular user: only dashboard, settings is not a capability (always available)
        return cap switch
        {
            GGs.Shared.Enums.Capability.ViewDashboard => true,
            _ => false
        };
    }

    public static Entitlements GetCurrentEntitlements()
    {
        lock (_gate)
        {
            var caps = Enum.GetValues(typeof(Capability)).Cast<Capability>()
                .Where(HasCapability).ToHashSet();
            return new GGs.Shared.Api.Entitlements
            {
                LicenseTier = _tier,
                Roles = _roles.ToArray(),
                RoleName = _roles.FirstOrDefault() ?? string.Empty,
                Role = Enum.TryParse<RbacRole>(_roles.FirstOrDefault(), out var role) ? role : RbacRole.Basic,
                EliBot = new EliBotQuota { DailyQuestionLimit = _tier == LicenseTier.Enterprise ? int.MaxValue : 10 },
                Monitoring = new MonitoringCapabilities { RealTimeMonitoring = _tier >= LicenseTier.Pro },
                Tweaks = new TweakCapabilities { CanExecuteTweaks = _tier >= LicenseTier.Basic },
                Themes = new ThemeCapabilities { DarkMode = true, LightMode = true }
            };
        }
    }

    private static void RaiseChanged()
    {
        try { Changed?.Invoke(null, GetCurrentEntitlements()); } catch { }
    }

    private static string[] Normalize(IEnumerable<string>? roles)
    {
        return roles == null
            ? Array.Empty<string>()
            : roles.Where(s => !string.IsNullOrWhiteSpace(s))
                   .Select(s => s.Trim())
                   .Distinct(StringComparer.OrdinalIgnoreCase)
                   .ToArray();
    }

    private static bool SequenceEqual(string[] a, string[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (!string.Equals(a[i], b[i], StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }
}

