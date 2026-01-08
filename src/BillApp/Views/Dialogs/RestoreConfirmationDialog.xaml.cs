using System.Windows;
using BillApp.Core.Models;

namespace BillApp.Views.Dialogs;

/// <summary>
/// Dialog to confirm restore operation, showing backup details.
/// </summary>
public partial class RestoreConfirmationDialog : Window
{
    /// <summary>
    /// Creates a restore confirmation dialog.
    /// </summary>
    /// <param name="manifest">The backup manifest to display.</param>
    public RestoreConfirmationDialog(BackupManifest manifest)
    {
        InitializeComponent();

        CreatedAtText.Text = manifest.CreatedAt.ToLocalTime().ToString("g");
        AppVersionText.Text = manifest.AppVersion;
        BillsCountText.Text = manifest.EntityCounts.Bills.ToString();
        PayeesCountText.Text = manifest.EntityCounts.Payees.ToString();
        CategoriesCountText.Text = manifest.EntityCounts.Categories.ToString();
    }

    private void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
