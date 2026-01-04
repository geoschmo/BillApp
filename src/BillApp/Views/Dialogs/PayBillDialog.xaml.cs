using System.Windows;

namespace BillApp.Views.Dialogs;

public partial class PayBillDialog : Window
{
    public decimal PaidAmount { get; private set; }
    public DateTime PaidDate { get; private set; }

    public PayBillDialog(string payee, decimal amountDue, DateTime dueDate)
    {
        InitializeComponent();

        BillInfoText.Text = payee;
        PaidAmountBox.Text = amountDue.ToString("F2");
        PaidDatePicker.SelectedDate = dueDate;

        PaidAmountBox.Focus();
        PaidAmountBox.SelectAll();
    }

    private void PayButton_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(PaidAmountBox.Text, out var amount) || amount < 0)
        {
            MessageBox.Show("Please enter a valid amount.", "Invalid Amount",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            PaidAmountBox.Focus();
            return;
        }

        if (!PaidDatePicker.SelectedDate.HasValue)
        {
            MessageBox.Show("Please select a paid date.", "Invalid Date",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        PaidAmount = amount;
        PaidDate = PaidDatePicker.SelectedDate.Value;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
