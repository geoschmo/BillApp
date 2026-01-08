using System.Windows;
using BillApp.ViewModels;

namespace BillApp.Views.Dialogs;

public partial class PayBillDialog : Window
{
    public decimal PaidAmount { get; private set; }
    public DateTime PaidDate { get; private set; }
    public PaymentMethodItem? SelectedPaymentMethod { get; private set; }
    public string? Confirmation { get; private set; }

    public PayBillDialog(string payee, decimal amountDue, DateTime dueDate, IEnumerable<PaymentMethodItem> paymentMethods, PaymentMethodItem? defaultPaymentMethod = null)
    {
        InitializeComponent();

        BillInfoText.Text = payee;
        PaidAmountBox.Text = amountDue.ToString("F2");
        PaidDatePicker.SelectedDate = DateTime.Today;

        // Set up payment methods dropdown
        var methods = paymentMethods.ToList();
        PayMethodCombo.ItemsSource = methods;

        // Try to select the default payment method, otherwise default to (None)
        if (defaultPaymentMethod != null)
        {
            var match = methods.FirstOrDefault(m =>
                (m.IsCash && defaultPaymentMethod.IsCash) ||
                (m.AccountId.HasValue && m.AccountId == defaultPaymentMethod.AccountId));
            PayMethodCombo.SelectedItem = match ?? methods.FirstOrDefault();
        }
        else
        {
            PayMethodCombo.SelectedIndex = 0; // Default to (None)
        }

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
        SelectedPaymentMethod = PayMethodCombo.SelectedItem as PaymentMethodItem;
        Confirmation = string.IsNullOrWhiteSpace(ConfirmationBox.Text) ? null : ConfirmationBox.Text.Trim();
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
