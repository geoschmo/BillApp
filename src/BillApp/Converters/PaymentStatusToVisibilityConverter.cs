using System.Globalization;
using System.Windows;
using System.Windows.Data;
using BillApp.Core.Enums;

namespace BillApp.Converters;

/// <summary>
/// Hides the "Mark as Paid" button when status is already Paid.
/// </summary>
public class PaymentStatusToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is PaymentStatus status)
        {
            return status == PaymentStatus.Paid ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
