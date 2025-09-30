using System.ComponentModel.DataAnnotations;

namespace GGs.Server.Models;

public class DeviceRegistration
{
    public int Id { get; set; }
    public string TenantId { get; set; } = string.Empty;

    [Required]
    public string DeviceId { get; set; } = string.Empty;

    [Required]
    public string Thumbprint { get; set; } = string.Empty;

    public string? CommonName { get; set; }

    public DateTime RegisteredUtc { get; set; }

    public DateTime LastSeenUtc { get; set; }

    public DateTime? RevokedUtc { get; set; }

    public bool IsActive { get; set; }
}

