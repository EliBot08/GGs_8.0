using System.Windows;
using GGs.Shared.Enums;

namespace GGs.Desktop.Services;

public static class ThemeService
{
    // Deprecated: Tier-based themes removed in favor of dynamic ThemeManagerService.
    public static void Apply(LicenseTier tier)
    {
        try { ThemeManagerService.Instance.ApplyTheme(); } catch { }
    }

    public static void SetFontScale(double points)
    {
        Application.Current.Resources["GlobalFontSize"] = points;
    }
}
