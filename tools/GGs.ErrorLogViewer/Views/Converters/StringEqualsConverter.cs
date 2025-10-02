#nullable enable
using System;
using System.Globalization;
using System.Windows.Data;

namespace GGs.ErrorLogViewer.Views.Converters
{
    /// <summary>
    /// Compares two string inputs and returns true when they match, ignoring case.
    /// Supports usage as both an <see cref="IValueConverter"/> and <see cref="IMultiValueConverter"/>.
    /// </summary>
    public sealed class StringEqualsConverter : IValueConverter, IMultiValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var left = value?.ToString();
            var right = parameter?.ToString();
            if (string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        public object? Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values is null || values.Length < 2)
            {
                return false;
            }

            var left = values[0]?.ToString();
            var right = values[1]?.ToString();

            if (parameter is string comparisonOverride && !string.IsNullOrWhiteSpace(comparisonOverride))
            {
                right = comparisonOverride;
            }

            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        {
            return Array.Empty<object>();
        }
    }
}
