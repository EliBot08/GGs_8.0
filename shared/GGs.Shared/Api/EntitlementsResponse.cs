using GGs.Shared.Enums;

namespace GGs.Shared.Api;

public class EntitlementsResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Entitlements? Entitlements { get; set; }
    public LicenseTier LicenseTier { get; set; }
    public string[] Roles { get; set; } = Array.Empty<string>();
    public DateTime ExpiresAt { get; set; }
    public bool IsValid { get; set; } = true;
    public DateTime GeneratedUtc { get; set; } = DateTime.UtcNow;
}
