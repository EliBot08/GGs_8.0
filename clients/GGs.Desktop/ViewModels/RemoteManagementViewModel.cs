using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using GGs.Shared.Api;
using GGs.Shared.Tweaks;
using GGs.Shared.Enums;
using GGs.Desktop.Extensions;
using GGs.Desktop.Services;
using System.Windows;

namespace GGs.Desktop.ViewModels;

public class RemoteManagementViewModel : BaseViewModel
{
    private readonly GGs.Shared.Api.ApiClient _api;
    private readonly GGs.Shared.Api.AuthService _auth;
    private readonly HashSet<string> _pending = new();

    private bool _allowedExecuteRemote = true;
    public bool AllowedExecuteRemote
    {
        get => _allowedExecuteRemote;
        set => SetField(ref _allowedExecuteRemote, value);
    } // dynamically evaluated for high-risk tweaks

    public ObservableCollection<string> Devices { get; } = new();
    public ObservableCollection<TweakDefinition> Tweaks { get; } = new();

    private string? _selectedDevice;
    public string? SelectedDevice
    {
        get => _selectedDevice;
        set => SetField(ref _selectedDevice, value);
    }

    private TweakDefinition? _selectedTweak;
    public TweakDefinition? SelectedTweak
    {
        get => _selectedTweak;
        set
        {
            if (SetField(ref _selectedTweak, value))
            {
                RefreshCapabilities();
            }
        }
    }

    private string _status = string.Empty;
    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public ICommand RefreshDevicesCommand { get; }
    public ICommand RefreshTweaksCommand { get; }
    public ICommand SendTweakCommand { get; }

    public RemoteManagementViewModel(GGs.Shared.Api.ApiClient api, GGs.Shared.Api.AuthService auth)
    {
        _api = api;
        _auth = auth;
        RefreshDevicesCommand = new RelayCommand(async () => await LoadDevices());
        RefreshTweaksCommand = new RelayCommand(async () => await LoadTweaks());
        SendTweakCommand = new RelayCommand(async () => await SendTweak());
        // connect to admin hub for ACKs
        _ = ConnectHub();
    }

    public async Task LoadDevices()
    {
        Devices.Clear();
        foreach (var d in await _api.GetConnectedDevicesAsync()) Devices.Add($"{d.Name} ({d.Id})");
        Status = $"Devices: {Devices.Count}";
    }

    public async Task LoadTweaks()
    {
        Tweaks.Clear();
        foreach (var t in await _api.GetTweaksAsync()) Tweaks.Add(t);
        Status = $"Tweaks: {Tweaks.Count}";
    }

    public async Task SendTweak()
    {
        if (SelectedDevice == null || SelectedTweak == null)
        {
            Status = "Select a device and a tweak first.";
            return;
        }
        
        // dynamic evaluation in case settings/service state changed
        RefreshCapabilities();
        if (!AllowedExecuteRemote)
        {
            var (exists, running) = GetAgentState();
            if (!Services.SettingsService.DeepOptimizationEnabled)
            {
                Status = "Deep Optimization is disabled. Enable it in Settings to send high-risk tweaks remotely.";
            }
            else if (!exists)
            {
                Status = "Agent service not installed. Install it from Settings > Deep Optimization to send high-risk tweaks.";
            }
            else if (!running)
            {
                Status = "Agent service is stopped. Start it from Settings > Deep Optimization to send high-risk tweaks.";
            }
            else
            {
                Status = "Remote execution is not allowed by current policy.";
            }
            return;
        }
        
        // Risk notification/confirmation for remote execution
        if (SelectedTweak.Risk == RiskLevel.High || SelectedTweak.Risk == RiskLevel.Critical)
        {
            var res = MessageBox.Show($"This tweak is marked as {SelectedTweak.Risk}. Send remotely to {SelectedDevice}?", "Remote Risk Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res != MessageBoxResult.Yes) { Status = "Remote execution cancelled."; return; }
        }
        
        var success = await _api.ExecuteRemoteTweakAsync(SelectedDevice, SelectedTweak.Id.ToString());
        if (success)
        {
            var correlationId = Guid.NewGuid().ToString();
            lock (_pending) _pending.Add(correlationId);
            Status = $"Delivered '{SelectedTweak.Name}' to {SelectedDevice} (corr={correlationId}). Waiting for execution...";
        }
        else
        {
            Status = $"Sent '{SelectedTweak.Name}' to {SelectedDevice}.";
        }
    }

    private void RefreshCapabilities()
    {
        try
        {
            var t = SelectedTweak;
            if (t == null)
            {
                AllowedExecuteRemote = true; return;
            }
            // Default allow
            var allow = true;
            // Gate high-risk tweaks behind Deep Optimization + running Agent service
            if (t.Risk == RiskLevel.High || t.Risk == RiskLevel.Critical)
            {
                var deep = Services.SettingsService.DeepOptimizationEnabled;
                var (_, running) = GetAgentState();
                allow = deep && running;
            }
            AllowedExecuteRemote = allow;
        }
        catch { AllowedExecuteRemote = true; }
    }

    private static (bool exists, bool running) GetAgentState()
    {
        try
        {
            bool exists, running;
            var ok = Services.AgentServiceHelper.TryGetStatus(out exists, out running);
            return ok ? (exists, running) : (false, false);
        }
        catch { return (false, false); }
    }

    private async Task ConnectHub()
    {
        try
        {
            await AdminHubClient.Instance.EnsureConnectedAsync();
            AdminHubClient.Instance.AuditAdded += (logData, correlationId) =>
            {
                bool ours;
                lock (_pending) ours = _pending.Remove(correlationId);
                if (ours)
                {
                    Status = $"Remote execution completed (corr={correlationId}).";
                    try { NotificationCenter.Add(NotificationType.Tweak, $"Remote execution completed", navigateTab: "Analytics", showToast: true); } catch { }
                }
            };
        }
        catch { }
    }
}

