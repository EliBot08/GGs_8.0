using System.Text.Json;
using System.Windows;
using GGs.Desktop.Services;
using GGs.Shared.Licensing;

namespace GGs.Desktop.Views;

public partial class LicenseWindow : Window
{
    private readonly LicenseService _licenseService;

    public LicenseWindow(LicenseService licenseService)
    {
        try { InitializeComponent(); }
        catch (Exception ex)
        {
            try { GGs.Desktop.Services.AppLogger.LogError("LicenseWindow InitializeComponent failed", ex); } catch { }
        }
        _licenseService = licenseService;
    }

    private void ExitBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            this.Close();
            if (System.Windows.Application.Current?.Windows != null && System.Windows.Application.Current.Windows.Count == 0)
            {
                var rw = new RecoveryWindow("License window closed. Keeping app running for logging.");
                rw.Show();
            }
        }
        catch { Application.Current?.Shutdown(); }
    }

    private async void ValidateBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusText.Text = "Validating license…";
            var license = JsonSerializer.Deserialize<SignedLicense>(LicenseText.Text);
            if (license == null) { StatusText.Text = "Invalid JSON."; return; }
            var (ok, msg, offline) = await _licenseService.ValidateAndSaveDetailedAsync(license);
            if (!ok)
            {
                StatusText.Text = msg ?? "License invalid.";
                return;
            }
            StatusText.Text = msg ?? (offline ? "Validated offline." : "License validated.");
            // Apply current theme using the centralized ThemeManager (old tier-based theme system removed)
            try { GGs.Desktop.Services.ThemeManagerService.Instance.ApplyTheme(); } catch { }
            try { Services.EntitlementsService.UpdateTier(license.Payload.Tier); } catch { }
            // mark onboarding state
            var st = Services.FirstRunService.Load();
            st.LicenseActivated = true; Services.FirstRunService.Save(st);
            var mw = new Views.ModernMainWindow();
            mw.Show();
            if (this.Owner is ActivationWindow)
            {
                try { this.Owner.Close(); } catch { }
            }
            this.Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }
}
