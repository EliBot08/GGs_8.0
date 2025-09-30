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

        // Enterprise Dark theme colors - Vibrant and Modern
        private static readonly Dictionary<Models.LogLevel, SolidColorBrush> DarkLogLevelBrushes = new()
        {
            { Models.LogLevel.Trace, new SolidColorBrush(Color.FromRgb(156, 163, 175)) },      // Cool Gray
            { Models.LogLevel.Debug, new SolidColorBrush(Color.FromRgb(96, 165, 250)) },       // Sky Blue
            { Models.LogLevel.Information, new SolidColorBrush(Color.FromRgb(52, 211, 153)) }, // Emerald
            { Models.LogLevel.Success, new SolidColorBrush(Color.FromRgb(16, 185, 129)) },     // Green
            { Models.LogLevel.Warning, new SolidColorBrush(Color.FromRgb(251, 191, 36)) },     // Amber
            { Models.LogLevel.Error, new SolidColorBrush(Color.FromRgb(248, 113, 113)) },      // Red
            { Models.LogLevel.Critical, new SolidColorBrush(Color.FromRgb(239, 68, 68)) }      // Crimson
        };

        // Enterprise Light theme colors - Clear and Professional
        private static readonly Dictionary<Models.LogLevel, SolidColorBrush> LightLogLevelBrushes = new()
        {
            { Models.LogLevel.Trace, new SolidColorBrush(Color.FromRgb(107, 114, 128)) },      // Gray
            { Models.LogLevel.Debug, new SolidColorBrush(Color.FromRgb(37, 99, 235)) },        // Blue
            { Models.LogLevel.Information, new SolidColorBrush(Color.FromRgb(5, 150, 105)) },  // Teal
            { Models.LogLevel.Success, new SolidColorBrush(Color.FromRgb(22, 163, 74)) },      // Green
            { Models.LogLevel.Warning, new SolidColorBrush(Color.FromRgb(245, 158, 11)) },     // Amber
            { Models.LogLevel.Error, new SolidColorBrush(Color.FromRgb(220, 38, 38)) },        // Red
            { Models.LogLevel.Critical, new SolidColorBrush(Color.FromRgb(185, 28, 28)) }      // Dark Red
        };

        // Source colors for different components - Enterprise Dark
        private static readonly Dictionary<string, SolidColorBrush> DarkSourceBrushes = new()
        {
            { "Desktop", new SolidColorBrush(Color.FromRgb(96, 165, 250)) },      // Blue
            { "Server", new SolidColorBrush(Color.FromRgb(251, 146, 60)) },       // Orange
            { "Launcher", new SolidColorBrush(Color.FromRgb(167, 139, 250)) },    // Purple
            { "Agent", new SolidColorBrush(Color.FromRgb(244, 114, 182)) },       // Pink
            { "LogViewer", new SolidColorBrush(Color.FromRgb(45, 212, 191)) },    // Teal
            { "Unknown", new SolidColorBrush(Color.FromRgb(156, 163, 175)) }      // Gray
        };

        // Source colors for different components - Enterprise Light
        private static readonly Dictionary<string, SolidColorBrush> LightSourceBrushes = new()
        {
            { "Desktop", new SolidColorBrush(Color.FromRgb(37, 99, 235)) },       // Blue
            { "Server", new SolidColorBrush(Color.FromRgb(234, 88, 12)) },        // Orange
            { "Launcher", new SolidColorBrush(Color.FromRgb(124, 58, 237)) },     // Purple
            { "Agent", new SolidColorBrush(Color.FromRgb(219, 39, 119)) },        // Pink
            { "LogViewer", new SolidColorBrush(Color.FromRgb(20, 184, 166)) },    // Teal
            { "Unknown", new SolidColorBrush(Color.FromRgb(107, 114, 128)) }      // Gray
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