using System.Windows;
using System.Windows.Input;

namespace BillApp.Views.Dialogs;

public partial class DatabaseSetupDialog : Window
{
    public bool UseSampleData { get; private set; }

    public DatabaseSetupDialog()
    {
        InitializeComponent();
    }

    private void EmptyDatabase_Click(object sender, MouseButtonEventArgs e)
    {
        UseSampleData = false;
        DialogResult = true;
    }

    private void SampleData_Click(object sender, MouseButtonEventArgs e)
    {
        UseSampleData = true;
        DialogResult = true;
    }
}
