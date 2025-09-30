using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.IO;
using GGs.Shared.Api;
using GGs.Shared.Tweaks;
using GGs.Shared.Enums;
using GGs.Shared.Models;
using GGs.Desktop.Services;
using GGs.Desktop.Extensions;

namespace GGs.Desktop.ViewModels;

public class AnalyticsViewModel : BaseViewModel
{
    private readonly GGs.Shared.Api.ApiClient _api;
    
    public ObservableCollection<GGs.Shared.Models.TweakStatistic> TweakStatistics { get; }
    public ObservableCollection<LicenseStatistic> LicenseStatistics { get; }
    public ObservableCollection<GGs.Shared.Tweaks.TweakApplicationLog> RecentLogs { get; }
    
    private int _totalTweaksApplied;
    public int TotalTweaksApplied
    {
        get => _totalTweaksApplied;
        set => SetField(ref _totalTweaksApplied, value);
    }
    
    private double _successRate;
    public double SuccessRate
    {
        get => _successRate;
        set => SetField(ref _successRate, value);
    }
    
    private string _mostPopularTweak = string.Empty;
    public string MostPopularTweak
    {
        get => _mostPopularTweak;
        set => SetField(ref _mostPopularTweak, value);
    }
    
    private int _activeLicenses;
    public int ActiveLicenses
    {
        get => _activeLicenses;
        set => SetField(ref _activeLicenses, value);
    }
    
    private int _connectedDevices;
    public int ConnectedDevices
    {
        get => _connectedDevices;
        set => SetField(ref _connectedDevices, value);
    }

    private string _bannerMessage = string.Empty;
    public string BannerMessage
    {
        get => _bannerMessage;
        set => SetField(ref _bannerMessage, value);
    }

    private bool _usedServerAnalytics;
    public bool UsedServerAnalytics
    {
        get => _usedServerAnalytics;
        set => SetField(ref _usedServerAnalytics, value);
    }
    
    public ICommand RefreshCommand { get; }
    public ICommand ExportReportCommand { get; }
    
    public AnalyticsViewModel(GGs.Shared.Api.ApiClient api)
    {
        _api = api;
        
        TweakStatistics = new ObservableCollection<GGs.Shared.Models.TweakStatistic>();
        LicenseStatistics = new ObservableCollection<LicenseStatistic>();
        RecentLogs = new ObservableCollection<GGs.Shared.Tweaks.TweakApplicationLog>();
        
        RefreshCommand = new RelayCommand(async () => await LoadAnalytics());
        ExportReportCommand = new RelayCommand(ExportReport);
    }
    
    public async Task LoadAnalytics()
    {
        BannerMessage = string.Empty;
        UsedServerAnalytics = false;

        var canUseServer = EntitlementsService.HasCapability(GGs.Shared.Enums.Capability.ViewAnalytics);
        if (canUseServer)
        {
            try
            {
                var summary = await _api.GetAnalyticsSummaryAsync(7);
                var top = await _api.GetTopTweaksAsync(10);
                // var devices = await _api.GetAnalyticsDevicesAsync(); // Method not implemented yet

                if (summary != null)
                {
                    TotalTweaksApplied = summary.logsSince;
                    ActiveLicenses = summary.licenses; // total licenses; active breakdown not provided here
                    ConnectedDevices = summary.devicesConnected;
                }
                else
                {
                    TotalTweaksApplied = 0;
                    ActiveLicenses = 0;
                    ConnectedDevices = 0; // devices variable not available
                }

                // Map top tweaks
                TweakStatistics.Clear();
                MostPopularTweak = top.FirstOrDefault()?.Name ?? "None";
                foreach (var t in top.Take(10))
                {
                    TweakStatistics.Add(new GGs.Shared.Models.TweakStatistic { Name = t.Name ?? "(unknown)", UsageCount = t.UsageCount, SuccessRate = t.SuccessRate, LastUsed = t.LastUsed, Category = t.Category });
                }

                // Recent logs (optional best-effort)
                try
                {
                    var logs = await _api.GetAuditLogsAsync();
                    RecentLogs.Clear();
                    foreach (var log in logs.Take(100)) RecentLogs.Add(log);
                    if (logs.Any())
                    {
                        var successCount = logs.Count(l => l.Success);
                        SuccessRate = (double)successCount / logs.Count * 100;
                    }
                    else
                    {
                        SuccessRate = top.Average(x => x.SuccessRate);
                    }
                }
                catch { /* ignore recents if not authorized */ }

                UsedServerAnalytics = true;
            }
            catch
            {
                // fall back gracefully if server analytics not accessible
                BannerMessage = "Server analytics unavailable (401/403/404). Showing limited local analytics.";
                ApplyLocalFallback();
            }
        }
        else
        {
            BannerMessage = "Analytics limited by license/roles. Showing local analytics.";
            ApplyLocalFallback();
        }
    }

    private void ApplyLocalFallback()
    {
        // Apply local fallback when server data unavailable - show empty state
        TotalTweaksApplied = 0;
        SuccessRate = 0;
        MostPopularTweak = "None";
        ActiveLicenses = 0;
        ConnectedDevices = 0;
        TweakStatistics.Clear();
        RecentLogs.Clear();
    }
    
    private void ExportReport()
    {
        var report = $"GGs Analytics Report\n" +
                    $"Generated: {DateTime.Now}\n\n" +
                    $"Total Tweaks Applied: {TotalTweaksApplied}\n" +
                    $"Success Rate: {SuccessRate:F2}%\n" +
                    $"Most Popular Tweak: {MostPopularTweak}\n" +
                    $"Active Licenses: {ActiveLicenses}\n" +
                    $"Connected Devices: {ConnectedDevices}\n\n" +
                    $"Top Tweaks:\n";
        
        foreach (var tweak in TweakStatistics.Take(5))
        {
            report += $"  - {tweak.Name}: {tweak.UsageCount} applications, {tweak.SuccessRate:F1}% success\n";
        }
        
        // Save to file
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), 
                               $"GGs_Report_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        File.WriteAllText(path, report);
    }
}

public class TweakStatistic
{
    public string Name { get; set; } = string.Empty;
    public int ApplicationCount { get; set; }
    public double SuccessRate { get; set; }
    public DateTime LastApplied { get; set; }
}

public class LicenseStatistic
{
    public string Tier { get; set; } = string.Empty;
    public int Count { get; set; }
    public int ActiveCount { get; set; }
}
