using GGs.Shared.Tweaks;
using GGs.Shared.Models;
using System;

namespace GGs.Desktop.Extensions
{
    public static partial class TweakStatisticExtensions
    {
        public static bool GetSuccess(this TweakStatistic statistic)
        {
            return statistic.UsageCount > 0;
        }
    }

    // TweakStatistic is now defined in GGs.Shared.Models

    public static class DoubleAnimationExtensions
    {
        public static double GetCurrentProgress(this System.Windows.Media.Animation.DoubleAnimation animation)
        {
            return animation.To ?? 0;
        }
    }
}
