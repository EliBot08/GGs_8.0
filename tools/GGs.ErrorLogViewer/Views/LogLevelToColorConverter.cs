#nullable enable
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using GGs.ErrorLogViewer.Models;

namespace GGs.ErrorLogViewer.Views.Converters
{
    public class LogLevelToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LogLevel level)
            {
                return level switch
                {
                    LogLevel.Trace => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#808080")),
                    LogLevel.Debug => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F00FF")),
                    LogLevel.Information => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#007ACC")),
                    LogLevel.Success => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#28A745")),
                    LogLevel.Warning => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFC107")),
                    LogLevel.Error => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC3545")),
                    LogLevel.Critical => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A50000")),
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
}
