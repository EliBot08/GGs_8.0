using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using GGs.Desktop.Configuration;

namespace GGs.Desktop.Services
{
    public enum AppTheme
    {
        Light,
        Dark,
        System
    }

    public class ThemeManagerService : INotifyPropertyChanged
    {
        private static ThemeManagerService? _instance;
        private AppTheme _currentTheme = AppTheme.Dark;
        private bool _isSystemTheme = true;
        private System.Windows.Media.Color? _accentPrimaryOverride;
        private System.Windows.Media.Color? _accentSecondaryOverride;
        private double? _fontSizeOverridePoints;
        private readonly string _registryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private readonly string _registryValueName = "AppsUseLightTheme";

        public static ThemeManagerService Instance => _instance ??= new ThemeManagerService();

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<AppTheme>? ThemeChanged;

        // Theme Colors for Dark Mode
        public static class DarkTheme
        {
            public static System.Windows.Media.Color BackgroundPrimary = System.Windows.Media.Color.FromRgb(26, 22, 37);
            public static System.Windows.Media.Color BackgroundSecondary = System.Windows.Media.Color.FromRgb(42, 36, 56);
            public static System.Windows.Media.Color BackgroundTertiary = System.Windows.Media.Color.FromRgb(61, 54, 80);
            public static System.Windows.Media.Color AccentPrimary = System.Windows.Media.Color.FromRgb(139, 92, 246);
            public static System.Windows.Media.Color AccentSecondary = System.Windows.Media.Color.FromRgb(167, 139, 250);
            public static System.Windows.Media.Color TextPrimary = System.Windows.Media.Color.FromRgb(255, 255, 255);
            public static System.Windows.Media.Color TextSecondary = System.Windows.Media.Color.FromRgb(156, 163, 175);
            public static System.Windows.Media.Color BorderColor = System.Windows.Media.Color.FromRgb(61, 54, 80);
            public static System.Windows.Media.Color Success = System.Windows.Media.Color.FromRgb(34, 197, 94);
            public static System.Windows.Media.Color Warning = System.Windows.Media.Color.FromRgb(251, 146, 60);
            public static System.Windows.Media.Color Error = System.Windows.Media.Color.FromRgb(239, 68, 68);
        }

        // Theme Colors for Light Mode
        public static class LightTheme
        {
            public static System.Windows.Media.Color BackgroundPrimary = System.Windows.Media.Color.FromRgb(255, 255, 255);
            public static System.Windows.Media.Color BackgroundSecondary = System.Windows.Media.Color.FromRgb(249, 250, 251);
            public static System.Windows.Media.Color BackgroundTertiary = System.Windows.Media.Color.FromRgb(243, 244, 246);
            public static System.Windows.Media.Color AccentPrimary = System.Windows.Media.Color.FromRgb(109, 40, 217);
            public static System.Windows.Media.Color AccentSecondary = System.Windows.Media.Color.FromRgb(139, 92, 246);
            public static System.Windows.Media.Color TextPrimary = System.Windows.Media.Color.FromRgb(17, 24, 39);
            public static System.Windows.Media.Color TextSecondary = System.Windows.Media.Color.FromRgb(107, 114, 128);
            public static System.Windows.Media.Color BorderColor = System.Windows.Media.Color.FromRgb(229, 231, 235);
            public static System.Windows.Media.Color Success = System.Windows.Media.Color.FromRgb(16, 185, 129);
            public static System.Windows.Media.Color Warning = System.Windows.Media.Color.FromRgb(245, 158, 11);
            public static System.Windows.Media.Color Error = System.Windows.Media.Color.FromRgb(239, 68, 68);
        }

        public AppTheme CurrentTheme
        {
            get => _currentTheme;
            set
            {
                if (_currentTheme != value)
                {
                    _currentTheme = value;
                    _isSystemTheme = value == AppTheme.System;
                    ApplyTheme();
                    OnPropertyChanged();
                    ThemeChanged?.Invoke(this, GetActualTheme());
                }
            }
        }

        public bool IsDarkMode => GetActualTheme() == AppTheme.Dark;

        private ThemeManagerService()
        {
            // Initialize with system theme
            DetectSystemTheme();
            SystemEvents.UserPreferenceChanged += OnSystemThemeChanged;
        }

        private void OnSystemThemeChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General && _isSystemTheme)
            {
                DetectSystemTheme();
                ApplyTheme();
                ThemeChanged?.Invoke(this, GetActualTheme());
            }
        }

        private void DetectSystemTheme()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(_registryKeyPath))
                {
                    var value = key?.GetValue(_registryValueName);
                    if (value != null)
                    {
                        // 0 = Dark theme, 1 = Light theme
                        _currentTheme = (int)value == 0 ? AppTheme.Dark : AppTheme.Light;
                    }
                }
            }
            catch
            {
                // Default to dark theme if detection fails
                _currentTheme = AppTheme.Dark;
            }
        }

        public AppTheme GetActualTheme()
        {
            if (_isSystemTheme)
            {
                DetectSystemTheme();
            }
            return _currentTheme == AppTheme.System ? AppTheme.Dark : _currentTheme;
        }

        public void ApplyTheme()
        {
            var actualTheme = GetActualTheme();
            var app = System.Windows.Application.Current;
            if (app?.Resources == null) return;

            // High-contrast support: override theme resources with system colors
            if (SystemParameters.HighContrast)
            {
                ApplyHighContrastTheme(app.Resources);
                return;
            }

            // Clear existing theme resources
            var keysToRemove = new System.Collections.Generic.List<object>();
            foreach (var key in app.Resources.Keys)
            {
                if (key.ToString()?.StartsWith("Theme") == true)
                {
                    keysToRemove.Add(key);
                }
            }
            foreach (var key in keysToRemove)
            {
                app.Resources.Remove(key);
            }

            // Apply new theme colors
            if (actualTheme == AppTheme.Dark)
            {
                ApplyDarkTheme(app.Resources);
            }
            else
            {
                ApplyLightTheme(app.Resources);
            }

            // Apply overrides (accent + gradient) if any
            ApplyAccentOverrides(app.Resources);

            // Apply font scaling if provided
            if (_fontSizeOverridePoints.HasValue)
            {
                try { app.Resources["GlobalFontSize"] = _fontSizeOverridePoints.Value; } catch { }
            }
        }

        private void ApplyHighContrastTheme(ResourceDictionary resources)
        {
            resources["ThemeBackgroundPrimary"] = SystemColors.WindowBrush;
            resources["ThemeBackgroundSecondary"] = SystemColors.ControlBrush;
            resources["ThemeBackgroundTertiary"] = SystemColors.ControlLightBrush;
            resources["ThemeAccentPrimary"] = SystemColors.HighlightBrush;
            resources["ThemeAccentSecondary"] = SystemColors.HotTrackBrush;
            resources["ThemeTextPrimary"] = SystemColors.WindowTextBrush;
            resources["ThemeTextSecondary"] = SystemColors.GrayTextBrush;
            resources["ThemeBorder"] = SystemColors.WindowFrameBrush;
            resources["ThemeSuccess"] = SystemColors.HighlightBrush;
            resources["ThemeWarning"] = SystemColors.ControlDarkBrush;
            resources["ThemeError"] = SystemColors.ControlDarkDarkBrush;

            var accentGradient = new LinearGradientBrush();
            accentGradient.StartPoint = new Point(0, 0);
            accentGradient.EndPoint = new Point(1, 1);
            accentGradient.GradientStops.Add(new GradientStop(((SolidColorBrush)SystemColors.HighlightBrush).Color, 0));
            accentGradient.GradientStops.Add(new GradientStop(((SolidColorBrush)SystemColors.HotTrackBrush).Color, 1));
            resources["ThemeAccentGradient"] = accentGradient;
        }

        private void ApplyDarkTheme(ResourceDictionary resources)
        {
            resources["ThemeBackgroundPrimary"] = new SolidColorBrush(DarkTheme.BackgroundPrimary);
            resources["ThemeBackgroundSecondary"] = new SolidColorBrush(DarkTheme.BackgroundSecondary);
            resources["ThemeBackgroundTertiary"] = new SolidColorBrush(DarkTheme.BackgroundTertiary);
            resources["ThemeAccentPrimary"] = new SolidColorBrush(DarkTheme.AccentPrimary);
            resources["ThemeAccentSecondary"] = new SolidColorBrush(DarkTheme.AccentSecondary);
            resources["ThemeTextPrimary"] = new SolidColorBrush(DarkTheme.TextPrimary);
            resources["ThemeTextSecondary"] = new SolidColorBrush(DarkTheme.TextSecondary);
            resources["ThemeBorder"] = new SolidColorBrush(DarkTheme.BorderColor);
            resources["ThemeSuccess"] = new SolidColorBrush(DarkTheme.Success);
            resources["ThemeWarning"] = new SolidColorBrush(DarkTheme.Warning);
            resources["ThemeError"] = new SolidColorBrush(DarkTheme.Error);

            // Gradient brushes
            var accentGradient = new LinearGradientBrush();
            accentGradient.StartPoint = new Point(0, 0);
            accentGradient.EndPoint = new Point(1, 1);
            accentGradient.GradientStops.Add(new GradientStop(DarkTheme.AccentPrimary, 0));
            accentGradient.GradientStops.Add(new GradientStop(DarkTheme.AccentSecondary, 1));
            resources["ThemeAccentGradient"] = accentGradient;
        }

        private void ApplyLightTheme(ResourceDictionary resources)
        {
            resources["ThemeBackgroundPrimary"] = new SolidColorBrush(LightTheme.BackgroundPrimary);
            resources["ThemeBackgroundSecondary"] = new SolidColorBrush(LightTheme.BackgroundSecondary);
            resources["ThemeBackgroundTertiary"] = new SolidColorBrush(LightTheme.BackgroundTertiary);
            resources["ThemeAccentPrimary"] = new SolidColorBrush(LightTheme.AccentPrimary);
            resources["ThemeAccentSecondary"] = new SolidColorBrush(LightTheme.AccentSecondary);
            resources["ThemeTextPrimary"] = new SolidColorBrush(LightTheme.TextPrimary);
            resources["ThemeTextSecondary"] = new SolidColorBrush(LightTheme.TextSecondary);
            resources["ThemeBorder"] = new SolidColorBrush(LightTheme.BorderColor);
            resources["ThemeSuccess"] = new SolidColorBrush(LightTheme.Success);
            resources["ThemeWarning"] = new SolidColorBrush(LightTheme.Warning);
            resources["ThemeError"] = new SolidColorBrush(LightTheme.Error);

            // Gradient brushes
            var accentGradient = new LinearGradientBrush();
            accentGradient.StartPoint = new Point(0, 0);
            accentGradient.EndPoint = new Point(1, 1);
            accentGradient.GradientStops.Add(new GradientStop(LightTheme.AccentPrimary, 0));
            accentGradient.GradientStops.Add(new GradientStop(LightTheme.AccentSecondary, 1));
            resources["ThemeAccentGradient"] = accentGradient;
        }

        private void ApplyAccentOverrides(ResourceDictionary resources)
        {
            try
            {
                var primary = _accentPrimaryOverride;
                var secondary = _accentSecondaryOverride;
                if (primary.HasValue)
                {
                    resources["ThemeAccentPrimary"] = new SolidColorBrush(primary.Value);
                }
                if (secondary.HasValue)
                {
                    resources["ThemeAccentSecondary"] = new SolidColorBrush(secondary.Value);
                }
                if (primary.HasValue || secondary.HasValue)
                {
                    var g = new LinearGradientBrush();
                    g.StartPoint = new Point(0, 0);
                    g.EndPoint = new Point(1, 1);
                    g.GradientStops.Add(new GradientStop(primary ?? ((SolidColorBrush)resources["ThemeAccentPrimary"]).Color, 0));
                    g.GradientStops.Add(new GradientStop(secondary ?? ((SolidColorBrush)resources["ThemeAccentSecondary"]).Color, 1));
                    resources["ThemeAccentGradient"] = g;
                }
            }
            catch { }
        }

        public void SetAccentOverrides(string? primaryHex, string? secondaryHex)
        {
            try
            {
                _accentPrimaryOverride = TryParseColor(primaryHex);
                _accentSecondaryOverride = TryParseColor(secondaryHex);
                ApplyTheme();
            }
            catch { }
        }

        public void SetFontSize(double points)
        {
            _fontSizeOverridePoints = Math.Max(8, Math.Min(24, points));
            try
            {
                var app = System.Windows.Application.Current;
                if (app?.Resources != null)
                {
                    app.Resources["GlobalFontSize"] = _fontSizeOverridePoints.Value;
                }
            }
            catch { }
            OnPropertyChanged(nameof(IsDarkMode));
        }

        public void ApplyAppearance(UserSettings settings)
        {
            try
            {
                // Theme
                var themeStr = (settings.Theme ?? "system").Trim().ToLowerInvariant();
                if (themeStr == "dark") CurrentTheme = AppTheme.Dark;
                else if (themeStr == "light") CurrentTheme = AppTheme.Light;
                else CurrentTheme = AppTheme.System;

                // Accent overrides
                _accentPrimaryOverride = TryParseColor(settings.AccentPrimaryHex);
                _accentSecondaryOverride = TryParseColor(settings.AccentSecondaryHex);

                // Font size
                _fontSizeOverridePoints = settings.FontSizePoints > 0 ? settings.FontSizePoints : 13.0;

                ApplyTheme();
            }
            catch { }
        }

        private static System.Windows.Media.Color? TryParseColor(string? hex)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(hex)) return null;
                var c = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
                return c;
            }
            catch { return null; }
        }

        public void ToggleTheme()
        {
            var actualTheme = GetActualTheme();
            CurrentTheme = actualTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
        }

        public void SetSystemTheme()
        {
            CurrentTheme = AppTheme.System;
        }

        public void SaveThemePreference()
        {
            try
            {
                // Save to registry or local file
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\GGs\Desktop"))
                {
                    key?.SetValue("AppTheme", CurrentTheme.ToString());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save theme preference: {ex.Message}");
            }
        }

        public void LoadThemePreference()
        {
            try
            {
                // Load from registry or local file
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\GGs\Desktop"))
                {
                    var value = key?.GetValue("AppTheme")?.ToString();
                    if (!string.IsNullOrEmpty(value) && Enum.TryParse<AppTheme>(value, out var theme))
                    {
                        CurrentTheme = theme;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load theme preference: {ex.Message}");
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
