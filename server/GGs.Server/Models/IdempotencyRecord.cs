namespace GGs.Server.Models;

public sealed class IdempotencyRecord
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public string ResponseJson { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
