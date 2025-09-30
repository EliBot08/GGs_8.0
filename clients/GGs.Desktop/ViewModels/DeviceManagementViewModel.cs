using System.Collections.ObjectModel;
using System.Windows.Input;
using GGs.Shared.Api;
using GGs.Shared.Models;

namespace GGs.Desktop.ViewModels;

public sealed class DeviceManagementViewModel : BaseViewModel
{
    private readonly GGs.Shared.Api.ApiClient _api;

    public ObservableCollection<DeviceRegistrationDto> Devices { get; } = new();

    private string _searchQuery = string.Empty;
    public string SearchQuery { get => _searchQuery; set => SetField(ref _searchQuery, value); }

    private int _pageSize = 25;
    public int PageSize { get => _pageSize; set { if (value < 1) value = 1; if (value > 100) value = 100; SetField(ref _pageSize, value); } }

    private int _pageIndex = 0;
    public int PageIndex { get => _pageIndex; set { if (value < 0) value = 0; SetField(ref _pageIndex, value); } }

    private int _totalCount = 0;
    public int TotalCount { get => _totalCount; private set => SetField(ref _totalCount, value); }

    private string _status = string.Empty;
    public string Status { get => _status; private set => SetField(ref _status, value); }

    public ICommand RefreshCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand NextPageCommand { get; }
    public ICommand PrevPageCommand { get; }

    public DeviceManagementViewModel(GGs.Shared.Api.ApiClient api)
    {
        _api = api;
        RefreshCommand = new RelayCommand(async () => await LoadPageAsync());
        SearchCommand = new RelayCommand(async () => { PageIndex = 0; await LoadPageAsync(); });
        NextPageCommand = new RelayCommand(async () => { if ((PageIndex + 1) * PageSize < TotalCount) { PageIndex++; await LoadPageAsync(); } });
        PrevPageCommand = new RelayCommand(async () => { if (PageIndex > 0) { PageIndex--; await LoadPageAsync(); } });
    }

    public async Task LoadPageAsync()
    {
        var (items, total) = await _api.GetDevicesPagedAsync(PageIndex, PageSize, string.IsNullOrWhiteSpace(SearchQuery) ? "" : SearchQuery);
        Devices.Clear();
        foreach (var d in items) Devices.Add(d);
        TotalCount = total;
        var from = TotalCount == 0 ? 0 : PageIndex * PageSize + 1;
        var to = Math.Min(TotalCount, (PageIndex + 1) * PageSize);
        Status = $"Showing {from}-{to} of {TotalCount}";
    }
}

