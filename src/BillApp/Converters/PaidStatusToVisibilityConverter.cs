using System.Globalization;
using System.Windows;
using System.Windows.Data;
using BillApp.Core.Enums;

namespace BillApp.Converters;

/// <summary>
/// Shows the "New Bill" button only when status is Paid.
/// </summary>
public class PaidStatusToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is PaymentStatus status)
        {
            return status == PaymentStatus.Paid ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
