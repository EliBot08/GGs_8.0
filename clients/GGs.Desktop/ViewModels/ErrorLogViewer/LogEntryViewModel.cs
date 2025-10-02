using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GGs.Desktop.ViewModels.ErrorLogViewer;

public sealed partial class LogEntryViewModel : ObservableObject
{
    [ObservableProperty]
    private long id;

    [ObservableProperty]
    private DateTime timestamp;

    [ObservableProperty]
    private string level = "INFO";

    [ObservableProperty]
    private string emoji = "⚙";

    [ObservableProperty]
    private string source = "[Unknown]";

    [ObservableProperty]
    private string message = "[No message]";

    [ObservableProperty]
    private string raw = string.Empty;

    [ObservableProperty]
    private string filePath = "[Unknown]";

    [ObservableProperty]
    private int lineNumber;

    public string TimestampText => timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff");
    partial void OnTimestampChanged(DateTime value)
    {
        OnPropertyChanged(nameof(TimestampText));
    }
}
