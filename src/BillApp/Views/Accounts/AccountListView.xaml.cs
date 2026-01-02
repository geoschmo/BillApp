using System.Windows;
using System.Windows.Controls;
using BillApp.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BillApp.Views.Accounts;

public partial class AccountListView : UserControl
{
    private const string ViewName = "AccountListView";
    private ISettingsService? _settingsService;

    public AccountListView()
    {
        InitializeComponent();
        Loaded += AccountListView_Loaded;
    }

    private void AccountListView_Loaded(object sender, RoutedEventArgs e)
    {
        _settingsService = App.Services.GetService<ISettingsService>();
        RestoreColumnWidths();

        // Find the DataGrid and subscribe to column width changes
        if (FindName("AccountsDataGrid") is DataGrid dataGrid)
        {
            foreach (var column in dataGrid.Columns)
            {
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

        if (FindName("AccountsDataGrid") is DataGrid dataGrid)
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
}
