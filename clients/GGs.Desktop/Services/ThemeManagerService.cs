using System;
using System.Collections.Generic;
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
        System = 0,
        Midnight = 1,
        Dark = Midnight,
        Vapor = 2,
        Tactical = 3,
        Carbon = 4,
        Light = 5
    }

    public sealed class ThemeManagerService : INotifyPropertyChanged
    {
        private sealed record ThemeDefinition(string Id, string DisplayName, string ResourceUri, bool IsDark);

        private static ThemeManagerService? _instance;
        private readonly IReadOnlyDictionary<AppTheme, string> _themeDisplayNames;
        private readonly Dictionary<AppTheme, ThemeDefinition> _themeDefinitions = new()
        {
            { AppTheme.Midnight, new ThemeDefinition("midnight", "Midnight Cyan", "Themes/Palettes/Midnight.xaml", true) },
            { AppTheme.Vapor,    new ThemeDefinition("vapor",    "Vapor Violet", "Themes/Palettes/Vapor.xaml", true) },
            { AppTheme.Tactical, new ThemeDefinition("tactical", "Tactical Green", "Themes/Palettes/Tactical.xaml", true) },
            { AppTheme.Carbon,   new ThemeDefinition("carbon",   "Carbon Minimal", "Themes/Palettes/Carbon.xaml", true) },
            { AppTheme.Light,    new ThemeDefinition("light",    "Lumen Light", "Themes/Palettes/Light.xaml", false) }
        };

        private const string ThemeRegistryPath = @"Software\\GGs\\Desktop";
        private const string ThemeRegistryValue = "AppTheme";
        private const string WindowsThemeRegistryPath = @"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize";
        private const string WindowsThemeRegistryValue = "AppsUseLightTheme";

        private AppTheme _currentTheme = AppTheme.System;
        private bool _systemPrefersDark = true;
        private ResourceDictionary? _activeThemeDictionary;
        private Color? _accentPrimaryOverride;
        private Color? _accentSecondaryOverride;
        private double? _fontSizeOverridePoints;

        public static ThemeManagerService Instance => _instance ??= new ThemeManagerService();

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<AppTheme>? ThemeChanged;

        public AppTheme CurrentTheme
        {
            get => _currentTheme;
            set
            {
                if (_currentTheme != value)
                {
                    _currentTheme = value;
                    ApplyTheme();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsDarkMode));
                    ThemeChanged?.Invoke(this, GetActualTheme());
                }
            }
        }

        public bool IsDarkMode => ResolveDefinition(GetActualTheme()).IsDark;

        public IReadOnlyDictionary<AppTheme, string> ThemeDisplayNames => _themeDisplayNames;

        private ThemeManagerService()
        {
            _themeDisplayNames = new Dictionary<AppTheme, string>(_themeDefinitions.Count)
            {
                [AppTheme.Midnight] = _themeDefinitions[AppTheme.Midnight].DisplayName,
                [AppTheme.Vapor] = _themeDefinitions[AppTheme.Vapor].DisplayName,
                [AppTheme.Tactical] = _themeDefinitions[AppTheme.Tactical].DisplayName,
                [AppTheme.Carbon] = _themeDefinitions[AppTheme.Carbon].DisplayName,
                [AppTheme.Light] = _themeDefinitions[AppTheme.Light].DisplayName
            };
            DetectSystemTheme();
            SystemEvents.UserPreferenceChanged += OnSystemPreferenceChanged;
        }
private void OnSystemPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General && _currentTheme == AppTheme.System)
            {
                DetectSystemTheme();
                ApplyTheme();
            }
        }

        public AppTheme GetActualTheme()
        {
            return _currentTheme switch
            {
                AppTheme.System => _systemPrefersDark ? AppTheme.Midnight : AppTheme.Light,
                AppTheme.Dark => AppTheme.Midnight,
                _ => _currentTheme
            };
        }

        private ThemeDefinition ResolveDefinition(AppTheme theme)
        {
            if (_themeDefinitions.TryGetValue(theme, out var def))
            {
                return def;
            }

            return _themeDefinitions[AppTheme.Midnight];
        }

        private void DetectSystemTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(WindowsThemeRegistryPath);
                var value = key?.GetValue(WindowsThemeRegistryValue);
                _systemPrefersDark = value is int i ? i == 0 : true;
            }
            catch
            {
                _systemPrefersDark = true;
            }
        }

        public void ApplyTheme()
        {
            try
            {
                var app = Application.Current;
                if (app is null)
                {
                    return;
                }

                var definition = ResolveDefinition(GetActualTheme());
                var uri = new Uri($"pack://application:,,,/GGs.Desktop;component/{definition.ResourceUri}", UriKind.Absolute);
                var palette = (ResourceDictionary)Application.LoadComponent(uri);

                var dictionaries = app.Resources.MergedDictionaries;
                if (_activeThemeDictionary != null)
                {
                    dictionaries.Remove(_activeThemeDictionary);
                }

                dictionaries.Insert(0, palette);
                _activeThemeDictionary = palette;

                ApplyAccentOverrides(app.Resources);
                ApplyFontSize(app.Resources);

                ThemeChanged?.Invoke(this, definition.IsDark ? AppTheme.Midnight : AppTheme.Light);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThemeManager] Failed to apply theme: {ex.Message}");
            }
        }

        private void ApplyFontSize(ResourceDictionary resources)
        {
            if (_fontSizeOverridePoints.HasValue)
            {
                resources["GlobalFontSize"] = _fontSizeOverridePoints.Value;
            }
            else if (!resources.Contains("GlobalFontSize"))
            {
                resources["GlobalFontSize"] = 13d;
            }
        }

        private void ApplyAccentOverrides(ResourceDictionary resources)
        {
            try
            {
                if (_accentPrimaryOverride.HasValue)
                {
                    resources["ThemeAccentPrimary"] = new SolidColorBrush(_accentPrimaryOverride.Value);
                }

                if (_accentSecondaryOverride.HasValue)
                {
                    resources["ThemeAccentSecondary"] = new SolidColorBrush(_accentSecondaryOverride.Value);
                }

                if (_accentPrimaryOverride.HasValue || _accentSecondaryOverride.HasValue)
                {
                    var start = _accentPrimaryOverride ?? ((SolidColorBrush)resources["ThemeAccentPrimary"]).Color;
                    var end = _accentSecondaryOverride ?? ((SolidColorBrush)resources["ThemeAccentSecondary"]).Color;
                    var gradient = new LinearGradientBrush(start, end, 45);
                    gradient.Freeze();
                    resources["ThemeAccentGradient"] = gradient;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThemeManager] Failed to apply accent overrides: {ex.Message}");
            }
        }

        public void SetAccentOverrides(string? primaryHex, string? secondaryHex)
        {
            _accentPrimaryOverride = TryParseColor(primaryHex);
            _accentSecondaryOverride = TryParseColor(secondaryHex);
            ApplyTheme();
        }

        public void SetFontSize(double points)
        {
            _fontSizeOverridePoints = Math.Max(8, Math.Min(24, points));
            var app = Application.Current;
            if (app is not null)
            {
                ApplyFontSize(app.Resources);
            }
        }

        public void ApplyAppearance(Configuration.UserSettings settings)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(settings.Theme))
                {
                    var themeToken = settings.Theme.Trim();
                    if (Enum.TryParse<AppTheme>(themeToken, true, out var parsed))
                    {
                        CurrentTheme = parsed;
                    }
                    else
                    {
                        var lowered = themeToken.ToLowerInvariant();
                        CurrentTheme = lowered switch
                        {
                            "dark" => AppTheme.Midnight,
                            "midnight" => AppTheme.Midnight,
                            "vapor" => AppTheme.Vapor,
                            "tactical" => AppTheme.Tactical,
                            "carbon" => AppTheme.Carbon,
                            "light" => AppTheme.Light,
                            _ => AppTheme.System
                        };
                    }
                }
                else
                {
                    CurrentTheme = AppTheme.System;
                }

                _accentPrimaryOverride = TryParseColor(settings.AccentPrimaryHex);
                _accentSecondaryOverride = TryParseColor(settings.AccentSecondaryHex);
                _fontSizeOverridePoints = settings.FontSizePoints > 0 ? settings.FontSizePoints : (double?)null;

                ApplyTheme();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThemeManager] Failed to apply appearance: {ex.Message}");
            }
        }

        public void ToggleTheme()
        {
            var actual = GetActualTheme();
            if (ResolveDefinition(actual).IsDark)
            {
                CurrentTheme = AppTheme.Light;
            }
            else
            {
                CurrentTheme = AppTheme.Midnight;
            }
        }

        public void SaveThemePreference()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(ThemeRegistryPath);
                key?.SetValue(ThemeRegistryValue, CurrentTheme.ToString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThemeManager] Failed to save theme preference: {ex.Message}");
            }
        }

        public void LoadThemePreference()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(ThemeRegistryPath);
                var value = key?.GetValue(ThemeRegistryValue)?.ToString();
                if (!string.IsNullOrWhiteSpace(value) && Enum.TryParse<AppTheme>(value, out var theme))
                {
                    CurrentTheme = theme;
                }
                else
                {
                    CurrentTheme = AppTheme.System;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThemeManager] Failed to load theme preference: {ex.Message}");
            }
        }

        private static Color? TryParseColor(string? hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                return null;
            }

            try
            {
                return (Color)ColorConverter.ConvertFromString(hex);
            }
            catch
            {
                return null;
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}



