using System;

namespace GGs.Shared.Models;

public class AnalyticsSummary
{
    public int TotalTweaksApplied { get; set; }
    public int TotalUsers { get; set; }
    public int TotalDevices { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public double SuccessRate { get; set; }
    public int ActiveUsers { get; set; }
    
    // Additional properties for desktop compatibility
    public int logsSince { get; set; }
    public int licenses { get; set; }
    public int devicesConnected { get; set; }
}
