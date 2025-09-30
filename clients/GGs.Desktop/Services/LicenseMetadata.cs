using System;
using System.Collections.Generic;

namespace GGs.Desktop.Services;

public class LicenseMetadata
{
    public DateTime? LastValidationUtc { get; set; }
    public DateTime? LastOnlineCheckUtc { get; set; }
    public DateTime? NextRevalidationUtc { get; set; }
    public string? RevocationStatus { get; set; } = "Unknown";
    public string? DeviceId { get; set; }
    public string? KeyFingerprint { get; set; }
    public List<string>? ValidationLineage { get; set; }
}

