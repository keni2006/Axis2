using System;
using System.Globalization;
using System.Windows.Data;

namespace Axis2.WPF.Converters
{
    /// <summary>Uppercases a string header (leaves non-string content untouched) so every card
    /// header matches the Fluent design's uppercase section labels.</summary>
    public class UpperCaseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is string s ? s.ToUpperInvariant() : value;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value;
    }
}
