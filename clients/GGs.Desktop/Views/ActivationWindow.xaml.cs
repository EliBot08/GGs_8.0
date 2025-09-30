using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace GGs.Desktop.Views;

public partial class ActivationWindow : Window
{
    private const string BuyLink = "https://discord.gg/3pBX9ymWRU";

    public ActivationWindow()
    {
        try { InitializeComponent(); }
        catch (Exception ex)
        {
            try { GGs.Desktop.Services.AppLogger.LogError("ActivationWindow InitializeComponent failed", ex); } catch { }
        }
    }

    private void LicenseTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var code = LicenseTextBox.Text?.Trim() ?? string.Empty;
        ActivateButton.IsEnabled = code.Length == 16; // exactly 16 chars
    }

    private void ActivateButton_Click(object sender, RoutedEventArgs e)
    {
        // For this build, activation is completed by pasting your signed license JSON.
        // Open the LicenseWindow to validate and store the license.
        OpenLicensePasteWindow();
    }

    private void PasteJsonButton_Click(object sender, RoutedEventArgs e)
    {
        OpenLicensePasteWindow();
    }

    private void OpenLicensePasteWindow()
    {
        try
        {
            var svc = new Services.LicenseService();
            var w = new LicenseWindow(svc) { Owner = this };
            this.Hide();
            w.Show();
        }
        catch
        {
            MessageBox.Show(this, "Unable to open the activation dialog.", "GGs", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void TierCard_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = BuyLink,
                UseShellExecute = true
            });
        }
        catch
        {
            MessageBox.Show(this, "Unable to open the link in your default browser.", "Open Link", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
