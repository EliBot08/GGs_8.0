using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using GGs.Desktop.Services;
using GGs.Desktop.Extensions;
using GGs.Shared.CloudProfiles;
using GGs.Shared.Api;
using Microsoft.Extensions.Configuration;

namespace GGs.Desktop.ViewModels;

public sealed class CloudProfilesViewModel : INotifyPropertyChanged
{
    private readonly CloudProfileService _svc;

    public ObservableCollection<CloudProfileSummary> Items { get; } = new();

    private string? _query;
    public string? Query
    {
        get => _query;
        set { _query = value; OnPropertyChanged(); }
    }

    private string? _category;
    public string? Category
    {
        get => _category;
        set { _category = value; OnPropertyChanged(); }
    }

    private int _page = 1;
    public int Page
    {
        get => _page;
        set { _page = value; OnPropertyChanged(); }
    }

    private int _pageSize = 20;
    public int PageSize
    {
        get => _pageSize;
        set { _pageSize = value; OnPropertyChanged(); }
    }

    private int _total;
    public int Total
    {
        get => _total;
        set { _total = value; OnPropertyChanged(); }
    }

    private CloudProfileSummary? _selected;
    public CloudProfileSummary? Selected
    {
        get => _selected;
        set { _selected = value; OnPropertyChanged(); }
    }

    private string _status = string.Empty;
    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    // Downloaded details
    private SignedCloudProfile? _downloaded;
    public SignedCloudProfile? Downloaded
    {
        get => _downloaded;
        set { _downloaded = value; OnPropertyChanged(); OnPropertyChanged(nameof(DownloadedFingerprint)); OnPropertyChanged(nameof(DownloadedTrusted)); }
    }

    public string DownloadedFingerprint => Downloaded?.KeyFingerprint ?? string.Empty;
    public string DownloadedTrusted => Downloaded != null ? "Signature verified (trusted)" : string.Empty;

    private readonly System.Collections.Generic.Dictionary<string, string> _trustedIssuers = new(StringComparer.OrdinalIgnoreCase);
    public string IssuerName
    {
        get
        {
            var fp = Downloaded?.KeyFingerprint ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(fp) && _trustedIssuers.TryGetValue(fp, out var issuer))
                return $"Issuer: {issuer}";
            return string.IsNullOrWhiteSpace(fp) ? string.Empty : "Issuer: Unknown";
        }
    }

    public CloudProfilesViewModel(CloudProfileService svc)
    {
        _svc = svc;
        try
        {
            var cfg = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();
            var section = cfg.GetSection("CloudProfiles:TrustedFingerprints");
            foreach (var child in section.GetChildren())
            {
                if (!string.IsNullOrWhiteSpace(child.Key) && !string.IsNullOrWhiteSpace(child.Value))
                {
                    _trustedIssuers[child.Key] = child.Value!;
                }
            }
        }
        catch { }
    }

    public async Task LoadPageAsync(CancellationToken ct = default)
    {
        try
        {
            Status = "Loading...";
            Items.Clear();
            var page = await _svc.BrowseAsync(Page, PageSize, ct);
            Total = page.Total;
            foreach (var it in page.Items)
                Items.Add(it);
            Status = Items.Count == 0 ? "No profiles found." : $"Loaded {Items.Count} of {Total}.";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    public async Task SearchAsync(CancellationToken ct = default)
    {
        try
        {
            Status = "Searching...";
            Items.Clear();
            var req = new CloudProfileSearchRequest { Query = Query, Category = Category, Page = Page, PageSize = PageSize };
            var page = await _svc.SearchAsync(req, ct);
            Total = page.Total;
            foreach (var it in page.Items)
                Items.Add(it);
            Status = Items.Count == 0 ? "No profiles found." : $"Loaded {Items.Count} of {Total}.";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    public async Task DownloadSelectedAsync(CancellationToken ct = default)
    {
        if (Selected == null)
        {
            Status = "Select a profile first.";
            return;
        }
        try
        {
            Status = "Downloading...";
            var signed = await _svc.DownloadAsync(Selected.Id, ct);
            Downloaded = signed;
            // notify issuer label update
            OnPropertyChanged(nameof(IssuerName));
            Status = signed != null ? $"Downloaded {Selected.Name}." : "Download failed or signature invalid.";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    public bool IsAdmin
    {
        get
        {
            try { return GGs.Desktop.Services.EntitlementsService.IsAdmin; } catch { return false; }
        }
    }

    public async Task ApproveSelectedAsync(CancellationToken ct = default)
    {
        if (Selected == null)
        {
            Status = "Select a profile first.";
            return;
        }
        try
        {
            if (!IsAdmin)
            {
                Status = "Approval requires Admin.";
                return;
            }
            Status = "Approving...";
            // Obtain bearer via AuthService
            var httpClient = new HttpClient();
            var auth = new GGs.Shared.Api.AuthService(httpClient);
            var (ok, token) = await auth.EnsureAccessTokenAsync();
            if (!ok || string.IsNullOrWhiteSpace(token))
            {
                Status = "Not authenticated.";
                return;
            }
            var (approveOk, msg) = await _svc.ApproveAsync(Selected.Id, token!, ct);
            if (approveOk)
            {
                Status = "Approved.";
                // Refresh listing state
                await LoadPageAsync(ct);
            }
            else
            {
                Status = msg ?? "Approval failed.";
            }
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
