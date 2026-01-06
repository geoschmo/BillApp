using System.Windows;

namespace BillApp.Views.Dialogs;

public partial class NewBillFromExistingDialog : Window
{
    public DateTime DueDate { get; private set; }
    public decimal AmountDue { get; private set; }
    public decimal Balance { get; private set; }

    public NewBillFromExistingDialog(string payee, DateTime suggestedDueDate, decimal suggestedAmountDue, decimal suggestedBalance)
    {
        InitializeComponent();

        BillInfoText.Text = $"New bill from: {payee}";
        DueDatePicker.SelectedDate = suggestedDueDate;
        AmountDueBox.Text = suggestedAmountDue.ToString("F2");
        BalanceBox.Text = suggestedBalance.ToString("F2");

        DueDatePicker.Focus();
    }

    private void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        if (!DueDatePicker.SelectedDate.HasValue)
        {
            MessageBox.Show("Please select a due date.", "Invalid Date",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(AmountDueBox.Text, out var amountDue) || amountDue < 0)
        {
            MessageBox.Show("Please enter a valid amount due.", "Invalid Amount",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            AmountDueBox.Focus();
            return;
        }

        if (!decimal.TryParse(BalanceBox.Text, out var balance) || balance < 0)
        {
            MessageBox.Show("Please enter a valid balance.", "Invalid Balance",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            BalanceBox.Focus();
            return;
        }

        DueDate = DueDatePicker.SelectedDate.Value;
        AmountDue = amountDue;
        Balance = balance;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
