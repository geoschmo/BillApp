using System.Windows;
using BillApp.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace BillApp.Views.Investments;

public partial class InvestmentsImportDialog : Window
{
    public InvestmentsImportDialog()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<InvestmentsViewModel>();
    }
}
