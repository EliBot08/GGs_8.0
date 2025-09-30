using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using GGs.Server.Data;
using GGs.Server.Models;
using GGs.Shared.Api;
using GGs.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace GGs.Server.Services;

public interface IEntitlementsService
{
    Task<Entitlements> ComputeAsync(ClaimsPrincipal principal, CancellationToken ct = default);
    Task<Entitlements> ComputeForUserAsync(string userId, IEnumerable<string> roles, CancellationToken ct = default);
}

public sealed class EntitlementsService : IEntitlementsService
{
    private readonly AppDbContext _db;
    private readonly ILogger<EntitlementsService> _logger;
    private readonly IEntitlementsCache _cache;

    public EntitlementsService(AppDbContext db, ILogger<EntitlementsService> logger, IEntitlementsCache cache)
    {
        _db = db;
        _logger = logger;
        _cache = cache;
    }

    public async Task<Entitlements> ComputeAsync(ClaimsPrincipal principal, CancellationToken ct = default)
    {
        var userId = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                     ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? string.Empty;
        var roles = principal.FindAll(ClaimTypes.Role).Select(r => r.Value).ToArray();
        return await ComputeForUserAsync(userId, roles, ct);
    }

    public async Task<Entitlements> ComputeForUserAsync(string userId, IEnumerable<string> roles, CancellationToken ct = default)
    {
        roles = roles?.ToArray() ?? Array.Empty<string>();
        if (!string.IsNullOrWhiteSpace(userId))
        {
            if (_cache.TryGet(userId, roles.ToArray(), out var cached))
            {
                return cached;
            }
        }
        var now = DateTime.UtcNow;

        // Determine highest active license tier for this user
        LicenseTier licenseTier = LicenseTier.Basic;
        try
        {
            var rec = await _db.Licenses.AsNoTracking()
                .Where(l => l.UserId == userId && l.Status == "Active" && (!l.ExpiresUtc.HasValue || l.ExpiresUtc > now))
                .OrderByDescending(l => TierRank(l.Tier))
                .FirstOrDefaultAsync(ct);
            if (rec != null)
            {
                if (Enum.TryParse<LicenseTier>(rec.Tier, ignoreCase: true, out var parsed))
                    licenseTier = parsed;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve license tier for user {UserId}", userId);
        }

        // Map roles to RBAC order of precedence
        var roleSet = new HashSet<string>(roles ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var rbac = RbacRoleFrom(roleSet, licenseTier);

        var ent = BuildBaseEntitlements(rbac);

        // Elevate selected capabilities based on license tier (progressive unlocks)
        ent = ElevateWithLicenseTier(ent, licenseTier);

        // Flags for client UX
        ent.Flags["licenseTier"] = licenseTier.ToString();
        ent.Flags["rbacRole"] = ent.RoleName;

        if (!string.IsNullOrWhiteSpace(userId))
        {
            _cache.Set(userId, roles?.ToArray() ?? Array.Empty<string>(), ent, TimeSpan.FromMinutes(5));
        }
        return ent;
    }

    private static int TierRank(string tier)
    {
        return (tier?.ToLowerInvariant()) switch
        {
            "admin" => 4,
            "enterprise" => 3,
            "pro" => 2,
            "basic" => 1,
            _ => 0
        };
    }

    private static RbacRole RbacRoleFrom(HashSet<string> roles, LicenseTier licenseTier)
    {
        if (roles.Contains("Owner")) return RbacRole.Owner;
        if (roles.Contains("Admin")) return RbacRole.Admin;
        if (roles.Contains("Moderator") || roles.Contains("Manager") || roles.Contains("Support")) return RbacRole.Moderator;
        if (roles.Contains("EnterpriseUser")) return RbacRole.Enterprise;
        if (roles.Contains("ProUser")) return RbacRole.Pro;
        if (roles.Contains("BasicUser") || roles.Contains("User")) return RbacRole.Basic;

        // Fallback to license tier if no RBAC role claim
        return licenseTier switch
        {
            LicenseTier.Enterprise => RbacRole.Enterprise,
            LicenseTier.Pro => RbacRole.Pro,
            _ => RbacRole.Basic
        };
    }

    private static Entitlements BuildBaseEntitlements(RbacRole role)
    {
        return role switch
        {
            RbacRole.Owner => new Entitlements
            {
                Role = role,
                RoleName = role.ToString(),
                EliBot = new EliBotQuota { DailyQuestionLimit = int.MaxValue, PredictiveOptimization = true, SystemDiagnostics = true, KnowledgeBaseManagement = true },
                Themes = new ThemeCapabilities { DefaultTheme = "founder", ThemeBuilder = true, AnimatedBackgrounds = true, AdvancedVisualizations = true },
                Monitoring = new MonitoringCapabilities { RealTimeCharts = true, HistoryDays = 365, CustomMetrics = true, TeamDashboards = true },
                Tweaks = new TweakCapabilities { AllowLowRisk = true, AllowMediumRisk = true, AllowHighRisk = true, AllowExperimental = true, CustomTweakCreation = true, TeamSharing = true, ApprovalWorkflows = true },
                RemoteControl = new RemoteControlCapabilities { LocalScripts = true, RemoteScripts = true, LiveSessions = true, JitElevation = true, MaxConcurrentSessions = 50 },
                Compliance = new ComplianceCapabilities { AuditRetentionDays = 3650, TamperEvidentLogs = true, EDiscoveryExport = true, DlpRules = true, LegalHolds = true },
                Collaboration = new CollaborationCapabilities { MaxCollaborators = 1000, TeamSpaces = true, ModerationQueue = true, OrgPublishing = true },
                Marketplace = new MarketplaceCapabilities { AccessCuratedFreePacks = true, AccessPaidPacks = true, PrivateCatalog = true, IntegrationHub = true },
                Enrollment = new DeviceEnrollmentCapabilities { MaxDevices = 1000, TwoFactorRequired = true, SsoEnabled = true, ScimSync = true, HardwareAttestation = true }
            },
            RbacRole.Admin => new Entitlements
            {
                Role = role,
                RoleName = role.ToString(),
                EliBot = new EliBotQuota { DailyQuestionLimit = int.MaxValue, PredictiveOptimization = true, SystemDiagnostics = true, KnowledgeBaseManagement = false },
                Themes = new ThemeCapabilities { DefaultTheme = "admin", ThemeBuilder = true, AnimatedBackgrounds = false, AdvancedVisualizations = true },
                Monitoring = new MonitoringCapabilities { RealTimeCharts = true, HistoryDays = 365, CustomMetrics = true, TeamDashboards = true },
                Tweaks = new TweakCapabilities { AllowLowRisk = true, AllowMediumRisk = true, AllowHighRisk = true, AllowExperimental = true, CustomTweakCreation = true, TeamSharing = true, ApprovalWorkflows = true },
                RemoteControl = new RemoteControlCapabilities { LocalScripts = true, RemoteScripts = true, LiveSessions = true, JitElevation = true, MaxConcurrentSessions = 20 },
                Compliance = new ComplianceCapabilities { AuditRetentionDays = 1825, TamperEvidentLogs = true, EDiscoveryExport = true, DlpRules = true, LegalHolds = false },
                Collaboration = new CollaborationCapabilities { MaxCollaborators = 200, TeamSpaces = true, ModerationQueue = true, OrgPublishing = true },
                Marketplace = new MarketplaceCapabilities { AccessCuratedFreePacks = true, AccessPaidPacks = true, PrivateCatalog = true, IntegrationHub = true },
                Enrollment = new DeviceEnrollmentCapabilities { MaxDevices = 500, TwoFactorRequired = true, SsoEnabled = true, ScimSync = true, HardwareAttestation = true }
            },
            RbacRole.Moderator => new Entitlements
            {
                Role = role,
                RoleName = role.ToString(),
                EliBot = new EliBotQuota { DailyQuestionLimit = 200, PredictiveOptimization = true, SystemDiagnostics = true, KnowledgeBaseManagement = false },
                Themes = new ThemeCapabilities { DefaultTheme = "support", ThemeBuilder = false, AnimatedBackgrounds = false, AdvancedVisualizations = true },
                Monitoring = new MonitoringCapabilities { RealTimeCharts = true, HistoryDays = 90, CustomMetrics = false, TeamDashboards = true },
                Tweaks = new TweakCapabilities { AllowLowRisk = true, AllowMediumRisk = true, AllowHighRisk = true, AllowExperimental = false, CustomTweakCreation = false, TeamSharing = true, ApprovalWorkflows = true },
                RemoteControl = new RemoteControlCapabilities { LocalScripts = true, RemoteScripts = true, LiveSessions = true, JitElevation = false, MaxConcurrentSessions = 5 },
                Compliance = new ComplianceCapabilities { AuditRetentionDays = 180, TamperEvidentLogs = true, EDiscoveryExport = false, DlpRules = false, LegalHolds = false },
                Collaboration = new CollaborationCapabilities { MaxCollaborators = 50, TeamSpaces = true, ModerationQueue = true, OrgPublishing = false },
                Marketplace = new MarketplaceCapabilities { AccessCuratedFreePacks = true, AccessPaidPacks = true, PrivateCatalog = false, IntegrationHub = false },
                Enrollment = new DeviceEnrollmentCapabilities { MaxDevices = 50, TwoFactorRequired = true, SsoEnabled = false, ScimSync = false, HardwareAttestation = false }
            },
            RbacRole.Enterprise => new Entitlements
            {
                Role = role,
                RoleName = role.ToString(),
                EliBot = new EliBotQuota { DailyQuestionLimit = 100, PredictiveOptimization = true, SystemDiagnostics = true, KnowledgeBaseManagement = false },
                Themes = new ThemeCapabilities { DefaultTheme = "corporate", ThemeBuilder = false, AnimatedBackgrounds = false, AdvancedVisualizations = true },
                Monitoring = new MonitoringCapabilities { RealTimeCharts = true, HistoryDays = 180, CustomMetrics = true, TeamDashboards = true },
                Tweaks = new TweakCapabilities { AllowLowRisk = true, AllowMediumRisk = true, AllowHighRisk = true, AllowExperimental = false, CustomTweakCreation = true, TeamSharing = true, ApprovalWorkflows = true },
                RemoteControl = new RemoteControlCapabilities { LocalScripts = true, RemoteScripts = true, LiveSessions = true, JitElevation = true, MaxConcurrentSessions = 10 },
                Compliance = new ComplianceCapabilities { AuditRetentionDays = 365, TamperEvidentLogs = true, EDiscoveryExport = true, DlpRules = true, LegalHolds = false },
                Collaboration = new CollaborationCapabilities { MaxCollaborators = 100, TeamSpaces = true, ModerationQueue = false, OrgPublishing = false },
                Marketplace = new MarketplaceCapabilities { AccessCuratedFreePacks = true, AccessPaidPacks = true, PrivateCatalog = true, IntegrationHub = true },
                Enrollment = new DeviceEnrollmentCapabilities { MaxDevices = 100, TwoFactorRequired = true, SsoEnabled = true, ScimSync = true, HardwareAttestation = true }
            },
            RbacRole.Pro => new Entitlements
            {
                Role = role,
                RoleName = role.ToString(),
                EliBot = new EliBotQuota { DailyQuestionLimit = 25, PredictiveOptimization = false, SystemDiagnostics = false, KnowledgeBaseManagement = false },
                Themes = new ThemeCapabilities { DefaultTheme = "gaming", ThemeBuilder = false, AnimatedBackgrounds = false, AdvancedVisualizations = false },
                Monitoring = new MonitoringCapabilities { RealTimeCharts = true, HistoryDays = 7, CustomMetrics = false, TeamDashboards = false },
                Tweaks = new TweakCapabilities { AllowLowRisk = true, AllowMediumRisk = true, AllowHighRisk = false, AllowExperimental = false, CustomTweakCreation = true, TeamSharing = false, ApprovalWorkflows = false },
                RemoteControl = new RemoteControlCapabilities { LocalScripts = true, RemoteScripts = true, LiveSessions = false, JitElevation = false, MaxConcurrentSessions = 1 },
                Compliance = new ComplianceCapabilities { AuditRetentionDays = 30, TamperEvidentLogs = false, EDiscoveryExport = false, DlpRules = false, LegalHolds = false },
                Collaboration = new CollaborationCapabilities { MaxCollaborators = 5, TeamSpaces = false, ModerationQueue = false, OrgPublishing = false },
                Marketplace = new MarketplaceCapabilities { AccessCuratedFreePacks = true, AccessPaidPacks = true, PrivateCatalog = false, IntegrationHub = false },
                Enrollment = new DeviceEnrollmentCapabilities { MaxDevices = 3, TwoFactorRequired = true, SsoEnabled = false, ScimSync = false, HardwareAttestation = false }
            },
            _ => new Entitlements
            {
                Role = role,
                RoleName = role.ToString(),
                EliBot = new EliBotQuota { DailyQuestionLimit = 5, PredictiveOptimization = false, SystemDiagnostics = false, KnowledgeBaseManagement = false },
                Themes = new ThemeCapabilities { DefaultTheme = "dark", ThemeBuilder = false, AnimatedBackgrounds = false, AdvancedVisualizations = false },
                Monitoring = new MonitoringCapabilities { RealTimeCharts = false, HistoryDays = 0, CustomMetrics = false, TeamDashboards = false },
                Tweaks = new TweakCapabilities { AllowLowRisk = true, AllowMediumRisk = false, AllowHighRisk = false, AllowExperimental = false, CustomTweakCreation = false, TeamSharing = false, ApprovalWorkflows = false },
                RemoteControl = new RemoteControlCapabilities { LocalScripts = true, RemoteScripts = false, LiveSessions = false, JitElevation = false, MaxConcurrentSessions = 0 },
                Compliance = new ComplianceCapabilities { AuditRetentionDays = 7, TamperEvidentLogs = false, EDiscoveryExport = false, DlpRules = false, LegalHolds = false },
                Collaboration = new CollaborationCapabilities { MaxCollaborators = 0, TeamSpaces = false, ModerationQueue = false, OrgPublishing = false },
                Marketplace = new MarketplaceCapabilities { AccessCuratedFreePacks = true, AccessPaidPacks = false, PrivateCatalog = false, IntegrationHub = false },
                Enrollment = new DeviceEnrollmentCapabilities { MaxDevices = 1, TwoFactorRequired = false, SsoEnabled = false, ScimSync = false, HardwareAttestation = false }
            }
        };
    }

    private static Entitlements ElevateWithLicenseTier(Entitlements e, LicenseTier tier)
    {
        // Combine license-based enhancements with role baseline. Elevation is additive (logical OR / max where meaningful).
        return tier switch
        {
            LicenseTier.Enterprise => new Entitlements
            {
                Role = e.Role,
                RoleName = e.RoleName,
                EliBot = new EliBotQuota
                {
                    DailyQuestionLimit = Math.Max(e.EliBot.DailyQuestionLimit, 100),
                    PredictiveOptimization = e.EliBot.PredictiveOptimization || true,
                    SystemDiagnostics = e.EliBot.SystemDiagnostics || true,
                    KnowledgeBaseManagement = e.EliBot.KnowledgeBaseManagement
                },
                Themes = e.Themes,
                Monitoring = new MonitoringCapabilities
                {
                    RealTimeCharts = true,
                    HistoryDays = Math.Max(e.Monitoring.HistoryDays, 180),
                    CustomMetrics = true,
                    TeamDashboards = true
                },
                Tweaks = new TweakCapabilities
                {
                    AllowLowRisk = true,
                    AllowMediumRisk = true,
                    AllowHighRisk = true,
                    AllowExperimental = e.Tweaks.AllowExperimental,
                    CustomTweakCreation = true,
                    TeamSharing = true,
                    ApprovalWorkflows = true
                },
                RemoteControl = new RemoteControlCapabilities
                {
                    LocalScripts = true,
                    RemoteScripts = true,
                    LiveSessions = true,
                    JitElevation = e.RemoteControl.JitElevation || true,
                    MaxConcurrentSessions = Math.Max(e.RemoteControl.MaxConcurrentSessions, 10)
                },
                Compliance = e.Compliance,
                Collaboration = e.Collaboration,
                Marketplace = e.Marketplace,
                Enrollment = new DeviceEnrollmentCapabilities
                {
                    MaxDevices = Math.Max(e.Enrollment.MaxDevices, 100),
                    TwoFactorRequired = true,
                    SsoEnabled = true,
                    ScimSync = true,
                    HardwareAttestation = e.Enrollment.HardwareAttestation || true
                },
                Flags = e.Flags
            },
            LicenseTier.Pro => new Entitlements
            {
                Role = e.Role,
                RoleName = e.RoleName,
                EliBot = new EliBotQuota
                {
                    DailyQuestionLimit = Math.Max(e.EliBot.DailyQuestionLimit, 25),
                    PredictiveOptimization = e.EliBot.PredictiveOptimization,
                    SystemDiagnostics = e.EliBot.SystemDiagnostics,
                    KnowledgeBaseManagement = e.EliBot.KnowledgeBaseManagement
                },
                Themes = e.Themes,
                Monitoring = new MonitoringCapabilities
                {
                    RealTimeCharts = true,
                    HistoryDays = Math.Max(e.Monitoring.HistoryDays, 7),
                    CustomMetrics = e.Monitoring.CustomMetrics,
                    TeamDashboards = e.Monitoring.TeamDashboards
                },
                Tweaks = new TweakCapabilities
                {
                    AllowLowRisk = true,
                    AllowMediumRisk = true,
                    AllowHighRisk = e.Tweaks.AllowHighRisk,
                    AllowExperimental = e.Tweaks.AllowExperimental,
                    CustomTweakCreation = true,
                    TeamSharing = e.Tweaks.TeamSharing,
                    ApprovalWorkflows = e.Tweaks.ApprovalWorkflows
                },
                RemoteControl = e.RemoteControl,
                Compliance = e.Compliance,
                Collaboration = e.Collaboration,
                Marketplace = e.Marketplace,
                Enrollment = new DeviceEnrollmentCapabilities
                {
                    MaxDevices = Math.Max(e.Enrollment.MaxDevices, 3),
                    TwoFactorRequired = true,
                    SsoEnabled = e.Enrollment.SsoEnabled,
                    ScimSync = e.Enrollment.ScimSync,
                    HardwareAttestation = e.Enrollment.HardwareAttestation
                },
                Flags = e.Flags
            },
            _ => e
        };
    }
}


