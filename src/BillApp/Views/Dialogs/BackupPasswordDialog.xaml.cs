using System.Windows;
using BillApp.Infrastructure.Services;

namespace BillApp.Views.Dialogs;

/// <summary>
/// Dialog for entering backup password (for create or restore operations).
/// </summary>
public partial class BackupPasswordDialog : Window
{
    private readonly bool _isRestoreMode;

    /// <summary>
    /// Gets the entered password if dialog was confirmed.
    /// </summary>
    public string? Password { get; private set; }

    /// <summary>
    /// Creates a backup password dialog.
    /// </summary>
    /// <param name="isRestoreMode">True for restore (single password field), false for create (with confirmation).</param>
    public BackupPasswordDialog(bool isRestoreMode = false)
    {
        InitializeComponent();
        _isRestoreMode = isRestoreMode;

        if (isRestoreMode)
        {
            Title = "Enter Backup Password";
            InstructionsText.Text = "Enter the password used to encrypt this backup.";
            ConfirmLabel.Visibility = Visibility.Collapsed;
            ConfirmPasswordBox.Visibility = Visibility.Collapsed;
        }
        else
        {
            Title = "Create Backup Password";
            InstructionsText.Text = "Enter a password to encrypt your backup. You will need this password to restore the backup.\n\nPassword must be at least 8 characters with letters and numbers.";
        }

        PasswordBox.Focus();
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        ValidationMessage.Visibility = Visibility.Collapsed;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        var password = PasswordBox.Password;

        if (_isRestoreMode)
        {
            // For restore, just check password is not empty
            if (string.IsNullOrEmpty(password))
            {
                ShowValidationError("Password is required.");
                return;
            }
        }
        else
        {
            // For create, validate password strength
            var (isValid, errorMessage) = BackupEncryptionHelper.ValidatePassword(password);
            if (!isValid)
            {
                ShowValidationError(errorMessage!);
                return;
            }

            // Check passwords match
            if (password != ConfirmPasswordBox.Password)
            {
                ShowValidationError("Passwords do not match.");
                ConfirmPasswordBox.Focus();
                return;
            }
        }

        Password = password;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowValidationError(string message)
    {
        ValidationMessage.Text = message;
        ValidationMessage.Visibility = Visibility.Visible;
        PasswordBox.Focus();
    }
}
