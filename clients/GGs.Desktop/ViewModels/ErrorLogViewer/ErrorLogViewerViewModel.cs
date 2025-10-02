using System;

    [ObservableProperty]
    private string pauseButtonText = "Pause Live";
using System.Diagnostics;

}
    }
    [ObservableProperty]
    private LogEntryViewModel? selectedLog;
}
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GGs.Desktop.Services.ErrorLogViewer;

namespace GGs.Desktop.ViewModels.ErrorLogViewer;

public sealed partial class ErrorLogViewerViewModel : ObservableObject, IAsyncDisposable
{
    private readonly ObservableCollection<LogEntryViewModel> _logs = new();
    private readonly ReadOnlyObservableCollection<LogEntryViewModel> _readonlyLogs;
    private readonly HashSet<long> _knownEntryIds = new();
    private readonly Dispatcher _dispatcher;
    private readonly ILogIndex _index;
    private readonly ILogIngestionService _ingestionService;
    private readonly LogParser _parser;
    private readonly SemaphoreSlim _refreshSemaphore = new(1, 1);

    private CancellationTokenSource? _filterCts;
    private bool _isInitialized;

    private const int PageSize = 500;
    private const int MaxVisibleEntries = 5000;

    [ObservableProperty]
    private string logDirectory = string.Empty;

    [ObservableProperty]
    private string filterText = string.Empty;

    [ObservableProperty]
    private bool includeInfo = true;

    [ObservableProperty]
    private bool includeWarning = true;

    [ObservableProperty]
    private bool includeError = true;

    [ObservableProperty]
    private bool includeDebug;

    [ObservableProperty]
    private bool isPaused;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private int totalCount;

    [ObservableProperty]
    private string statusText = "Ready";

    public ReadOnlyObservableCollection<LogEntryViewModel> Logs => _readonlyLogs;

    public IAsyncRelayCommand InitializeCommand { get; }
    public IAsyncRelayCommand LoadMoreCommand { get; }
    public IAsyncRelayCommand ExportCommand { get; }
    public IRelayCommand TogglePauseCommand { get; }
    public IAsyncRelayCommand ClearCommand { get; }
    public IRelayCommand OpenFolderCommand { get; }

    public ErrorLogViewerViewModel()
        : this(CreateDefaultDispatcher(), CreateDefaultIndex(out var logDir, out var parser, out var ingestion), ingestion, parser)
    {
        LogDirectory = logDir;
    }

    public ErrorLogViewerViewModel(Dispatcher dispatcher, ILogIndex index, ILogIngestionService ingestionService, LogParser parser)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _index = index ?? throw new ArgumentNullException(nameof(index));
        _ingestionService = ingestionService ?? throw new ArgumentNullException(nameof(ingestionService));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));

        _readonlyLogs = new ReadOnlyObservableCollection<LogEntryViewModel>(_logs);

        InitializeCommand = new AsyncRelayCommand(InitializeAsync, CanInitialize);
        LoadMoreCommand = new AsyncRelayCommand(LoadMoreAsync, CanLoadMore);
        ExportCommand = new AsyncRelayCommand(ExportAsync, () => _logs.Count > 0);
        TogglePauseCommand = new RelayCommand(TogglePause);
        ClearCommand = new AsyncRelayCommand(ClearAsync);
        OpenFolderCommand = new RelayCommand(OpenFolder);
    }

    public async Task ApplyFilterAsync()
    {
        if (!_isInitialized)
        {
            return;
        }

        _filterCts?.Cancel();
        _filterCts = new CancellationTokenSource();
        var token = _filterCts.Token;

        try
        {
            await Task.Delay(300, token).ConfigureAwait(false);
            await RefreshAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // expected when filter changes rapidly
        }
    }

    private async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        await _refreshSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_isInitialized)
            {
                return;
            }

            IsBusy = true;

            if (string.IsNullOrWhiteSpace(LogDirectory))
            {
                LogDirectory = ResolveLogDirectory();
            }

            Directory.CreateDirectory(LogDirectory);

            await _index.InitializeAsync(CancellationToken.None).ConfigureAwait(false);
            _ingestionService.EntriesAdded += OnEntriesAdded;
            await _ingestionService.StartAsync(CancellationToken.None).ConfigureAwait(false);

            await RefreshAsync(CancellationToken.None).ConfigureAwait(false);

            _isInitialized = true;
            InitializeCommand.NotifyCanExecuteChanged();
            StatusText = "Live log monitoring ready";
        }
        finally
        {
            IsBusy = false;
            _refreshSemaphore.Release();
        }
    }

    private bool CanInitialize() => !_isInitialized;

    private bool CanLoadMore() => !IsBusy && _logs.Count < TotalCount;

    private async Task LoadMoreAsync()
    {
        await _refreshSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_logs.Count >= TotalCount)
            {
                return;
            }

            IsBusy = true;
            var entries = await _index.QueryAsync(BuildQueryOptions(_logs.Count), CancellationToken.None).ConfigureAwait(false);
            TotalCount = await _index.CountAsync(BuildQueryOptions(0), CancellationToken.None).ConfigureAwait(false);
            AppendEntries(entries, atTop: false);
        }
        finally
        {
            IsBusy = false;
            LoadMoreCommand.NotifyCanExecuteChanged();
            _refreshSemaphore.Release();
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await _refreshSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IsBusy = true;
            var options = BuildQueryOptions(0);
            var entriesTask = _index.QueryAsync(options, cancellationToken);
            var countTask = _index.CountAsync(options, cancellationToken);
            await Task.WhenAll(entriesTask, countTask).ConfigureAwait(false);

            TotalCount = countTask.Result;

            await _dispatcher.InvokeAsync(() =>
            {
                _logs.Clear();
                _knownEntryIds.Clear();
            });

            AppendEntries(entriesTask.Result, atTop: false);
            StatusText = TotalCount > 0 ? $"{TotalCount} entries" : "No log entries";
            ExportCommand.NotifyCanExecuteChanged();
        }
        finally
        {
            IsBusy = false;
            LoadMoreCommand.NotifyCanExecuteChanged();
            _refreshSemaphore.Release();
        }
    }

    private void AppendEntries(IReadOnlyCollection<LogEntryRecord> entries, bool atTop)
    {
        if (entries.Count == 0)
        {
            return;
        }

        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.Invoke(() => AppendEntries(entries, atTop));
            return;
        }

        var mapped = entries
            .Select(CreateViewModel)
            .Where(vm => vm != null)
            .Cast<LogEntryViewModel>()
            .OrderByDescending(vm => vm.Timestamp)
            .ToList();

        if (atTop)
        {
            for (var i = mapped.Count - 1; i >= 0; i--)
            {
                var vm = mapped[i];
                if (_knownEntryIds.Add(vm.Id))
                {
                    _logs.Insert(0, vm);
                }
            }
        }
        else
        {
            foreach (var vm in mapped)
            {
                if (_knownEntryIds.Add(vm.Id))
                {
                    _logs.Add(vm);
                }
            }
        }

        TrimLogBuffer();
        StatusText = $"Showing {_logs.Count} of {TotalCount} entries";
    }

    private void TrimLogBuffer()
    {
        while (_logs.Count > MaxVisibleEntries)
        {
            var removed = _logs[^1];
            _logs.RemoveAt(_logs.Count - 1);
            _knownEntryIds.Remove(removed.Id);
        }
    }

    private static LogEntryViewModel? CreateViewModel(LogEntryRecord record)
    {
        if (record is null)
        {
            return null;
        }

        return new LogEntryViewModel
        {
            Id = record.Id,
            Timestamp = record.Timestamp,
            Level = string.IsNullOrWhiteSpace(record.Level) ? "INFO" : record.Level,
            Emoji = MapEmoji(record.Level),
            Source = string.IsNullOrWhiteSpace(record.Source) ? "[Unknown]" : record.Source,
            Message = string.IsNullOrWhiteSpace(record.Message) ? "[No message]" : record.Message,
            Raw = record.Raw ?? string.Empty,
            FilePath = string.IsNullOrWhiteSpace(record.FilePath) ? "[Unknown]" : record.FilePath,
            LineNumber = record.LineNumber
        };
    }

    private static string MapEmoji(string level) => level?.ToUpperInvariant() switch
    {
        "ERROR" => "⛔",
        "WARNING" or "WARN" => "⚠",
        "INFO" => "ℹ",
        "DEBUG" => "🐞",
        "TRACE" => "🧭",
        "OK" => "✅",
        _ => "📝"
    };

    private LogQueryOptions BuildQueryOptions(int skip, int take = PageSize)
    {
        var levels = new Collection<string>();
        if (IncludeError) levels.Add("ERROR");
        if (IncludeWarning) levels.Add("WARNING");
        if (IncludeInfo) levels.Add("INFO");
        if (IncludeDebug) levels.Add("DEBUG");

        return new LogQueryOptions
        {
            Levels = levels,
            SearchText = FilterText ?? string.Empty,
            Skip = skip,
            Take = take
        };
    }

    private async Task ExportAsync()
    {
        await _refreshSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var exportPath = Path.Combine(LogDirectory, $"logs-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
            Directory.CreateDirectory(Path.GetDirectoryName(exportPath) ?? LogDirectory);
            await using var stream = new FileStream(exportPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(stream, _logs, new JsonSerializerOptions { WriteIndented = true }).ConfigureAwait(false);
            StatusText = $"Exported {_logs.Count} entries to {exportPath}";
        }
        catch (Exception ex)
        {
            StatusText = $"Export failed: {ex.Message}";
            AppLogger.LogWarn($"Export failed: {ex.Message}");
        }
        finally
        {
            _refreshSemaphore.Release();
        }
    }

    private async Task ClearAsync()
    {
        await _refreshSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            IsBusy = true;
            await _index.ClearAsync(CancellationToken.None).ConfigureAwait(false);
            await _dispatcher.InvokeAsync(() =>
            {
                _logs.Clear();
                _knownEntryIds.Clear();
            });
            TotalCount = 0;
            StatusText = "Log index cleared";
        }
        finally
        {
            IsBusy = false;
            LoadMoreCommand.NotifyCanExecuteChanged();
            ExportCommand.NotifyCanExecuteChanged();
            _refreshSemaphore.Release();
        }
    }

    private void TogglePause()
    {
        IsPaused = !IsPaused;
    }
        else
        {
            _ = _ingestionService.ResumeAsync();
            _ = RefreshHeadAsync(PageSize);
        }
    }

    private void OpenFolder()
    {
        try
        {
            var target = string.IsNullOrWhiteSpace(LogDirectory) ? ResolveLogDirectory() : LogDirectory;
            Directory.CreateDirectory(target);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to open folder: {ex.Message}";
        }
    }

    private async void OnEntriesAdded(object? sender, LogEntriesAddedEventArgs e)
    {
        if (!_isInitialized)
        {
            return;
        }

        if (IsPaused)
        {
            StatusText = "Live updates paused";
            return;
        }

        try
        {
            await RefreshHeadAsync(Math.Max(e.Entries.Count, PageSize)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AppLogger.LogWarn($"Failed to refresh log view after new entries: {ex.Message}");
        }
    }

    private async Task RefreshHeadAsync(int takeHint)
    {
        await _refreshSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var take = Math.Clamp(takeHint, PageSize, MaxVisibleEntries);
            var entriesTask = _index.QueryAsync(BuildQueryOptions(0, take), CancellationToken.None);
            var countTask = _index.CountAsync(BuildQueryOptions(0), CancellationToken.None);
            await Task.WhenAll(entriesTask, countTask).ConfigureAwait(false);

            TotalCount = countTask.Result;
            ApplyHeadSnapshot(entriesTask.Result);
            ExportCommand.NotifyCanExecuteChanged();
        }
        finally
        {
            LoadMoreCommand.NotifyCanExecuteChanged();
            _refreshSemaphore.Release();
        }
    }

    private void ApplyHeadSnapshot(IReadOnlyList<LogEntryRecord> latest)
    {
        if (latest.Count == 0)
        {
            return;
        }

        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.Invoke(() => ApplyHeadSnapshot(latest));
            return;
        }

        var mapped = latest
            .Select(CreateViewModel)
            .Where(vm => vm != null)
            .Cast<LogEntryViewModel>()
            .OrderByDescending(vm => vm.Timestamp)
            .ToList();

        var mappedIds = mapped.Select(m => m.Id).ToHashSet();
        var tail = _logs.Where(existing => !mappedIds.Contains(existing.Id)).ToList();

        _logs.Clear();
        _knownEntryIds.Clear();

        foreach (var vm in mapped)
        {
            if (_knownEntryIds.Add(vm.Id))
            {
                _logs.Add(vm);
            }
        }

        foreach (var vm in tail)
        {
            if (_knownEntryIds.Add(vm.Id))
            {
                _logs.Add(vm);
            }
        }

        TrimLogBuffer();
        StatusText = $"Showing {_logs.Count} of {TotalCount} entries";
    }

    private static Dispatcher CreateDefaultDispatcher()
    {
        if (Application.Current?.Dispatcher is Dispatcher dispatcher)
        {
            return dispatcher;
        }

        return Dispatcher.CurrentDispatcher;
    }

    private static ILogIndex CreateDefaultIndex(out string logDir, out LogParser parser, out ILogIngestionService ingestion)
    {
        parser = new LogParser();
        logDir = ResolveLogDirectory();
        var databasePath = Path.Combine(logDir, "viewer-cache", "logs.sqlite");
        var index = new SqliteLogIndex(databasePath);
        ingestion = new LogIngestionService(logDir, index, parser);
        return index;
    }

    private static string ResolveLogDirectory()
    {
        try
        {
            var fromEnv = Environment.GetEnvironmentVariable("GGS_LOG_DIR");
            if (!string.IsNullOrWhiteSpace(fromEnv) && Directory.Exists(fromEnv))
            {
                return fromEnv;
            }
        }
        catch
        {
            // ignored - fallback below
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GGs", "logs");
    }

    partial void OnFilterTextChanged(string value)
    {
        if (_isInitialized)
        {
            _ = ApplyFilterAsync();
        }
    }

    partial void OnIncludeInfoChanged(bool value)
    {
        if (_isInitialized)
        {
            _ = ApplyFilterAsync();
        }
    }

    partial void OnIncludeWarningChanged(bool value)
    {
        if (_isInitialized)
        {
            _ = ApplyFilterAsync();
        }
    }

    partial void OnIncludeErrorChanged(bool value)
    {
        if (_isInitialized)
        {
            _ = ApplyFilterAsync();
        }
    }

    partial void OnIncludeDebugChanged(bool value)
    {
        if (_isInitialized)
        {
    partial void OnIsPausedChanged(bool value)
    {
        PauseButtonText = value ? "Resume Live" : "Pause Live";

        if (!_isInitialized)
        {
            return;
        }

        StatusText = value ? "Live updates paused" : "Live updates resumed";

        if (value)
        {
            _ = _ingestionService.PauseAsync();
        }
        else
        {
            _ = _ingestionService.ResumeAsync();
            _ = RefreshHeadAsync(PageSize);
        }
    }
            _ = ApplyFilterAsync();
        }
    }

    partial void OnIsBusyChanged(bool value)
    {
        LoadMoreCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
    }

    partial void OnTotalCountChanged(int value)
    {
        LoadMoreCommand.NotifyCanExecuteChanged();
    }

    public async ValueTask DisposeAsync()
    {
        _ingestionService.EntriesAdded -= OnEntriesAdded;
        await _ingestionService.DisposeAsync().ConfigureAwait(false);
        await _index.DisposeAsync().ConfigureAwait(false);
        _refreshSemaphore.Dispose();
        _filterCts?.Dispose();
    }
}

