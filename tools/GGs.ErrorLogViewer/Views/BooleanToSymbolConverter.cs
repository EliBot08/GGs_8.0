#nullable enable
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ModernWpf.Controls;

namespace GGs.ErrorLogViewer.Views.Converters
{
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
            return DependencyProperty.UnsetValue;
        }
    }
}
