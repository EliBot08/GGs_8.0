#nullable enable
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GGs.ErrorLogViewer.Views.Converters
{
    public class StringEqualsToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return Visibility.Collapsed;

            string? actualValue = value.ToString();
            string? expectedValue = parameter.ToString();

            return string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
