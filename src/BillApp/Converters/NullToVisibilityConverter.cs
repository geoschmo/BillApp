using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BillApp.Converters;

/// <summary>
/// Converts null/empty to Collapsed, non-null to Visible.
/// Useful for showing validation errors only when they exist.
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        var hasValue = value != null;

        if (value is string str)
        {
            hasValue = !string.IsNullOrWhiteSpace(str);
        }

        // If parameter is "Invert", flip the logic
        if (parameter is string param && param == "Invert")
        {
            return hasValue ? Visibility.Collapsed : Visibility.Visible;
        }

        return hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
