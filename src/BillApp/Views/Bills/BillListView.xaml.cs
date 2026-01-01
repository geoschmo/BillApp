using System.Windows.Controls;
using BillApp.Core.Models;
using BillApp.ViewModels;

namespace BillApp.Views.Bills;

public partial class BillListView : UserControl
{
    public BillListView()
    {
        InitializeComponent();
    }

    private async void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction == DataGridEditAction.Cancel)
            return;

        // Get the edited bill
        if (e.Row.Item is Bill bill && DataContext is BillListViewModel viewModel)
        {
            // Use Dispatcher to ensure the binding has updated before saving
            await Dispatcher.InvokeAsync(async () =>
            {
                await viewModel.SaveBillInlineCommand.ExecuteAsync(bill);
            }, System.Windows.Threading.DispatcherPriority.Background);
        }
    }
}
