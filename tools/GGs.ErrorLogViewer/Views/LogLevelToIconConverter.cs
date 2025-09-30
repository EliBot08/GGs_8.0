using System;
using System.Globalization;
using System.Windows.Data;
using ModernWpf.Controls;
using GGs.ErrorLogViewer.Models;

namespace GGs.ErrorLogViewer.Views.Converters
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
}
