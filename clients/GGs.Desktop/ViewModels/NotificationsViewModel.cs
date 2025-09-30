using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;

namespace GGs.Desktop.ViewModels;

// Note: Using Services.NotificationItem as the backing model to match NotificationsView bindings

public class NotificationsViewModel : BaseViewModel
{

    public ICollectionView NotificationsView { get; }

    private string _filterType = "All";
    public string FilterType
    {
        get => _filterType;
        set { if (_filterType != value) { _filterType = value; NotificationsView.Refresh(); OnPropertyChanged(); } }
    }

    private bool _unreadOnly;
    public bool UnreadOnly
    {
        get => _unreadOnly;
        set { if (_unreadOnly != value) { _unreadOnly = value; NotificationsView.Refresh(); OnPropertyChanged(); } }
    }

    public IReadOnlyList<string> Types { get; } = new[] { "All", "Info", "Warning", "Error", "License", "Tweak", "System" };

    public ICommand MarkAllAsReadCommand { get; }
    public ICommand MarkSelectedAsReadCommand { get; }

    private Services.NotificationItem? _selected;
    public Services.NotificationItem? Selected
    {
        get => _selected;
        set { _selected = value; OnPropertyChanged(); }
    }

    public NotificationsViewModel()
    {
        NotificationsView = CollectionViewSource.GetDefaultView(Services.NotificationCenter.Items);
        NotificationsView.Filter = ApplyFilter;
        MarkAllAsReadCommand = new RelayCommand(() => Services.NotificationCenter.MarkAllAsRead());
        MarkSelectedAsReadCommand = new RelayCommand(() => { if (Selected != null) Services.NotificationCenter.MarkAsRead(Selected.Id); });
    }

    private bool ApplyFilter(object o)
    {
        if (o is not Services.NotificationItem n) return false;
        if (UnreadOnly && n.IsRead) return false;
        if (FilterType != "All" && !string.Equals(n.Type.ToString(), FilterType, StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }
}

