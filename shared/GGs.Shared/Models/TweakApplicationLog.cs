using System;

namespace GGs.Shared.Models;

public class TweakApplicationLog
{
    public string Id { get; set; } = string.Empty;
    public string TweakId { get; set; } = string.Empty;
    public string TweakName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime AppliedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusIcon { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public string RiskLevelColor { get; set; } = string.Empty;
    public bool HasError { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string ProfileId { get; set; } = string.Empty;
}
