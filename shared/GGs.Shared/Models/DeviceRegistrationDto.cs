using System;

namespace GGs.Shared.Models;

public class DeviceRegistrationDto
{
    public string Id { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSeenAt { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
