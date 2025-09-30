using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModernWpf;

namespace GGs.ErrorLogViewer.Services
{
    public enum AppTheme
    {
        Dark,
        Light,
        System
    }

    public interface IThemeService : INotifyPropertyChanged
    {
        AppTheme CurrentTheme { get; set; }
        bool IsDarkMode { get; }
        void ToggleTheme();
        void SetTheme(AppTheme theme);
        SolidColorBrush GetLogLevelBrush(Models.LogLevel level);
        SolidColorBrush GetSourceBrush(string source);
    }

    public class ThemeService : IThemeService, INotifyPropertyChanged
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ThemeService> _logger;
        private AppTheme _currentTheme;

        public event PropertyChangedEventHandler? PropertyChanged;

        public AppTheme CurrentTheme
        {
            get => _currentTheme;
            set
            {
                if (_currentTheme != value)
                {
                    _currentTheme = value;
                    ApplyTheme();
                    OnPropertyChanged(nameof(CurrentTheme));
                    OnPropertyChanged(nameof(IsDarkMode));
                }
            }
        }

        public bool IsDarkMode => GetEffectiveTheme() == ElementTheme.Dark;

        // Dark theme colors
        private static readonly Dictionary<Models.LogLevel, SolidColorBrush> DarkLogLevelBrushes = new()
        {
            { Models.LogLevel.Trace, new SolidColorBrush(Color.FromRgb(128, 128, 128)) },      // Gray
            { Models.LogLevel.Debug, new SolidColorBrush(Color.FromRgb(173, 216, 230)) },     // Light Blue
            { Models.LogLevel.Information, new SolidColorBrush(Color.FromRgb(144, 238, 144)) }, // Light Green
            { Models.LogLevel.Success, new SolidColorBrush(Color.FromRgb(50, 205, 50)) },     // Lime Green
            { Models.LogLevel.Warning, new SolidColorBrush(Color.FromRgb(255, 215, 0)) },     // Gold
            { Models.LogLevel.Error, new SolidColorBrush(Color.FromRgb(255, 99, 71)) },       // Tomato
            { Models.LogLevel.Critical, new SolidColorBrush(Color.FromRgb(220, 20, 60)) }     // Crimson
        };

        // Light theme colors
        private static readonly Dictionary<Models.LogLevel, SolidColorBrush> LightLogLevelBrushes = new()
        {
            { Models.LogLevel.Trace, new SolidColorBrush(Color.FromRgb(105, 105, 105)) },     // Dim Gray
            { Models.LogLevel.Debug, new SolidColorBrush(Color.FromRgb(70, 130, 180)) },      // Steel Blue
            { Models.LogLevel.Information, new SolidColorBrush(Color.FromRgb(34, 139, 34)) }, // Forest Green
            { Models.LogLevel.Success, new SolidColorBrush(Color.FromRgb(0, 128, 0)) },       // Green
            { Models.LogLevel.Warning, new SolidColorBrush(Color.FromRgb(255, 140, 0)) },     // Dark Orange
            { Models.LogLevel.Error, new SolidColorBrush(Color.FromRgb(178, 34, 34)) },       // Fire Brick
            { Models.LogLevel.Critical, new SolidColorBrush(Color.FromRgb(139, 0, 0)) }       // Dark Red
        };

        // Source colors for different components
        private static readonly Dictionary<string, SolidColorBrush> DarkSourceBrushes = new()
        {
            { "Desktop", new SolidColorBrush(Color.FromRgb(100, 149, 237)) },    // Cornflower Blue
            { "Server", new SolidColorBrush(Color.FromRgb(255, 165, 0)) },       // Orange
            { "Launcher", new SolidColorBrush(Color.FromRgb(147, 112, 219)) },   // Medium Slate Blue
            { "Agent", new SolidColorBrush(Color.FromRgb(255, 20, 147)) },       // Deep Pink
            { "LogViewer", new SolidColorBrush(Color.FromRgb(64, 224, 208)) },   // Turquoise
            { "Unknown", new SolidColorBrush(Color.FromRgb(169, 169, 169)) }     // Dark Gray
        };

        private static readonly Dictionary<string, SolidColorBrush> LightSourceBrushes = new()
        {
            { "Desktop", new SolidColorBrush(Color.FromRgb(65, 105, 225)) },     // Royal Blue
            { "Server", new SolidColorBrush(Color.FromRgb(255, 140, 0)) },       // Dark Orange
            { "Launcher", new SolidColorBrush(Color.FromRgb(123, 104, 238)) },   // Medium Slate Blue
            { "Agent", new SolidColorBrush(Color.FromRgb(199, 21, 133)) },       // Medium Violet Red
            { "LogViewer", new SolidColorBrush(Color.FromRgb(72, 209, 204)) },   // Medium Turquoise
            { "Unknown", new SolidColorBrush(Color.FromRgb(105, 105, 105)) }     // Dim Gray
        };

        public ThemeService(IConfiguration configuration, ILogger<ThemeService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            // Load theme from configuration
            var themeConfig = _configuration["UI:DefaultTheme"] ?? "Dark";
            if (Enum.TryParse<AppTheme>(themeConfig, true, out var theme))
            {
                _currentTheme = theme;
            }
            else
            {
                _currentTheme = AppTheme.Dark; // Default to dark mode
            }

            ApplyTheme();
        }

        public void ToggleTheme()
        {
            CurrentTheme = CurrentTheme switch
            {
                AppTheme.Dark => AppTheme.Light,
                AppTheme.Light => AppTheme.Dark,
                AppTheme.System => AppTheme.Dark, // Default to dark when toggling from system
                _ => AppTheme.Dark
            };
        }

        public void SetTheme(AppTheme theme)
        {
            CurrentTheme = theme;
        }

        private void ApplyTheme()
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var effectiveTheme = GetEffectiveTheme();
                    ThemeManager.Current.ApplicationTheme = effectiveTheme switch
                    {
                        ElementTheme.Dark => ApplicationTheme.Dark,
                        ElementTheme.Light => ApplicationTheme.Light,
                        _ => null
                    };
                    
                    _logger.LogInformation("Applied theme: {Theme} (effective: {EffectiveTheme})", 
                        CurrentTheme, effectiveTheme);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply theme: {Theme}", CurrentTheme);
            }
        }

        private ElementTheme GetEffectiveTheme()
        {
            return CurrentTheme switch
            {
                AppTheme.Dark => ElementTheme.Dark,
                AppTheme.Light => ElementTheme.Light,
                AppTheme.System => GetSystemTheme(),
                _ => ElementTheme.Dark
            };
        }

        private static ElementTheme GetSystemTheme()
        {
            try
            {
                // Check Windows registry for system theme preference
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                
                if (key?.GetValue("AppsUseLightTheme") is int value)
                {
                    return value == 0 ? ElementTheme.Dark : ElementTheme.Light;
                }
            }
            catch
            {
                // Ignore errors and fall back to dark theme
            }

            return ElementTheme.Dark; // Default to dark if unable to determine
        }

        public SolidColorBrush GetLogLevelBrush(Models.LogLevel level)
        {
            var brushes = IsDarkMode ? DarkLogLevelBrushes : LightLogLevelBrushes;
            return brushes.TryGetValue(level, out var brush) ? brush : brushes[Models.LogLevel.Information];
        }

        public SolidColorBrush GetSourceBrush(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                source = "Unknown";

            var brushes = IsDarkMode ? DarkSourceBrushes : LightSourceBrushes;
            
            // Try exact match first
            if (brushes.TryGetValue(source, out var brush))
                return brush;

            // Try partial matches
            foreach (var kvp in brushes)
            {
                if (source.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }

            return brushes["Unknown"];
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}