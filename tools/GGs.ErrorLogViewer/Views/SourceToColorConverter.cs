#nullable enable
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace GGs.ErrorLogViewer.Views.Converters
{
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
            return System.Windows.DependencyProperty.UnsetValue;
        }
    }
}
