using GGs.Shared.Licensing;

namespace GGs.Server.Models;

public sealed class LicenseRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = string.Empty;
    public string LicenseId { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty; // Identity user id assigned to this license
    public string Tier { get; set; } = string.Empty;
    public DateTime IssuedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresUtc { get; set; }
    public bool IsAdminKey { get; set; }
    public string? DeviceBindingId { get; set; }
    public bool AllowOfflineValidation { get; set; } = true;

    // Lifecycle / enforcement
    public string Status { get; set; } = "Active"; // Active|Suspended|Revoked
    public bool DeveloperMode { get; set; }
    public int MaxDevices { get; set; } = 1;
    public int UsageCount { get; set; }
    public string AssignedDevicesJson { get; set; } = "[]";
    public string? Notes { get; set; }

    // Persist the signed license token for re-issue/download
    public string SignedLicenseJson { get; set; } = string.Empty;
}
