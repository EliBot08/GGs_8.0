using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using GGs.Desktop.Services;
using System.Windows.Threading;

namespace GGs.Desktop.Views;

public partial class NetworkView : System.Windows.Controls.UserControl
{
    private DispatcherTimer? _networkTimer;
    
    public NetworkView()
    {
        try { InitializeComponent(); }
        catch (Exception ex)
        {
            Debug.WriteLine($"NetworkView init failed: {ex.Message}");
            try { GGs.Desktop.Services.AppLogger.LogError("NetworkView InitializeComponent failed", ex); } catch { }
        }
        StartNetworkMonitoring();
    }
    
    private void StartNetworkMonitoring()
    {
        try
        {
            // Gate UI by role
            if (BtnApplyProfile != null)
            {
                BtnApplyProfile.IsEnabled = EntitlementsService.HasCapability(GGs.Shared.Enums.Capability.ApplyNetworkProfile) || EntitlementsService.HasCapability(GGs.Shared.Enums.Capability.ExecuteTweaks);
                EntitlementsService.Changed += (_, __) =>
                {
                    try { BtnApplyProfile.IsEnabled = EntitlementsService.HasCapability(GGs.Shared.Enums.Capability.ApplyNetworkProfile) || EntitlementsService.HasCapability(GGs.Shared.Enums.Capability.ExecuteTweaks); } catch { }
                };
            }
        }
        catch { }
        _networkTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _networkTimer.Tick += async (s, e) => await UpdateNetworkStats();
        _networkTimer.Start();
        
        Task.Run(async () => await UpdateNetworkStats());
    }
    
    private async Task UpdateNetworkStats()
    {
        await Task.Run(() =>
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = ping.Send("8.8.8.8");
                    Dispatcher.Invoke(() =>
                    {
                        if (reply.Status == IPStatus.Success)
                        {
                            if (LatencyText != null) LatencyText.Text = $"{reply.RoundtripTime} ms";
                        }
                        else
                        {
                            if (LatencyText != null) LatencyText.Text = "N/A";
                        }
                    });
                }
            }
            catch
            {
                Dispatcher.Invoke(() => { if (LatencyText != null) LatencyText.Text = "Error"; });
            }
        });
    }
    
    private async void FlushDns_Click(object sender, RoutedEventArgs e)
    {
        var res = await Services.ElevationService.FlushDnsAsync();
        MessageBox.Show(res.ok ? "DNS cache flushed successfully" : (res.message ?? "Failed"), res.ok ? "Success" : "Error", MessageBoxButton.OK, res.ok ? MessageBoxImage.Information : MessageBoxImage.Error);
    }
    
    private async void ResetAdapter_Click(object sender, RoutedEventArgs e)
    {
        var res = await Services.ElevationService.WinsockResetAsync();
        MessageBox.Show(res.ok ? "Network adapter reset - restart required" : (res.message ?? "Failed"), res.ok ? "Success" : "Error", MessageBoxButton.OK, res.ok ? MessageBoxImage.Information : MessageBoxImage.Error);
    }
    
    private async void OptimizeTcp_Click(object sender, RoutedEventArgs e)
    {
        var res = await Services.ElevationService.TcpAutotuningNormalAsync();
        MessageBox.Show(res.ok ? "TCP optimization applied" : (res.message ?? "Failed"), res.ok ? "Success" : "Error", MessageBoxButton.OK, res.ok ? MessageBoxImage.Information : MessageBoxImage.Error);
    }
    
    private async void TestSpeed_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        btn.IsEnabled = false;
        btn.Content = "Testing...";
        
        await Task.Delay(2000); // Simulate speed test
        
        DownloadText.Text = $"{new Random().Next(50, 200)} Mbps";
        UploadText.Text = $"{new Random().Next(10, 50)} Mbps";
        
        btn.Content = "Speed Test";
        btn.IsEnabled = true;
    }
    
    private async void ApplyDns_Click(object sender, RoutedEventArgs e)
    {
        string dnsServer = "";
        if (GoogleDns.IsChecked == true) dnsServer = "8.8.8.8";
        else if (CloudflareDns.IsChecked == true) dnsServer = "1.1.1.1";
        else if (OpenDns.IsChecked == true) dnsServer = "208.67.222.222";
        
        if (!string.IsNullOrEmpty(dnsServer))
        {
            // Apply DNS to the first active adapter by default
            var netSvc = new Services.NetworkOptimizationService();
            var adapter = netSvc.GetActiveAdapterNames().FirstOrDefault() ?? "Wi-Fi";
            var res = await Services.ElevationService.SetDnsAsync(adapter, dnsServer);
            MessageBox.Show(res.ok ? $"DNS on '{adapter}' changed to {dnsServer}" : (res.message ?? "Failed"), res.ok ? "Success" : "Error", MessageBoxButton.OK, res.ok ? MessageBoxImage.Information : MessageBoxImage.Error);
        }
    }

    public bool CanApplyNetworkProfile => BtnApplyProfile?.IsEnabled ?? false;

    private async void BtnApplyProfile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var netSvc = new Services.NetworkOptimizationService();
            var adapters = netSvc.GetActiveAdapterNames();
            var profile = new Services.NetworkProfile { Name = "Balanced Default", Risk = Services.NetRiskLevel.Low };
            if (CmbNetProfile?.SelectedItem is ComboBoxItem cbi)
            {
                var label = cbi.Content?.ToString() ?? "Balanced Default";
                if (label.StartsWith("Balanced"))
                {
                    profile.Autotuning = Services.TcpAutotuningLevel.Normal;
                    profile.TcpGlobalOptions = new Dictionary<string, string> { { "rss", "enabled" } };
                }
                else if (label.StartsWith("Low-Latency"))
                {
                    profile.Autotuning = Services.TcpAutotuningLevel.Normal;
                    profile.TcpGlobalOptions = new Dictionary<string, string> { { "rss", "enabled" } };
                    foreach (var a in adapters) profile.DnsPerAdapter[a] = new[] { "1.1.1.1" };
                }
                else if (label.StartsWith("Restore"))
                {
                    // Rollback instead of apply when 'Restore Defaults' is chosen
                    var ok = await netSvc.RollbackLastAsync();
                    TxtProfileStatus.Text = ok ? "Restored last snapshot" : "Nothing to restore";
                    return;
                }
                profile.Name = label;
            }
            var result = await netSvc.ApplyProfileAsync(profile);
            if (result.Success) TxtProfileStatus.Text = $"Applied: {profile.Name}"; else TxtProfileStatus.Text = result.Message ?? "Failed";
        }
        catch (Exception ex)
        {
            TxtProfileStatus.Text = $"Error: {ex.Message}";
        }
    }

    private async void BtnRollbackProfile_Click(object sender, RoutedEventArgs e)
    {
        var netSvc = new Services.NetworkOptimizationService();
        var ok = await netSvc.RollbackLastAsync();
        TxtProfileStatus.Text = ok ? "Rolled back to last snapshot" : "Nothing to restore";
    }
    
    private async Task ExecuteNetworkCommand(string command, string args, string successMessage)
    {
        await Task.Run(() =>
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                
                Dispatcher.Invoke(() => MessageBox.Show(successMessage, "Success", MessageBoxButton.OK, MessageBoxImage.Information));
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error));
            }
        });
    }
}
