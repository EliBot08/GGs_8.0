namespace GGs.Server.Models;

public sealed class EliUsageCounter
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime DayUtc { get; set; } // truncated to date (UTC midnight)
    public int Count { get; set; }
}


