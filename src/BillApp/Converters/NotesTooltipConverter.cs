using System.Globalization;
using System.Text;
using System.Windows.Data;

namespace BillApp.Converters;

/// <summary>
/// Combines bill and payee note text for tooltips.
/// </summary>
public class NotesTooltipConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var billNotes = values.Length > 0 ? values[0] as string : null;
        var payeeNotes = values.Length > 1 ? values[1] as string : null;

        var builder = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(billNotes))
        {
            builder.Append("Bill: ");
            builder.Append(billNotes.Trim());
        }

        if (!string.IsNullOrWhiteSpace(payeeNotes))
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append("Payee: ");
            builder.Append(payeeNotes.Trim());
        }

        return builder.ToString();
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
