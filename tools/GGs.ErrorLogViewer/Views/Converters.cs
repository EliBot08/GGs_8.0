using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ModernWpf.Controls;
using GGs.ErrorLogViewer.Models;

namespace GGs.ErrorLogViewer.Views
{
    public class LogLevelToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LogLevel level)
            {
                return level switch
                {
                    LogLevel.Trace => Symbol.More,
                    LogLevel.Debug => Symbol.Setting,
                    LogLevel.Information => Symbol.Message,
                    LogLevel.Success => Symbol.Accept,
                    LogLevel.Warning => Symbol.Important,
                    LogLevel.Error => Symbol.Cancel,
                    LogLevel.Critical => Symbol.DisconnectDrive,
                    _ => Symbol.Help
                };
            }
            return Symbol.Help;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class LogLevelToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LogLevel level)
            {
                return level switch
                {
                    LogLevel.Trace => new SolidColorBrush(Colors.Gray),
                    LogLevel.Debug => new SolidColorBrush(Colors.LightBlue),
                    LogLevel.Information => new SolidColorBrush(Colors.LightGreen),
                    LogLevel.Success => new SolidColorBrush(Colors.Green),
                    LogLevel.Warning => new SolidColorBrush(Colors.Orange),
                    LogLevel.Error => new SolidColorBrush(Colors.Red),
                    LogLevel.Critical => new SolidColorBrush(Colors.DarkRed),
                    _ => new SolidColorBrush(Colors.Black)
                };
            }
            return new SolidColorBrush(Colors.Black);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class SourceToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string source)
            {
                return source.ToLowerInvariant() switch
                {
                    "desktop" => new SolidColorBrush(Colors.CornflowerBlue),
                    "server" => new SolidColorBrush(Colors.Orange),
                    "launcher" => new SolidColorBrush(Colors.MediumSlateBlue),
                    "agent" => new SolidColorBrush(Colors.DeepPink),
                    "logviewer" => new SolidColorBrush(Colors.Turquoise),
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility == Visibility.Visible;
            }
            return false;
        }
    }

    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return false;
        }
    }

    public class BooleanToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && parameter is string paramString)
            {
                var parts = paramString.Split('|');
                if (parts.Length == 2)
                {
                    return boolValue ? parts[0] : parts[1];
                }
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BooleanToSymbolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && parameter is string paramString)
            {
                var parts = paramString.Split('|');
                if (parts.Length == 2)
                {
                    var symbolName = boolValue ? parts[0] : parts[1];
                    if (Enum.TryParse<Symbol>(symbolName, out var symbol))
                    {
                        return symbol;
                    }
                }
            }
            return Symbol.Help;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BooleanToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && parameter is string paramString)
            {
                var parts = paramString.Split('|');
                if (parts.Length == 2)
                {
                    var colorName = boolValue ? parts[0] : parts[1];
                    try
                    {
                        var color = (Color)ColorConverter.ConvertFromString(colorName);
                        return new SolidColorBrush(color);
                    }
                    catch
                    {
                        // Fallback to default colors
                        return boolValue ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Gray);
                    }
                }
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BooleanToMessageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is LogEntry entry)
            {
                if (value is bool isRawMode)
                {
                    return isRawMode ? entry.RawLine : entry.CompactMessage;
                }
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // New: Use MultiBinding converter so we don't rely on ConverterParameter binding
    public class RawOrCompactMessageConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
                return string.Empty;

            var isRaw = values[0] is bool b && b;
            var entry = values[1] as LogEntry;
            if (entry == null)
                return string.Empty;

            return isRaw ? entry.RawLine : entry.CompactMessage;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}