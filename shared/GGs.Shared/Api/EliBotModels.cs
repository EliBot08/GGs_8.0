using System;
using System.Collections.Generic;

namespace GGs.Shared.Api;

public sealed class EliBotRequest
{
    public required string Question { get; init; }
    public List<EliBotMessage> ConversationHistory { get; init; } = new();
    public string? UserId { get; init; }
    public bool IncludeSystemContext { get; init; } = true;
    public string? SystemInfo { get; init; }
    public List<string> EnabledTweaks { get; init; } = new();
}

public sealed class EliBotResponse
{
    public required string Answer { get; init; }
    public string? ModelUsed { get; init; }
    public int TokensUsed { get; init; }
    public decimal Cost { get; init; }
    public bool IsRateLimited { get; init; }
    public bool IsFromCache { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public List<string> SuggestedActions { get; init; } = new();
    public List<RecommendedTweak> RecommendedTweaks { get; init; } = new();
}

public sealed class EliBotMessage
{
    public required string Role { get; init; } // "user" or "assistant"
    public required string Content { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

public sealed class EliBotUsage
{
    public int QuestionsUsedToday { get; init; }
    public int DailyLimit { get; init; }
    public bool CanAskQuestion { get; init; }
    public DateTime ResetTime { get; init; }
    public decimal TotalCostToday { get; init; }
    public int TokensUsedToday { get; init; }
}

public sealed class RecommendedTweak
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public string? Reason { get; init; }
    public bool IsEnabled { get; init; }
}

public sealed class EliBotAnalytics
{
    public int TotalQuestions { get; init; }
    public int UniqueUsers { get; init; }
    public decimal TotalCost { get; init; }
    public int TotalTokens { get; init; }
    public Dictionary<string, int> TopQuestionCategories { get; init; } = new();
    public Dictionary<string, int> ModelUsageStats { get; init; } = new();
    public double AverageResponseTime { get; init; }
    public double UserSatisfactionScore { get; init; }
}
