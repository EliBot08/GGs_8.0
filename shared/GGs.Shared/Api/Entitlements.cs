using GGs.Shared.Enums;

namespace GGs.Shared.Api;

public class Entitlements
{
    public LicenseTier LicenseTier { get; set; }
    public string[] Roles { get; set; } = Array.Empty<string>();
    public bool CanAccessAdvancedFeatures { get; set; }
    public bool CanManageUsers { get; set; }
    public bool CanViewAnalytics { get; set; }
    public bool CanExportData { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsValid { get; set; } = true;
    
    // Additional properties for desktop compatibility
    public string RoleName { get; set; } = string.Empty;
    public EliBotQuota EliBot { get; set; } = new();
    public MonitoringCapabilities Monitoring { get; set; } = new();
    public TweakCapabilities Tweaks { get; set; } = new();
    public RbacRole Role { get; set; } = RbacRole.Basic;
    public ThemeCapabilities Themes { get; set; } = new();
    public RemoteControlCapabilities RemoteControl { get; set; } = new();
    public ComplianceCapabilities Compliance { get; set; } = new();
    public CollaborationCapabilities Collaboration { get; set; } = new();
    public MarketplaceCapabilities Marketplace { get; set; } = new();
    public DeviceEnrollmentCapabilities Enrollment { get; set; } = new();
    public Dictionary<string, object> Flags { get; set; } = new();
}

public class EliBotQuota
{
    public int DailyQuestionLimit { get; set; } = int.MaxValue;
    public int MonthlyQuestionLimit { get; set; } = int.MaxValue;
    public int QuestionsUsedToday { get; set; }
    public int QuestionsUsedThisMonth { get; set; }
    public bool PredictiveOptimization { get; set; }
    public bool SystemDiagnostics { get; set; }
    public bool KnowledgeBaseManagement { get; set; }
}

public class MonitoringCapabilities
{
    public bool RealTimeMonitoring { get; set; }
    public bool HistoricalData { get; set; }
    public int MaxRetentionDays { get; set; } = 30;
    public bool CustomAlerts { get; set; }
    public bool RealTimeCharts { get; set; }
    public int HistoryDays { get; set; } = 30;
    public bool CustomMetrics { get; set; }
    public bool TeamDashboards { get; set; }
}

public class TweakCapabilities
{
    public bool CanExecuteTweaks { get; set; }
    public bool CanCreateTweaks { get; set; }
    public bool CanModifyTweaks { get; set; }
    public int MaxCustomTweaks { get; set; } = 10;
    public bool AllowLowRisk { get; set; }
    public bool AllowMediumRisk { get; set; }
    public bool AllowHighRisk { get; set; }
    public bool AllowExperimental { get; set; }
    public bool CustomTweakCreation { get; set; }
    public bool TeamSharing { get; set; }
    public bool ApprovalWorkflows { get; set; }
}

public class ThemeCapabilities
{
    public bool CustomThemes { get; set; }
    public bool DarkMode { get; set; } = true;
    public bool LightMode { get; set; } = true;
    public string DefaultTheme { get; set; } = "System";
    public bool ThemeBuilder { get; set; }
    public bool AnimatedBackgrounds { get; set; }
    public bool AdvancedVisualizations { get; set; }
}

public class RemoteControlCapabilities
{
    public bool RemoteAccess { get; set; }
    public bool ScreenSharing { get; set; }
    public bool FileTransfer { get; set; }
    public bool CommandExecution { get; set; }
    public int MaxConcurrentSessions { get; set; } = 1;
    public bool LocalScripts { get; set; }
    public bool RemoteScripts { get; set; }
    public bool LiveSessions { get; set; }
    public bool JitElevation { get; set; }
}

public class ComplianceCapabilities
{
    public bool AuditLogging { get; set; }
    public bool DataRetention { get; set; }
    public bool Encryption { get; set; }
    public bool ComplianceReporting { get; set; }
    public int AuditRetentionDays { get; set; } = 90;
    public bool TamperEvidentLogs { get; set; }
    public bool EDiscoveryExport { get; set; }
    public bool DlpRules { get; set; }
    public bool LegalHolds { get; set; }
}

public class CollaborationCapabilities
{
    public bool TeamWorkspaces { get; set; }
    public bool SharedProfiles { get; set; }
    public bool RealTimeCollaboration { get; set; }
    public bool CommentSystem { get; set; }
    public int MaxCollaborators { get; set; } = 10;
    public bool TeamSpaces { get; set; }
    public bool ModerationQueue { get; set; }
    public bool OrgPublishing { get; set; }
}

public class MarketplaceCapabilities
{
    public bool BrowseMarketplace { get; set; }
    public bool InstallTweaks { get; set; }
    public bool PublishTweaks { get; set; }
    public bool RateTweaks { get; set; }
    public bool AccessCuratedFreePacks { get; set; }
    public bool AccessPaidPacks { get; set; }
    public bool PrivateCatalog { get; set; }
    public bool IntegrationHub { get; set; }
}

public class DeviceEnrollmentCapabilities
{
    public bool AutoEnrollment { get; set; }
    public bool DeviceManagement { get; set; }
    public bool BulkEnrollment { get; set; }
    public bool DeviceTracking { get; set; }
    public int MaxDevices { get; set; } = 100;
    public bool TwoFactorRequired { get; set; }
    public bool SsoEnabled { get; set; }
    public bool ScimSync { get; set; }
    public bool HardwareAttestation { get; set; }
}