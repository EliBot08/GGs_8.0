using System;
using System.Threading.Tasks;
using System.Windows;

namespace GGs.Desktop.Views
{
    public partial class LicenseDiagnosticsWindow : Window
    {
        private readonly Services.LicenseService _svc = new();

        public LicenseDiagnosticsWindow()
        {
            InitializeComponent();
            BtnRefresh.Click += (_, __) => RefreshMeta();
            BtnClose.Click += (_, __) => this.Close();
            BtnRevalidate.Click += async (_, __) => await RevalidateNow();
            Loaded += (_, __) => RefreshMeta();
        }

        private void RefreshMeta()
        {
            try
            {
                var m = _svc.GetMetadata();
                TxtDeviceId.Text = m.DeviceId ?? "—";
                TxtKeyFingerprint.Text = m.KeyFingerprint ?? "—";
                TxtLastValidation.Text = m.LastValidationUtc?.ToString("O") ?? "—";
                TxtLastOnline.Text = m.LastOnlineCheckUtc?.ToString("O") ?? "—";
                TxtNextReval.Text = m.NextRevalidationUtc?.ToString("O") ?? "—";
                TxtStatus.Text = m.RevocationStatus ?? "—";
            }
            catch (Exception ex)
            {
                try { MessageBox.Show(this, $"Failed to load metadata: {ex.Message}", "GGs", MessageBoxButton.OK, MessageBoxImage.Warning); } catch { }
            }
        }

        private async Task RevalidateNow()
        {
            try
            {
                var (ok, msg) = await Services.LicenseRevalidationService.Instance.RevalidateNowAsync();
                var icon = ok ? MessageBoxImage.Information : MessageBoxImage.Warning;
                try { MessageBox.Show(this, msg ?? (ok ? "Revalidated." : "Revalidation failed."), "GGs", MessageBoxButton.OK, icon); } catch { }
                RefreshMeta();
            }
            catch (Exception ex)
            {
                try { MessageBox.Show(this, $"Error: {ex.Message}", "GGs", MessageBoxButton.OK, MessageBoxImage.Error); } catch { }
            }
        }
    }
}

