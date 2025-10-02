#nullable enable
using System;
using System.Globalization;
using System.Windows.Data;
using GGs.ErrorLogViewer.Models;

namespace GGs.ErrorLogViewer.Views.Converters
{
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
            return new object[] { System.Windows.DependencyProperty.UnsetValue };
        }
    }
}
