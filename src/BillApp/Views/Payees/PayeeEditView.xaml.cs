using System.Windows;
using System.Windows.Controls;
using BillApp.ViewModels;

namespace BillApp.Views.Payees;

public partial class PayeeEditView : UserControl
{
    public PayeeEditView()
    {
        InitializeComponent();
        Loaded += PayeeEditView_Loaded;
    }

    private void PayeeEditView_Loaded(object sender, RoutedEventArgs e)
    {
        // Set password box value from ViewModel when view loads
        if (DataContext is PayeeEditViewModel vm && !string.IsNullOrEmpty(vm.Password))
        {
            PasswordBox.Password = vm.Password;
        }
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        // Update ViewModel password when password box changes
        if (DataContext is PayeeEditViewModel vm)
        {
            vm.Password = PasswordBox.Password;
        }
    }
}
