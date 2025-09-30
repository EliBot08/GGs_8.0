using System;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;

namespace GGs.Desktop.Views
{
    public partial class UpdateWindow : Window
    {
        private readonly Services.AutoUpdateService _svc = new();
        private Services.UpdateInfo? _info;

        public UpdateWindow(Services.UpdateInfo info)
        {
            InitializeComponent();
            _info = info;
            TxtVersion.Text = $"Version: {info.Version}";
            TxtChannel.Text = $"Channel: {info.Channel}";
            TxtNotes.Text = info.Notes ?? "";
            BtnClose.Click += (_, __) => this.Close();
            BtnInstall.Click += async (_, __) => await InstallUpdate();

            try
            {
                // Initialize channel selection from settings or manifest
                var chan = GGs.Desktop.Services.SettingsService.UpdateChannel;
                SetChannelSelection(string.IsNullOrWhiteSpace(chan) ? info.Channel : chan);
                CmbChannel.SelectionChanged += CmbChannel_SelectionChanged;

                // Initialize silent + bandwidth controls
                ChkSilent.IsChecked = GGs.Desktop.Services.SettingsService.UpdateSilent;
                ChkSilent.Checked += (_, __) => GGs.Desktop.Services.SettingsService.UpdateSilent = true;
                ChkSilent.Unchecked += (_, __) => GGs.Desktop.Services.SettingsService.UpdateSilent = false;
                var kb = GGs.Desktop.Services.SettingsService.UpdateBandwidthLimitKBps;
                if (kb < 0) kb = 0;
                SldBandwidth.Value = kb;
                TxtBandwidth.Text = kb.ToString();
                SldBandwidth.ValueChanged += (s, e) =>
                {
                    try
                    {
                        var val = (int)SldBandwidth.Value;
                        GGs.Desktop.Services.SettingsService.UpdateBandwidthLimitKBps = val;
                        TxtBandwidth.Text = val.ToString();
                    }
                    catch { }
                };
            }
            catch { }
        }

        private void SetChannelSelection(string channel)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(channel)) channel = "stable";
                foreach (var item in CmbChannel.Items)
                {
                    if (item is ComboBoxItem cbi && string.Equals(cbi.Content?.ToString(), channel, StringComparison.OrdinalIgnoreCase))
                    {
                        CmbChannel.SelectedItem = cbi;
                        break;
                    }
                }
            }
            catch { }
        }

        private void CmbChannel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (CmbChannel.SelectedItem is ComboBoxItem cbi)
                {
                    var selected = cbi.Content?.ToString() ?? "stable";
                    GGs.Desktop.Services.SettingsService.UpdateChannel = selected;
                }
            }
            catch { }
        }

        private async Task InstallUpdate()
        {
            try
            {
                if (_info == null) return;
                Progress.Visibility = Visibility.Visible;
                var prog = new Progress<int>(p => Progress.Value = p);
                BtnInstall.IsEnabled = false;
                var res = await _svc.DownloadAndInstallAsync(_info, prog);
                MessageBox.Show(res.ok ? "Installer launched" : (res.message ?? "Failed"), res.ok ? "Update" : "Error", MessageBoxButton.OK, res.ok ? MessageBoxImage.Information : MessageBoxImage.Error);
                BtnInstall.IsEnabled = true;
                Progress.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                BtnInstall.IsEnabled = true;
                Progress.Visibility = Visibility.Collapsed;
            }
        }
    }
}

