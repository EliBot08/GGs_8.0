namespace GGs.Shared.SystemIntelligence;

/// <summary>
/// Source of detected tweaks
/// </summary>
public enum TweakSource
{
    Unknown = 0,
    Registry = 1,
    GroupPolicy = 2,
    Service = 3,
    BiosUefi = 4,
    ThirdPartyOptimizer = 5,
    StartupProgram = 6,
    ScheduledTask = 7,
    NetworkConfiguration = 8,
    PowerSettings = 9,
    VisualEffects = 10,
    SystemFile = 11,
    EnvironmentVariable = 12,
    UserProfile = 13,
    KnowledgeBase = 14
}

/// <summary>
/// Category of performance tweaks
/// </summary>
public enum TweakCategory
{
    Unknown = 0,
    Performance = 1,
    Gaming = 2,
    Privacy = 3,
    Security = 4,
    Network = 5,
    Storage = 6,
    Memory = 7,
    Cpu = 8,
    Gpu = 9,
    Audio = 10,
    Visual = 11,
    Power = 12,
    Startup = 13,
    Services = 14,
    Updates = 15,
    Telemetry = 16,
    Bloatware = 17,
    Compatibility = 18,
    Accessibility = 19,
    SystemFile = 20,
    General = 21
}

/// <summary>
/// Status of system scanning operation
/// </summary>
public enum ScanStatus
{
    Initializing = 0,
    PreparingEnvironment = 1,
    ScanningRegistry = 2,
    ScanningServices = 3,
    ScanningGroupPolicies = 4,
    ScanningBiosSettings = 5,
    ScanningThirdPartyApps = 6,
    ScanningStartupPrograms = 7,
    ScanningScheduledTasks = 8,
    ScanningNetworkSettings = 9,
    ScanningPowerSettings = 10,
    ScanningVisualEffects = 11,
    AnalyzingTweaks = 12,
    GeneratingProfile = 13,
    Finalizing = 14,
    Completed = 15,
    Failed = 16,
    Cancelled = 17,
    Paused = 18
}

/// <summary>
/// Estimated performance impact of tweaks
/// </summary>
public enum PerformanceImpact
{
    Unknown = 0,
    Negligible = 1,
    Minor = 2,
    Moderate = 3,
    Major = 4,
    High = 5,
    Negative = 6,
    Variable = 7
}

/// <summary>
/// The type of a tweak, indicating the system area it affects.
/// </summary>
public enum TweakType
{
    Unknown = 0,
    Registry = 1,
    Service = 2,
    ScheduledTask = 3,
    PowerPlan = 4,
    NetworkAdapter = 5,
    VisualEffect = 6,
    SystemFile = 7,
    Driver = 8,
    Hardware = 9,
    Software = 10
}

/// <summary>
/// Third-party optimizer applications that can be detected
/// </summary>
public enum KnownOptimizer
{
    Unknown = 0,
    CCleaner = 1,
    AdvancedSystemCare = 2,
    SystemMechanic = 3,
    PCOptimizer = 4,
    TuneUpUtilities = 5,
    SystemOptimizer = 6,
    RegistryMechanic = 7,
    PCCleaner = 8,
    SystemBooster = 9,
    GameBooster = 10,
    DriverBooster = 11,
    WinOptimizer = 12,
    SystemTweaker = 13,
    PerformanceOptimizer = 14,
    RegistryOptimizer = 15,
    SpeedUpMyPC = 16,
    SystemSpeedup = 17,
    PCSpeedUp = 18,
    SystemCleaner = 19,
    RegistryCleaner = 20,
    // Gaming optimizers
    RazerCortex = 21,
    MSIAfterburner = 22,
    GameMode = 23,
    NvidiaGeForceExperience = 24,
    AMDRadeonSoftware = 25,
    // Custom/Manual tweaks
    ManualTweak = 100,
    CustomScript = 101,
    PowerUserTweak = 102
}

/// <summary>
/// Scan depth levels for different scanning operations
/// </summary>
public enum ScanDepth
{
    Surface = 0,    // Surface level scan (~1-2 minutes)
    Quick = 1,      // Essential tweaks only (~2-3 minutes)
    Standard = 2,   // Common tweaks and optimizations (~5-7 minutes)
    Deep = 3,       // Comprehensive scan (~10-15 minutes)
    Maximum = 4,    // Maximum depth scan (~15-20 minutes)
    Forensic = 5    // Every possible tweak (~20+ minutes)
}

/// <summary>
/// Detection confidence levels
/// </summary>
public enum DetectionConfidence
{
    Unknown = 0,
    VeryLow = 1,    // 0-20% confidence
    Low = 2,        // 20-40% confidence
    Medium = 3,     // 40-60% confidence
    High = 4,       // 60-80% confidence
    VeryHigh = 5,   // 80-95% confidence
    Certain = 6     // 95%+ confidence
}

/// <summary>
/// System areas that can be scanned
/// </summary>
[Flags]
public enum ScanArea
{
    None = 0,
    Registry = 1 << 0,
    Services = 1 << 1,
    GroupPolicy = 1 << 2,
    BiosUefi = 1 << 3,
    ThirdPartyApps = 1 << 4,
    StartupPrograms = 1 << 5,
    ScheduledTasks = 1 << 6,
    NetworkSettings = 1 << 7,
    PowerSettings = 1 << 8,
    VisualEffects = 1 << 9,
    SystemFiles = 1 << 10,
    EnvironmentVariables = 1 << 11,
    UserProfiles = 1 << 12,
    Drivers = 1 << 13,
    Hardware = 1 << 14,
    All = Registry | Services | GroupPolicy | BiosUefi | ThirdPartyApps | 
          StartupPrograms | ScheduledTasks | NetworkSettings | PowerSettings | 
          VisualEffects | SystemFiles | EnvironmentVariables | UserProfiles | 
          Drivers | Hardware
}

/// <summary>
/// Profile sharing permissions
/// </summary>
public enum SharingPermission
{
    Private = 0,
    FriendsOnly = 1,
    Community = 2,
    Public = 3
}

/// <summary>
/// Cloud profile status
/// </summary>
public enum CloudProfileStatus
{
    Draft = 0,
    Generated = 1,
    Loaded = 2,
    Pending = 3,
    Approved = 4,
    Rejected = 5,
    Suspended = 6,
    Archived = 7
}

/// <summary>
/// Security severity levels
/// </summary>
public enum SecuritySeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

/// <summary>
/// Service actions that can be performed
/// </summary>
public enum ServiceAction
{
    None = 0,
    Start = 1,
    Stop = 2,
    Restart = 3,
    Disable = 4,
    Enable = 5,
    SetAutomatic = 6,
    SetManual = 7,
    SetDisabled = 8
}

/// <summary>
/// Risk levels for security assessment
/// </summary>
public enum RiskLevel
{
    Unknown = 0,
    VeryLow = 1,
    Low = 2,
    Medium = 3,
    High = 4,
    VeryHigh = 5,
    Critical = 6
}

/// <summary>
/// Safety levels for tweak assessment
/// </summary>
public enum SafetyLevel
{
    Unknown = 0,
    VeryLow = 1,
    Low = 2,
    Medium = 3,
    High = 4,
    VeryHigh = 5
}

/// <summary>
/// Command types for tweak execution
/// </summary>
public enum CommandType
{
    Unknown = 0,
    Registry = 1,
    Service = 2,
    GroupPolicy = 3,
    PowerShell = 4,
    CommandLine = 5,
    WMI = 6,
    API = 7,
    FileSystem = 8,
    Environment = 9
}

/// <summary>
/// License tiers for feature access
/// </summary>
public enum LicenseTier
{
    Free = 0,
    Basic = 1,
    Pro = 2,
    Enterprise = 3
}