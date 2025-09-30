using GGs.Shared.Tweaks;
using System;

namespace GGs.Desktop.Extensions
{
    public static partial class TweakApplicationLogExtensions
    {
        // Extension methods for TweakApplicationLog to provide computed UI properties
        public static string GetStatusIcon(this TweakApplicationLog log)
        {
            return log.Success ? "✓" : "✗";
        }

        public static string GetRiskLevelColor(this TweakApplicationLog log)
        {
            // Default risk level color based on success
            return log.Success ? "#4CAF50" : "#F44336";
        }

        public static string GetRiskLevel(this TweakApplicationLog log)
        {
            return log.Success ? "Low" : "High";
        }

        public static bool GetHasError(this TweakApplicationLog log)
        {
            return !string.IsNullOrWhiteSpace(log.Error);
        }

        public static string GetDescription(this TweakApplicationLog log)
        {
            return log.TweakName ?? "No description available";
        }

        public static string GetCorrelationId(this TweakApplicationLog log)
        {
            return Guid.NewGuid().ToString();
        }
    }
}
