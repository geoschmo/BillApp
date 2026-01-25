using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BillApp.Converters;

/// <summary>
/// Converts multiple note strings to Visible when any has content.
/// </summary>
public class NotesIndicatorVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        foreach (var value in values)
        {
            if (value is string text && !string.IsNullOrWhiteSpace(text))
            {
                return Visibility.Visible;
            }
        }

        return Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
