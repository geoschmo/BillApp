using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BillApp.Converters;

/// <summary>
/// Converts a boolean value to Visibility.
/// True = Visible, False = Collapsed
/// Similar to Angular's *ngIf directive.
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            // If parameter is "Invert", flip the logic
            if (parameter is string param && param == "Invert")
            {
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility == Visibility.Visible;
        }
        return false;
    }
}
