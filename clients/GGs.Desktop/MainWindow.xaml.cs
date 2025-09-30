using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Windows;
using GGs.Desktop.Services;
using GGs.Shared.Api;
using GGs.Shared.Enums;
using GGs.Shared.Tweaks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IdentityModel.Tokens.Jwt;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace GGs.Desktop;

public partial class MainWindow : Window
{
    private readonly LicenseService _licenseService;
    private readonly GGs.Shared.Api.ApiClient _api;
    private readonly EliBotService _eli;

    private string? _jwt;
    private bool _isAdmin;
    private bool _isManager;
    private bool _isSupport;

    private DispatcherTimer? _netTimer;
    private bool _isOffline;
    private string _baseUrl = string.Empty;
    private GGs.Shared.Http.HttpClientSecurityOptions? _httpSec;

    public MainWindow(LicenseService licenseService)
    {
        InitializeComponent();
        _licenseService = licenseService;
        var cfg = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: true).AddEnvironmentVariables().Build();
        _baseUrl = cfg["Server:BaseUrl"] ?? "https://localhost:5001";
        _httpSec = BuildSecurityOptions(cfg);
        _api = new ApiClient(GGs.Shared.Http.SecureHttpClientFactory.GetOrCreate(_baseUrl, _httpSec, userAgent: "GGs.Desktop"));
        
        // Initialize EliBotService with required dependencies
        var httpClient = GGs.Shared.Http.SecureHttpClientFactory.GetOrCreate(_baseUrl, _httpSec, userAgent: "GGs.Desktop");
        var authService = new GGs.Shared.Api.AuthService(httpClient);
        var logger = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole().AddDebug()).CreateLogger<EliBotService>();
        _eli = new EliBotService(httpClient, authService, logger);

        // Login for admin features
        var login = new Views.LoginWindow { Owner = this };
        if (login.ShowDialog() == true)
        {
var auth = new AuthService(GGs.Shared.Http.SecureHttpClientFactory.GetOrCreate(_baseUrl, _httpSec, userAgent: "GGs.Desktop"));
            var loginRes = auth.LoginAsync(login.UserOrEmail!, login.Password!).GetAwaiter().GetResult();
            if (loginRes.ok)
            {
                var ensured = auth.EnsureAccessTokenAsync().GetAwaiter().GetResult();
                if (ensured.ok && !string.IsNullOrWhiteSpace(ensured.token))
                {
                    _jwt = ensured.token;
                    _api.SetBearer(ensured.token);
                    ParseRoles(ensured.token);
                }
            }
        }

        var payload = _licenseService.CurrentPayload;
        TierText.Text = $"Tier: {payload?.Tier ?? LicenseTier.Basic}";
        UserText.Text = payload?.UserId ?? "";

        // Apply centralized theme (tier-based XAML themes removed)
        try { ThemeManagerService.Instance.ApplyTheme(); } catch { }

        // Setup viewmodels
var licenseVm = new ViewModels.LicenseManagementViewModel(_api, new AuthService(GGs.Shared.Http.SecureHttpClientFactory.GetOrCreate(_baseUrl, _httpSec, userAgent: "GGs.Desktop")));
        LicenseView.DataContext = licenseVm;
        _ = licenseVm.LoadLicenses();

        var tweakVm = new ViewModels.TweakManagementViewModel(_api)
        {
            AllowedEdit = _isAdmin || _isManager,
            AllowedDelete = _isAdmin || _isManager,
            AllowedExecute = _isAdmin || _isManager
        };
        TweakView.DataContext = tweakVm;
        _ = tweakVm.LoadTweaks();

        var userVm = new ViewModels.UserManagementViewModel(_api);
        UserView.DataContext = userVm;
        _ = userVm.LoadUsers();

        var analyticsVm = new ViewModels.AnalyticsViewModel(_api);
        AnalyticsView.DataContext = analyticsVm;
        _ = analyticsVm.LoadAnalytics();

        // Notifications
        NotificationsView.DataContext = new ViewModels.NotificationsViewModel();

        var remoteVm = new ViewModels.RemoteManagementViewModel(_api, new AuthService(GGs.Shared.Http.SecureHttpClientFactory.GetOrCreate(_baseUrl, _httpSec, userAgent: "GGs.Desktop")));
        RemoteView.DataContext = remoteVm;
        _ = remoteVm.LoadDevices();
        _ = remoteVm.LoadTweaks();

        // Role- and Tier-based gating
        ApplyRoleGating();
        ApplyTierGating(payload?.Tier ?? LicenseTier.Basic);

        // Onboarding & notifications
        ShowOnboarding();
        CheckLicenseExpiry(payload);

        // Connectivity monitoring for Offline Mode UX
        _netTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        _netTimer.Tick += async (_, __) => await UpdateConnectivityAsync();
        _netTimer.Start();
        _ = UpdateConnectivityAsync();
        UpdateOnboardingVisibility();
    }

    private void ParseRoles(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return;
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        var roles = jwt.Claims.Where(c => c.Type == ClaimTypes.Role || c.Type == "role").Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _isAdmin = roles.Contains("Admin");
        _isManager = roles.Contains("Manager");
        _isSupport = roles.Contains("Support");
    }

    private void ApplyRoleGating()
    {
        // Reset all to visible first; tier gating will further restrict.
        TweaksTab.Visibility = Visibility.Visible;
        LicensesTab.Visibility = Visibility.Visible;
        UsersTab.Visibility = Visibility.Visible;
        AnalyticsTab.Visibility = Visibility.Visible;
        RemoteTab.Visibility = Visibility.Visible;

        if (_isAdmin)
        {
            // Admin sees everything
            return;
        }
        if (_isManager)
        {
            // Managers: hide Users tab; keep others
            UsersTab.Visibility = Visibility.Collapsed;
            return;
        }
        if (_isSupport)
        {
            // Support: Dashboard + Analytics only
            TweaksTab.Visibility = Visibility.Collapsed;
            LicensesTab.Visibility = Visibility.Collapsed;
            UsersTab.Visibility = Visibility.Collapsed;
            RemoteTab.Visibility = Visibility.Collapsed;
            return;
        }
        // Regular users: only Dashboard
        TweaksTab.Visibility = Visibility.Collapsed;
        LicensesTab.Visibility = Visibility.Collapsed;
        UsersTab.Visibility = Visibility.Collapsed;
        AnalyticsTab.Visibility = Visibility.Collapsed;
        RemoteTab.Visibility = Visibility.Collapsed;
    }

    private void ApplyTierGating(LicenseTier tier)
    {
        if (_isAdmin)
        {
            // Do not restrict admins by tier
            return;
        }
        switch (tier)
        {
            case LicenseTier.Basic:
                AnalyticsTab.Visibility = Visibility.Collapsed;
                RemoteTab.Visibility = Visibility.Collapsed;
                break;
            case LicenseTier.Pro:
                RemoteTab.Visibility = Visibility.Collapsed;
                break;
            case LicenseTier.Enterprise:
            case LicenseTier.Admin:
            default:
                // no additional restrictions
                break;
        }
    }

    private void ShowOnboarding()
    {
        try
        {
            var state = Services.FirstRunService.Load();
            if (state.ChecklistCompleted)
            {
                Onboarding.Visibility = Visibility.Collapsed;
                return;
            }
            var msg = _eli.GetOnboardingMessage();
            MessageBox.Show(this, msg, "Welcome to GGs", MessageBoxButton.OK, MessageBoxImage.Information);
            UpdateOnboardingVisibility();
        }
        catch { }
    }

    private void CheckLicenseExpiry(GGs.Shared.Licensing.LicensePayload? payload)
    {
        if (payload?.ExpiresUtc is DateTime exp)
        {
            var days = (exp - DateTime.UtcNow).TotalDays;
            if (days <= 7)
            {
                NotificationText.Text = days <= 0 ? "License expired." : $"License expires in {Math.Max(0, (int)Math.Ceiling(days))} day(s).";
                NotificationBar.Visibility = Visibility.Visible;
            }
        }
    }

    private static GGs.Shared.Http.HttpClientSecurityOptions BuildSecurityOptions(Microsoft.Extensions.Configuration.IConfiguration cfg)
    {
        var sec = new GGs.Shared.Http.HttpClientSecurityOptions();
        if (int.TryParse(cfg["Security:Http:TimeoutSeconds"], out var t)) sec.Timeout = TimeSpan.FromSeconds(Math.Clamp(t, 1, 120));
        var mode = cfg["Security:Http:Pinning:Mode"]; if (Enum.TryParse<GGs.Shared.Http.PinningMode>(mode, true, out var pm)) sec.PinningMode = pm;
        var vals = cfg["Security:Http:Pinning:Values"]; if (!string.IsNullOrWhiteSpace(vals)) sec.PinnedValues = vals.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var hosts = cfg["Security:Http:Pinning:Hostnames"]; if (!string.IsNullOrWhiteSpace(hosts)) sec.Hostnames = hosts.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (bool.TryParse(cfg["Security:Http:ClientCertificate:Enabled"], out var cce)) sec.ClientCertificateEnabled = cce;
        sec.ClientCertFindType = cfg["Security:Http:ClientCertificate:FindType"];
        sec.ClientCertFindValue = cfg["Security:Http:ClientCertificate:FindValue"];
        sec.ClientCertStoreName = cfg["Security:Http:ClientCertificate:StoreName"] ?? "My";
        sec.ClientCertStoreLocation = cfg["Security:Http:ClientCertificate:StoreLocation"] ?? "CurrentUser";
        return sec;
    }

    private async Task UpdateConnectivityAsync()
    {
        try
        {
            var http = GGs.Shared.Http.SecureHttpClientFactory.GetOrCreate(_baseUrl, _httpSec, userAgent: "GGs.Desktop");
            http.Timeout = TimeSpan.FromMilliseconds(1500);
            var resp = await http.GetAsync("/"); // any response is considered reachable
            SetOfflineState(false);
        }
        catch
        {
            SetOfflineState(true);
        }
    }

    private void SetOfflineState(bool offline)
    {
        if (_isOffline == offline) return;
        _isOffline = offline;
        ConnectivityBar.Visibility = offline ? Visibility.Visible : Visibility.Collapsed;
        ConnectivityText.Text = offline ? "Offline mode: Some features are disabled. Changes will sync when back online." : "";
        UpdateOnboardingVisibility();

        // Adjust tabs when offline: hide Remote and Licenses (server-required)
        if (offline)
        {
            RemoteTab.Visibility = Visibility.Collapsed;
            LicensesTab.Visibility = Visibility.Collapsed;
            Services.NotificationCenter.Add(Services.NotificationType.System, "Switched to offline mode.", navigateTab: null);
        }
        else
        {
            // Re-evaluate role/tier gating to restore visibility appropriately
            ApplyRoleGating();
            ApplyTierGating(_licenseService.CurrentPayload?.Tier ?? LicenseTier.Basic);
            Services.NotificationCenter.Add(Services.NotificationType.System, "Back online.", navigateTab: "Notifications");
        }
    }

    private void FontSlider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
    {
        // Preserve font scaling using Application resource updated by ThemeManager
        try { System.Windows.Application.Current.Resources["GlobalFontSize"] = e.NewValue; } catch { }
    }

    private async void AskEliBot_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is System.Windows.Controls.Button b) { b.IsEnabled = false; b.Content = "Thinking…"; }
            var q = EliQuestion?.Text ?? string.Empty;
            var response = await _eli.AskQuestionAsync(q);
            if (EliAnswer != null) EliAnswer.Text = response.Answer;
        }
        catch (Exception ex)
        {
            if (EliAnswer != null) EliAnswer.Text = ex.Message;
        }
        finally
        {
            if (sender is System.Windows.Controls.Button b2) { b2.Content = "Ask EliBot"; b2.IsEnabled = true; }
        }
    }

    private void UpdateOnboardingVisibility()
    {
        try
        {
            var state = Services.FirstRunService.Load();
            var licActive = _licenseService.CurrentPayload != null;
            if (licActive && !state.LicenseActivated) { state.LicenseActivated = true; Services.FirstRunService.Save(state); }
            if (state.ConnectedToServer && (state.LicenseActivated || licActive) && state.BaselineApplied)
            {
                state.ChecklistCompleted = true;
                Services.FirstRunService.Save(state);
            }
            Onboarding.Visibility = state.ChecklistCompleted ? Visibility.Collapsed : Visibility.Visible;
        }
        catch { }
    }
}
