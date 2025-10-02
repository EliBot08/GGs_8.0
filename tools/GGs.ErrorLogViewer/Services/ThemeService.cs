#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModernWpf;

namespace GGs.ErrorLogViewer.Services
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

    public interface IThemeService : INotifyPropertyChanged
    {
        AppTheme CurrentTheme { get; set; }
        bool IsDarkMode { get; }
        void ToggleTheme();
        void SetTheme(AppTheme theme);
        SolidColorBrush GetLogLevelBrush(Models.LogLevel level);
        SolidColorBrush GetSourceBrush(string source);
        IReadOnlyDictionary<AppTheme, string> ThemeDisplayNames { get; }
    }

    public sealed class ThemeService : IThemeService
    {
        private sealed record ThemeDefinition(string Id, string DisplayName, string ResourceUri, bool IsDark, Dictionary<Models.LogLevel, SolidColorBrush> LogLevelBrushes, Color[] SourcePalette);

        private readonly IConfiguration _configuration;
        private readonly ILogger<ThemeService> _logger;
        private readonly Dictionary<AppTheme, ThemeDefinition> _definitions;
        private readonly IReadOnlyDictionary<AppTheme, string> _displayNames;
        private readonly Dictionary<AppTheme, Dictionary<string, SolidColorBrush>> _sourceBrushCache = new();
        private ResourceDictionary? _activeThemeDictionary;
        private AppTheme _currentTheme = AppTheme.System;
        private bool _systemPrefersDark = true;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ThemeService(IConfiguration configuration, ILogger<ThemeService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            _definitions = BuildDefinitions();
            _displayNames = new Dictionary<AppTheme, string>
            {
                [AppTheme.Midnight] = _definitions[AppTheme.Midnight].DisplayName,
                [AppTheme.Vapor] = _definitions[AppTheme.Vapor].DisplayName,
                [AppTheme.Tactical] = _definitions[AppTheme.Tactical].DisplayName,
                [AppTheme.Carbon] = _definitions[AppTheme.Carbon].DisplayName,
                [AppTheme.Light] = _definitions[AppTheme.Light].DisplayName
            };

            DetectSystemTheme();
            CurrentTheme = ResolveInitialTheme(configuration["UI:DefaultTheme"]);
        }

        public IReadOnlyDictionary<AppTheme, string> ThemeDisplayNames => _displayNames;

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
                }
            }
        }

        public bool IsDarkMode => ResolveDefinition(GetActualTheme()).IsDark;

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

        public void SetTheme(AppTheme theme)
        {
            CurrentTheme = theme;
        }

        public SolidColorBrush GetLogLevelBrush(Models.LogLevel level)
        {
            var definition = ResolveDefinition(GetActualTheme());
            return definition.LogLevelBrushes.TryGetValue(level, out var brush)
                ? brush
                : definition.LogLevelBrushes[Models.LogLevel.Information];
        }

        public SolidColorBrush GetSourceBrush(string source)
        {
            var key = string.IsNullOrWhiteSpace(source) ? "default" : source.Trim();
            var theme = GetActualTheme();

            if (!_sourceBrushCache.TryGetValue(theme, out var cache))
            {
                cache = new Dictionary<string, SolidColorBrush>(StringComparer.OrdinalIgnoreCase);
                _sourceBrushCache[theme] = cache;
            }

            if (!cache.TryGetValue(key, out var brush))
            {
                var palette = ResolveDefinition(theme).SourcePalette;
                var index = Math.Abs(key.GetHashCode());
                var color = palette[index % palette.Length];
                brush = new SolidColorBrush(color);
                brush.Freeze();
                cache[key] = brush;
            }

            return brush;
        }

        private AppTheme ResolveInitialTheme(string? themeToken)
        {
            if (string.IsNullOrWhiteSpace(themeToken))
            {
                return AppTheme.System;
            }

            var lowered = themeToken.Trim();
            if (Enum.TryParse<AppTheme>(lowered, true, out var parsed))
            {
                return parsed;
            }

            lowered = lowered.ToLowerInvariant();
            return lowered switch
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

        private void DetectSystemTheme()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize");
                var value = key?.GetValue("AppsUseLightTheme");
                _systemPrefersDark = value is int v ? v == 0 : true;
            }
            catch
            {
                _systemPrefersDark = true;
            }
        }

        private void ApplyTheme()
        {
            try
            {
                var app = Application.Current;
                if (app is null)
                {
                    return;
                }

                var definition = ResolveDefinition(GetActualTheme());
                var uri = new Uri($"pack://application:,,,/GGs.ErrorLogViewer;component/{definition.ResourceUri}", UriKind.Absolute);
                var palette = (ResourceDictionary)Application.LoadComponent(uri);

                var dictionaries = app.Resources.MergedDictionaries;
                if (_activeThemeDictionary != null)
                {
                    dictionaries.Remove(_activeThemeDictionary);
                }

                dictionaries.Insert(0, palette);
                _activeThemeDictionary = palette;

                ThemeManager.Current.ApplicationTheme = definition.IsDark ? ApplicationTheme.Dark : ApplicationTheme.Light;
                OnPropertyChanged(nameof(IsDarkMode));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply theme {Theme}", CurrentTheme);
            }
        }

        private AppTheme GetActualTheme()
        {
            return CurrentTheme switch
            {
                AppTheme.System => _systemPrefersDark ? AppTheme.Midnight : AppTheme.Light,
                AppTheme.Dark => AppTheme.Midnight,
                _ => CurrentTheme
            };
        }

        private ThemeDefinition ResolveDefinition(AppTheme theme)
        {
            if (_definitions.TryGetValue(theme, out var definition))
            {
                return definition;
            }

            return _definitions[AppTheme.Midnight];
        }

        private static Dictionary<AppTheme, ThemeDefinition> BuildDefinitions()
        {
            static Dictionary<Models.LogLevel, SolidColorBrush> CreateLogLevelPalette((Color trace, Color debug, Color info, Color success, Color warning, Color error, Color critical) colors)
            {
                return new Dictionary<Models.LogLevel, SolidColorBrush>
                {
                    { Models.LogLevel.Trace, Create(colors.trace) },
                    { Models.LogLevel.Debug, Create(colors.debug) },
                    { Models.LogLevel.Information, Create(colors.info) },
                    { Models.LogLevel.Success, Create(colors.success) },
                    { Models.LogLevel.Warning, Create(colors.warning) },
                    { Models.LogLevel.Error, Create(colors.error) },
                    { Models.LogLevel.Critical, Create(colors.critical) }
                };

                static SolidColorBrush Create(Color color)
                {
                    var brush = new SolidColorBrush(color);
                    brush.Freeze();
                    return brush;
                }
            }

            static Color[] Palette(params string[] hex)
            {
                var array = new Color[hex.Length];
                for (var i = 0; i < hex.Length; i++)
                {
                    array[i] = (Color)ColorConverter.ConvertFromString(hex[i])!;
                }
                return array;
            }

            return new Dictionary<AppTheme, ThemeDefinition>
            {
                {
                    AppTheme.Midnight,
                    new ThemeDefinition(
                        "midnight",
                        "Midnight Cyan",
                        "Views/Themes/Palettes/Midnight.xaml",
                        true,
                        CreateLogLevelPalette((
                            Color.FromRgb(156, 163, 175),
                            Color.FromRgb(96, 165, 250),
                            Color.FromRgb(52, 211, 153),
                            Color.FromRgb(16, 185, 129),
                            Color.FromRgb(251, 191, 36),
                            Color.FromRgb(248, 113, 113),
                            Color.FromRgb(239, 68, 68))),
                        Palette("#FF00E5FF", "#FF00B8D4", "#FF0091EA", "#FF37FFC4", "#FF00A9E0", "#FFB4F5FF"))
                },
                {
                    AppTheme.Vapor,
                    new ThemeDefinition(
                        "vapor",
                        "Vapor Violet",
                        "Views/Themes/Palettes/Vapor.xaml",
                        true,
                        CreateLogLevelPalette((
                            Color.FromRgb(164, 164, 196),
                            Color.FromRgb(167, 139, 250),
                            Color.FromRgb(129, 140, 248),
                            Color.FromRgb(236, 72, 153),
                            Color.FromRgb(250, 204, 21),
                            Color.FromRgb(248, 113, 166),
                            Color.FromRgb(236, 72, 153))),
                        Palette("#FF9B5CFF", "#FF7F3BFF", "#FFB45CFF", "#FF5C2FFF", "#FFE15CFF", "#FF7C5CFF"))
                },
                {
                    AppTheme.Tactical,
                    new ThemeDefinition(
                        "tactical",
                        "Tactical Green",
                        "Views/Themes/Palettes/Tactical.xaml",
                        true,
                        CreateLogLevelPalette((
                            Color.FromRgb(120, 146, 135),
                            Color.FromRgb(94, 234, 212),
                            Color.FromRgb(16, 185, 129),
                            Color.FromRgb(34, 197, 94),
                            Color.FromRgb(250, 204, 21),
                            Color.FromRgb(249, 115, 22),
                            Color.FromRgb(248, 72, 69))),
                        Palette("#FF00FF9C", "#FF00D37F", "#FF37FFA3", "#FF2FD4FF", "#FFAFE9D8", "#FF39FFB6"))
                },
                {
                    AppTheme.Carbon,
                    new ThemeDefinition(
                        "carbon",
                        "Carbon Minimal",
                        "Views/Themes/Palettes/Carbon.xaml",
                        true,
                        CreateLogLevelPalette((
                            Color.FromRgb(126, 136, 150),
                            Color.FromRgb(66, 153, 225),
                            Color.FromRgb(59, 130, 246),
                            Color.FromRgb(241, 90, 34),
                            Color.FromRgb(251, 191, 36),
                            Color.FromRgb(248, 113, 113),
                            Color.FromRgb(239, 68, 68))),
                        Palette("#FFFF6B35", "#FFFF9240", "#FFF55D3E", "#FFC94F30", "#FFFFA552", "#FFFF7B54"))
                },
                {
                    AppTheme.Light,
                    new ThemeDefinition(
                        "light",
                        "Lumen Light",
                        "Views/Themes/Palettes/Light.xaml",
                        false,
                        CreateLogLevelPalette((
                            Color.FromRgb(107, 114, 128),
                            Color.FromRgb(37, 99, 235),
                            Color.FromRgb(5, 150, 105),
                            Color.FromRgb(22, 163, 74),
                            Color.FromRgb(245, 158, 11),
                            Color.FromRgb(220, 38, 38),
                            Color.FromRgb(185, 28, 28))),
                        Palette("#FF2563EB", "#FF4C6EF5", "#FF1D4ED8", "#FF3B82F6", "#FF10B981", "#FFF68B24"))
                }
            };
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}





