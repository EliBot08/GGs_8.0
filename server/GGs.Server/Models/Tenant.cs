namespace GGs.Server.Models;

public sealed class Tenant
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public interface ITenantScoped
{
    string TenantId { get; set; }
}


