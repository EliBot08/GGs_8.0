using System;
using System.Globalization;
using System.Windows.Data;
using GGs.ErrorLogViewer.Models;

namespace GGs.ErrorLogViewer.Views.Converters
{
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
}
