using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BillApp.Converters;

/// <summary>
/// Converts a numeric value to Visibility.
/// Zero = Visible, Non-zero = Collapsed.
/// Useful for showing "empty state" messages when a collection has no items.
/// </summary>
public class ZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intValue)
        {
            return intValue == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
