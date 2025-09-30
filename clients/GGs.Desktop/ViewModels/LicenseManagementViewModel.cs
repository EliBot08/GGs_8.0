using System.Collections.ObjectModel;
using System.Windows.Input;
using GGs.Shared.Api;
using GGs.Shared.Enums;
using GGs.Shared.Licensing;

namespace GGs.Desktop.ViewModels;

public class LicenseManagementViewModel : BaseViewModel
{
    private readonly GGs.Shared.Api.ApiClient _api;
    private readonly GGs.Shared.Api.AuthService _auth;
    
    public ObservableCollection<LicenseRecord> Licenses { get; }
    public ObservableCollection<LicenseTier> AvailableTiers { get; }
    public ObservableCollection<string> LicenseDurations { get; }
    
    private string _selectedUserId = string.Empty;
    public string SelectedUserId
    {
        get => _selectedUserId;
        set => SetField(ref _selectedUserId, value);
    }
    
    private LicenseTier _selectedTier = LicenseTier.Basic;
    public LicenseTier SelectedTier
    {
        get => _selectedTier;
        set => SetField(ref _selectedTier, value);
    }
    
    private string _selectedDuration = "Permanent";
    public string SelectedDuration
    {
        get => _selectedDuration;
        set => SetField(ref _selectedDuration, value);
    }
    
    private bool _isAdminKey;
    public bool IsAdminKey
    {
        get => _isAdminKey;
        set => SetField(ref _isAdminKey, value);
    }
    
    private bool _isDeveloperMode;
    public bool IsDeveloperMode
    {
        get => _isDeveloperMode;
        set => SetField(ref _isDeveloperMode, value);
    }
    
    private bool _bindToDevice;
    public bool BindToDevice
    {
        get => _bindToDevice;
        set => SetField(ref _bindToDevice, value);
    }
    
    public ICommand GenerateLicenseCommand { get; }
    public ICommand RevokeLicenseCommand { get; }
    public ICommand RefreshCommand { get; }
    
    public LicenseManagementViewModel(GGs.Shared.Api.ApiClient api, GGs.Shared.Api.AuthService auth)
    {
        _api = api;
        _auth = auth;
        
        Licenses = new ObservableCollection<LicenseRecord>();
        AvailableTiers = new ObservableCollection<LicenseTier>
        {
            LicenseTier.Basic,
            LicenseTier.Pro,
            LicenseTier.Enterprise,
            LicenseTier.Admin
        };
        
        LicenseDurations = new ObservableCollection<string>
        {
            "One-time",
            "7 days",
            "14 days",
            "30 days",
            "Permanent"
        };
        
        GenerateLicenseCommand = new RelayCommand(async () => await GenerateLicense());
        RevokeLicenseCommand = new RelayCommand<LicenseRecord>(async (license) => await RevokeLicense(license));
        RefreshCommand = new RelayCommand(async () => await LoadLicenses());
    }
    
    private async Task GenerateLicense()
    {
        var expiresUtc = SelectedDuration switch
        {
            "One-time" => DateTime.UtcNow.AddMinutes(5),
            "7 days" => DateTime.UtcNow.AddDays(7),
            "14 days" => DateTime.UtcNow.AddDays(14),
            "30 days" => DateTime.UtcNow.AddDays(30),
            _ => (DateTime?)null
        };
        
        var deviceId = BindToDevice ? GGs.Shared.Platform.DeviceIdHelper.GetStableDeviceId() : null;
        
        var request = new GGs.Shared.Api.LicenseIssueRequest
        {
            UserId = SelectedUserId,
            Tier = SelectedTier,
            ExpiresUtc = expiresUtc,
            IsAdminKey = IsAdminKey,
            DeviceBindingId = deviceId,
            AllowOfflineValidation = true,
            Notes = IsDeveloperMode ? "Developer Mode License" : null
        };
        
        await _api.IssueLicenseAsync(request.UserId, request.Tier.ToString(), request.ExpiresUtc ?? DateTime.UtcNow.AddYears(1));
        await LoadLicenses();
    }
    
    private async Task RevokeLicense(LicenseRecord? license)
    {
        if (license == null) return;
        await _api.RevokeLicenseAsync(license.Id);
        await LoadLicenses();
    }
    
    public async Task LoadLicenses()
    {
        var licenses = await _api.GetLicensesAsync();
        Licenses.Clear();
        foreach (var sharedLicense in licenses)
        {
            var localLicense = new LicenseRecord
            {
                Id = sharedLicense.Id,
                UserId = sharedLicense.UserId,
                Tier = sharedLicense.Tier,
                IssuedAt = sharedLicense.IssuedAt,
                ExpiresAt = sharedLicense.ExpiresAt,
                IsActive = sharedLicense.IsActive,
                IssuedBy = sharedLicense.IssuedBy
            };
            Licenses.Add(localLicense);
        }
    }
}

public class LicenseRecord
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public GGs.Shared.Enums.LicenseTier Tier { get; set; }
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public string IssuedBy { get; set; } = string.Empty;
}
