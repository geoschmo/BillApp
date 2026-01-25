using System.Windows;
using System.Windows.Controls;
using BillApp.Core.Models;
using BillApp.Services;
using BillApp.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace BillApp.Views.Bills;

public partial class BillListView : System.Windows.Controls.UserControl
{
    private const string ViewName = "BillListView";
    private ISettingsService? _settingsService;

    public BillListView()
    {
        InitializeComponent();
        Loaded += BillListView_Loaded;
    }

    private void BillListView_Loaded(object sender, RoutedEventArgs e)
    {
        _settingsService = App.Services.GetService<ISettingsService>();
        RestoreColumnWidths();

        // Find the DataGrid and subscribe to column width changes
        if (FindName("BillsDataGrid") is DataGrid dataGrid)
        {
            foreach (var column in dataGrid.Columns)
            {
                // Use a closure to capture the column header
                var header = column.Header?.ToString() ?? "";
                var descriptor = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(
                    DataGridColumn.ActualWidthProperty, typeof(DataGridColumn));
                descriptor?.AddValueChanged(column, (s, args) => SaveColumnWidth(header, column.ActualWidth));
            }
        }
    }

    private void RestoreColumnWidths()
    {
        if (_settingsService == null) return;

        if (FindName("BillsDataGrid") is DataGrid dataGrid)
        {
            foreach (var column in dataGrid.Columns)
            {
                var header = column.Header?.ToString();
                if (string.IsNullOrEmpty(header)) continue;

                var savedWidth = _settingsService.Settings.GetColumnWidth(ViewName, header, 0);
                if (savedWidth > 0)
                {
                    column.Width = new DataGridLength(savedWidth);
                }
            }
        }
    }

    private void SaveColumnWidth(string header, double width)
    {
        if (_settingsService == null || string.IsNullOrEmpty(header)) return;

        _settingsService.Settings.SetColumnWidth(ViewName, header, width);
        _settingsService.Save();
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
