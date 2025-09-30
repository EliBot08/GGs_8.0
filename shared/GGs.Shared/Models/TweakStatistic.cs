using System;

namespace GGs.Shared.Models;

public class TweakStatistic
{
    public string Name { get; set; } = string.Empty;
    public int UsageCount { get; set; }
    public double SuccessRate { get; set; }
    public DateTime LastUsed { get; set; } = DateTime.UtcNow;
    public string Category { get; set; } = string.Empty;
}
