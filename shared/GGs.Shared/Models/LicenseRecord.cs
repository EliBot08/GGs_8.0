using System;
using GGs.Shared.Enums;

namespace GGs.Shared.Models;

public class LicenseRecord
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public LicenseTier Tier { get; set; }
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public string IssuedBy { get; set; } = string.Empty;
}
