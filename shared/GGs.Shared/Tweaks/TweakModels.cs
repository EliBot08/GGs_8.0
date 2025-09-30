using GGs.Shared.Enums;

namespace GGs.Shared.Tweaks;

public sealed class TweakDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }

    public CommandType CommandType { get; set; }

    // Registry
    public string? RegistryPath { get; set; }
    public string? RegistryValueName { get; set; }
    public string? RegistryValueType { get; set; } // String, DWord, QWord, MultiString, Binary
    public string? RegistryValueData { get; set; }

    // Service
    public string? ServiceName { get; set; }
    public ServiceAction? ServiceAction { get; set; }

    // Script
    public string? ScriptContent { get; set; } // PowerShell by default

    public SafetyLevel Safety { get; set; } = SafetyLevel.Medium;
    public RiskLevel Risk { get; set; } = RiskLevel.Medium;

    public bool RequiresAdmin { get; set; } = true;
    public bool AllowUndo { get; set; } = true;
    public string? UndoScriptContent { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedUtc { get; set; }
    public string? CreatedBy { get; set; }
}

public sealed class TweakApplicationLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TweakId { get; set; }
    public string? TweakName { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public DateTime AppliedUtc { get; set; } = DateTime.UtcNow;
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? BeforeState { get; set; }
    public string? AfterState { get; set; }
    public int ExecutionTimeMs { get; set; }

    // Enriched audit fields (explicit for ops and compliance)
    // Registry
    public string? RegistryPath { get; set; }
    public string? RegistryValueName { get; set; }
    public string? RegistryValueType { get; set; }
    public string? OriginalValue { get; set; }
    public string? NewValue { get; set; }

    // Service
    public string? ServiceName { get; set; }
    public string? ActionApplied { get; set; }

    // Script
    public string? ScriptApplied { get; set; }
    public string? UndoScript { get; set; }
}
