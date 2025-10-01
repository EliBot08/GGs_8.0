#nullable enable
using System.Windows.Media;

namespace GGs.ErrorLogViewer.Models
{
    /// <summary>
    /// Enhanced theme configuration for professional log viewer
    /// </summary>
    public enum AppTheme
    {
        Dark,
        Light,
        Solarized,
        HighContrast,
        System
    }

    public class ThemeColors
    {
        public required Color Background { get; set; }
        public required Color Surface { get; set; }
        public required Color Primary { get; set; }
        public required Color Secondary { get; set; }
        public required Color Accent { get; set; }
        public required Color Text { get; set; }
        public required Color TextSecondary { get; set; }
        public required Color Border { get; set; }
        public required Color Error { get; set; }
        public required Color Warning { get; set; }
        public required Color Success { get; set; }
        public required Color Info { get; set; }
        
        // Log level specific colors
        public required Color TraceBrush { get; set; }
        public required Color DebugBrush { get; set; }
        public required Color InformationBrush { get; set; }
        public required Color WarningBrush { get; set; }
        public required Color ErrorBrush { get; set; }
        public required Color CriticalBrush { get; set; }
        
        // Chart colors
        public required Color ChartPrimary { get; set; }
        public required Color ChartSecondary { get; set; }
        public required Color ChartTertiary { get; set; }
    }

    public static class ThemePresets
    {
        public static ThemeColors GetDarkTheme() => new()
        {
            Background = Color.FromRgb(0x1E, 0x1E, 0x1E),
            Surface = Color.FromRgb(0x2D, 0x2D, 0x30),
            Primary = Color.FromRgb(0x00, 0x7A, 0xCC),
            Secondary = Color.FromRgb(0x3E, 0x3E, 0x42),
            Accent = Color.FromRgb(0x00, 0x97, 0xFB),
            Text = Color.FromRgb(0xF5, 0xF5, 0xF5),
            TextSecondary = Color.FromRgb(0xA0, 0xA0, 0xA0),
            Border = Color.FromRgb(0x3E, 0x3E, 0x42),
            Error = Color.FromRgb(0xF4, 0x43, 0x36),
            Warning = Color.FromRgb(0xFF, 0x98, 0x00),
            Success = Color.FromRgb(0x4C, 0xAF, 0x50),
            Info = Color.FromRgb(0x21, 0x96, 0xF3),
            TraceBrush = Color.FromRgb(0x9C, 0xA3, 0xAF),
            DebugBrush = Color.FromRgb(0x60, 0xA5, 0xFA),
            InformationBrush = Color.FromRgb(0x34, 0xD3, 0x99),
            WarningBrush = Color.FromRgb(0xFB, 0xBF, 0x24),
            ErrorBrush = Color.FromRgb(0xF8, 0x71, 0x71),
            CriticalBrush = Color.FromRgb(0xEF, 0x44, 0x44),
            ChartPrimary = Color.FromRgb(0x00, 0xBF, 0xFF),
            ChartSecondary = Color.FromRgb(0xFF, 0x6B, 0x6B),
            ChartTertiary = Color.FromRgb(0x4E, 0xCB, 0x71)
        };

        public static ThemeColors GetLightTheme() => new()
        {
            Background = Color.FromRgb(0xF5, 0xF5, 0xF5),
            Surface = Color.FromRgb(0xFF, 0xFF, 0xFF),
            Primary = Color.FromRgb(0x00, 0x7A, 0xCC),
            Secondary = Color.FromRgb(0xE0, 0xE0, 0xE0),
            Accent = Color.FromRgb(0x00, 0x97, 0xFB),
            Text = Color.FromRgb(0x21, 0x21, 0x21),
            TextSecondary = Color.FromRgb(0x75, 0x75, 0x75),
            Border = Color.FromRgb(0xD0, 0xD0, 0xD0),
            Error = Color.FromRgb(0xD3, 0x2F, 0x2F),
            Warning = Color.FromRgb(0xF5, 0x9E, 0x0B),
            Success = Color.FromRgb(0x16, 0xA3, 0x4A),
            Info = Color.FromRgb(0x14, 0x78, 0xC2),
            TraceBrush = Color.FromRgb(0x6B, 0x72, 0x80),
            DebugBrush = Color.FromRgb(0x25, 0x63, 0xEB),
            InformationBrush = Color.FromRgb(0x05, 0x96, 0x69),
            WarningBrush = Color.FromRgb(0xF5, 0x9E, 0x0B),
            ErrorBrush = Color.FromRgb(0xDC, 0x26, 0x26),
            CriticalBrush = Color.FromRgb(0xB9, 0x1C, 0x1C),
            ChartPrimary = Color.FromRgb(0x34, 0x80, 0xF5),
            ChartSecondary = Color.FromRgb(0xEF, 0x44, 0x44),
            ChartTertiary = Color.FromRgb(0x22, 0xC5, 0x5E)
        };

        public static ThemeColors GetSolarizedTheme() => new()
        {
            Background = Color.FromRgb(0x00, 0x2B, 0x36),
            Surface = Color.FromRgb(0x07, 0x36, 0x42),
            Primary = Color.FromRgb(0x26, 0x8B, 0xD2),
            Secondary = Color.FromRgb(0x58, 0x6E, 0x75),
            Accent = Color.FromRgb(0x2A, 0xA1, 0x98),
            Text = Color.FromRgb(0x83, 0x94, 0x96),
            TextSecondary = Color.FromRgb(0x65, 0x7B, 0x83),
            Border = Color.FromRgb(0x58, 0x6E, 0x75),
            Error = Color.FromRgb(0xDC, 0x32, 0x2F),
            Warning = Color.FromRgb(0xCB, 0x4B, 0x16),
            Success = Color.FromRgb(0x85, 0x99, 0x00),
            Info = Color.FromRgb(0x26, 0x8B, 0xD2),
            TraceBrush = Color.FromRgb(0x65, 0x7B, 0x83),
            DebugBrush = Color.FromRgb(0x26, 0x8B, 0xD2),
            InformationBrush = Color.FromRgb(0x2A, 0xA1, 0x98),
            WarningBrush = Color.FromRgb(0xCB, 0x4B, 0x16),
            ErrorBrush = Color.FromRgb(0xDC, 0x32, 0x2F),
            CriticalBrush = Color.FromRgb(0xD3, 0x36, 0x82),
            ChartPrimary = Color.FromRgb(0x26, 0x8B, 0xD2),
            ChartSecondary = Color.FromRgb(0xDC, 0x32, 0x2F),
            ChartTertiary = Color.FromRgb(0x85, 0x99, 0x00)
        };

        public static ThemeColors GetHighContrastTheme() => new()
        {
            Background = Color.FromRgb(0x00, 0x00, 0x00),
            Surface = Color.FromRgb(0x1A, 0x1A, 0x1A),
            Primary = Color.FromRgb(0xFF, 0xFF, 0x00),
            Secondary = Color.FromRgb(0x40, 0x40, 0x40),
            Accent = Color.FromRgb(0x00, 0xFF, 0xFF),
            Text = Color.FromRgb(0xFF, 0xFF, 0xFF),
            TextSecondary = Color.FromRgb(0xCC, 0xCC, 0xCC),
            Border = Color.FromRgb(0xFF, 0xFF, 0xFF),
            Error = Color.FromRgb(0xFF, 0x00, 0x00),
            Warning = Color.FromRgb(0xFF, 0xA5, 0x00),
            Success = Color.FromRgb(0x00, 0xFF, 0x00),
            Info = Color.FromRgb(0x00, 0xCC, 0xFF),
            TraceBrush = Color.FromRgb(0xCC, 0xCC, 0xCC),
            DebugBrush = Color.FromRgb(0x00, 0xCC, 0xFF),
            InformationBrush = Color.FromRgb(0x00, 0xFF, 0x00),
            WarningBrush = Color.FromRgb(0xFF, 0xA5, 0x00),
            ErrorBrush = Color.FromRgb(0xFF, 0x00, 0x00),
            CriticalBrush = Color.FromRgb(0xFF, 0x00, 0xFF),
            ChartPrimary = Color.FromRgb(0xFF, 0xFF, 0x00),
            ChartSecondary = Color.FromRgb(0xFF, 0x00, 0x00),
            ChartTertiary = Color.FromRgb(0x00, 0xFF, 0x00)
        };
    }
}
