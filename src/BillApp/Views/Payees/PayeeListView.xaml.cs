using System.Windows.Controls;
using System.Windows.Input;
using BillApp.Core.Models;
using BillApp.ViewModels;

namespace BillApp.Views.Payees;

public partial class PayeeListView : UserControl
{
    public PayeeListView()
    {
        InitializeComponent();
    }

    private void DataGridRow_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGridRow row && row.DataContext is Payee payee)
        {
            if (DataContext is PayeeListViewModel vm)
            {
                vm.EditPayeeCommand.Execute(payee);
            }
        }
    }
}
