using System;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using System.Windows;
using GGs.Desktop.Services;

namespace GGs.Desktop.Views;

public partial class OnboardingChecklist : System.Windows.Controls.UserControl
{
    private readonly LicenseService _license;
    private readonly EliBotService _eli;
    private readonly string _baseUrl;
    private readonly GGs.Shared.Http.HttpClientSecurityOptions _httpSec;

    public OnboardingChecklist()
    {
        try { InitializeComponent(); }
        catch (Exception ex)
        {
            try { AppLogger.LogError("OnboardingChecklist InitializeComponent failed", ex); } catch { }
        }
        _license = new LicenseService();
        var cfg = new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddJsonFile("appsettings.json", optional: true).AddEnvironmentVariables().Build();
        _baseUrl = cfg["Server:BaseUrl"] ?? "https://localhost:5001";
        _httpSec = BuildSecurityOptions(cfg);
        try
        {
            var http = GGs.Shared.Http.SecureHttpClientFactory.GetOrCreate(_baseUrl, _httpSec, userAgent: "GGs.Desktop");
            var auth = new GGs.Shared.Api.AuthService(http);
            var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => { });
            var logger = loggerFactory.CreateLogger("EliBotService");
            _eli = new EliBotService(http, auth, Microsoft.Extensions.Logging.Abstractions.NullLogger<EliBotService>.Instance);
        }
        catch
        {
            _eli = new EliBotService(new HttpClient(), new GGs.Shared.Api.AuthService(new HttpClient()), Microsoft.Extensions.Logging.Abstractions.NullLogger<EliBotService>.Instance);
        }
        RefreshState();
    }

    private void RefreshState()
    {
        var state = FirstRunService.Load();
        ServerCheck.IsChecked = state.ConnectedToServer;
        LicenseCheck.IsChecked = state.LicenseActivated || _license.CurrentPayload != null;
        BaselineCheck.IsChecked = state.BaselineApplied;
    }

    private async void CheckServer_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var http = GGs.Shared.Http.SecureHttpClientFactory.GetOrCreate(_baseUrl, _httpSec, userAgent: "GGs.Desktop");
            http.Timeout = TimeSpan.FromSeconds(2);
            var resp = await http.GetAsync("/");
            var ok = resp != null;
            var s = FirstRunService.Load(); s.ConnectedToServer = ok; FirstRunService.Save(s);
            StatusText.Text = ok ? "Server reachable." : "Server not reachable.";
            RefreshState();
        }
        catch { StatusText.Text = "Server not reachable."; }
    }

    private void OpenActivation_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var w = new LicenseWindow(new LicenseService());
            Window.GetWindow(this)?.Hide();
            w.Show();
        }
        catch { }
    }

    private async void RunBaseline_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var bundle = _eli.GetOptimizationBundle("baseline").ToList();
            var exec = new TweakExecutionService();
            int okCount = 0;
            foreach (var t in bundle)
            {
                var log = await exec.ExecuteTweakAsync(t);
                if (log?.Success == true) okCount++;
            }
            var s = FirstRunService.Load(); s.BaselineApplied = okCount == bundle.Count; FirstRunService.Save(s);
            StatusText.Text = okCount == bundle.Count ? "Baseline applied." : $"Baseline applied partially ({okCount}/{bundle.Count}).";
            RefreshState();
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
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
}

