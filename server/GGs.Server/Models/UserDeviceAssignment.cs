namespace GGs.Server.Models;

public sealed class UserDeviceAssignment
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public DateTime AssignedUtc { get; set; } = DateTime.UtcNow;
}


