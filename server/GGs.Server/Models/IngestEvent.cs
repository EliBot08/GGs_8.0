using System;

namespace GGs.Server.Models;

public sealed class IngestEvent
{
    public int Id { get; set; }
    public string EventId { get; set; } = string.Empty; // unique
    public string Type { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public string? Client { get; set; }
    public DateTime? ProcessedUtc { get; set; }
}
