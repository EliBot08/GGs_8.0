using GGs.Shared.Enums;
using GGs.Shared.Licensing;
using GGs.Shared.Tweaks;

namespace GGs.Shared.Api;

public class CreateTweakRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? Category { get; init; }
    public CommandType CommandType { get; init; }
    public string? RegistryPath { get; init; }
    public string? RegistryValueName { get; init; }
    public string? RegistryValueType { get; init; }
    public string? RegistryValueData { get; init; }
    public string? ServiceName { get; init; }
    public ServiceAction? ServiceAction { get; init; }
    public string? ScriptContent { get; init; }
    public SafetyLevel Safety { get; init; } = SafetyLevel.Medium;
    public RiskLevel Risk { get; init; } = RiskLevel.Medium;
    public bool RequiresAdmin { get; init; } = true;
    public bool AllowUndo { get; init; } = true;
    public string? UndoScriptContent { get; init; }
}

public class UpdateTweakRequest : CreateTweakRequest
{
    public required Guid Id { get; init; }
}

public sealed class TweakResponse
{
    public required TweakDefinition Tweak { get; init; }
}

public sealed class LicenseIssueRequest
{
    public required string UserId { get; init; }
    public required LicenseTier Tier { get; init; }
    public DateTime? ExpiresUtc { get; init; }
    public bool IsAdminKey { get; init; }
    public string? DeviceBindingId { get; init; }
    public bool AllowOfflineValidation { get; init; } = true;
    public string? Notes { get; init; }
}

public sealed class LicenseIssueResponse
{
    public required SignedLicense License { get; init; }
}

public sealed class LicenseValidateRequest
{
    public required SignedLicense License { get; init; }
    public string? CurrentDeviceBinding { get; init; }
}

public sealed class LicenseValidateResponse
{
    public bool IsValid { get; init; }
    public string? Message { get; init; }
}
