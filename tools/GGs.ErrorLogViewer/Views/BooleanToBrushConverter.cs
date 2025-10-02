#nullable enable
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace GGs.ErrorLogViewer.Views.Converters
{
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
            return DependencyProperty.UnsetValue;
        }
    }
}
