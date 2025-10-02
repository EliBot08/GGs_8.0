using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace GGs.Desktop.Services.ErrorLogViewer;

public sealed class LogEntriesAddedEventArgs : EventArgs
{
    public LogEntriesAddedEventArgs(IReadOnlyList<LogEntryRecord> entries)
    {
        Entries = entries ?? Array.Empty<LogEntryRecord>();
    }

    public IReadOnlyList<LogEntryRecord> Entries { get; }
}

public interface ILogIngestionService : IAsyncDisposable
{
    event EventHandler<LogEntriesAddedEventArgs>? EntriesAdded;

    Task StartAsync(CancellationToken cancellationToken);
    Task PauseAsync();
    Task ResumeAsync();
    Task ForceRefreshAsync(CancellationToken cancellationToken);
}

public sealed class LogIngestionService : ILogIngestionService
{
    private readonly string _logRoot;
    private readonly ILogIndex _index;
    private readonly LogParser _parser;
    private readonly ConcurrentDictionary<string, long> _fileOffsets = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly Channel<string> _ingestionQueue = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _pipelineCts;
    private Task? _ingestionTask;
    private volatile bool _isPaused;

    public event EventHandler<LogEntriesAddedEventArgs>? EntriesAdded;

    public LogIngestionService(string logRoot, ILogIndex index, LogParser parser)
    {
        _logRoot = string.IsNullOrWhiteSpace(logRoot)
            ? throw new ArgumentException("Log root must be provided", nameof(logRoot))
            : logRoot;
        _index = index ?? throw new ArgumentNullException(nameof(index));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_pipelineCts != null)
            {
                return;
            }

            Directory.CreateDirectory(_logRoot);
            await _index.InitializeAsync(cancellationToken).ConfigureAwait(false);

            _pipelineCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _ingestionTask = Task.Run(() => RunIngestionLoopAsync(_pipelineCts.Token));

            EnqueueExistingFiles();
            InitializeWatcher();
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async Task PauseAsync()
    {
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            _isPaused = true;
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async Task ResumeAsync()
    {
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            _isPaused = false;
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async Task ForceRefreshAsync(CancellationToken cancellationToken)
    {
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var file in Directory.EnumerateFiles(_logRoot, "*.*", SearchOption.TopDirectoryOnly))
            {
                _fileOffsets.TryRemove(file, out _);
                await _ingestionQueue.Writer.WriteAsync(file, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            _watcher?.Dispose();
            _watcher = null;

            if (_pipelineCts != null)
            {
                try
                {
                    _pipelineCts.Cancel();
                }
                catch
                {
                    // Ignore cancellation errors
                }
            }
        }
        finally
        {
            _lifecycle.Release();
        }

        if (_ingestionTask != null)
        {
            try
            {
                await _ingestionTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // expected on shutdown
            }
            catch (Exception ex)
            {
                AppLogger.LogWarn($"Log ingestion loop terminated unexpectedly: {ex.Message}");
            }
        }

        _pipelineCts?.Dispose();
        _pipelineCts = null;
    }

    private async Task RunIngestionLoopAsync(CancellationToken cancellationToken)
    {
        var reader = _ingestionQueue.Reader;
        while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!reader.TryRead(out var path))
            {
                continue;
            }

            if (_isPaused)
            {
                // Requeue and wait briefly to avoid tight loop
                await Task.Delay(250, cancellationToken).ConfigureAwait(false);
                await _ingestionQueue.Writer.WriteAsync(path, cancellationToken).ConfigureAwait(false);
                continue;
            }

            try
            {
                var entries = await IngestFileAsync(path, cancellationToken).ConfigureAwait(false);
                if (entries.Count > 0)
                {
                    OnEntriesAdded(entries);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                AppLogger.LogWarn($"Failed to ingest '{path}': {ex.Message}");
            }
        }
    }

    private void EnqueueExistingFiles()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(_logRoot, "*.*", SearchOption.TopDirectoryOnly))
            {
                _ = _ingestionQueue.Writer.TryWrite(file);
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogWarn($"Failed to enumerate log directory '{_logRoot}': {ex.Message}");
        }
    }

    private void InitializeWatcher()
    {
        _watcher = new FileSystemWatcher(_logRoot)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite,
            Filter = "*.*"
        };

        _watcher.Created += (_, args) => _ = _ingestionQueue.Writer.TryWrite(args.FullPath);
        _watcher.Changed += (_, args) => _ = _ingestionQueue.Writer.TryWrite(args.FullPath);
        _watcher.Renamed += (_, args) => _ = _ingestionQueue.Writer.TryWrite(args.FullPath);
        _watcher.EnableRaisingEvents = true;
    }

    private async Task<IReadOnlyList<LogEntryRecord>> IngestFileAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return Array.Empty<LogEntryRecord>();
        }

        var parsedEntries = new List<LogEntryRecord>();

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);

            var offset = _fileOffsets.GetOrAdd(path, 0);
            if (offset > 0 && stream.Length >= offset)
            {
                stream.Seek(offset, SeekOrigin.Begin);
                reader.DiscardBufferedData();
            }

            var lineNumber = 0;
            while (!reader.EndOfStream)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync().ConfigureAwait(false);
                lineNumber++;
                if (line is null)
                {
                    continue;
                }

                var entry = _parser.ParseLine(path, line, lineNumber);
                parsedEntries.Add(entry);
            }

            _fileOffsets[path] = stream.Position;
        }
        catch (IOException ex)
        {
            AppLogger.LogWarn($"IO failure while reading logs from '{path}': {ex.Message}");
        }

        if (parsedEntries.Count == 0)
        {
            return Array.Empty<LogEntryRecord>();
        }

        var inserted = await _index.AddEntriesAsync(parsedEntries, cancellationToken).ConfigureAwait(false);
        return inserted;
    }
    private void OnEntriesAdded(IReadOnlyList<LogEntryRecord> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }

        try
        {
            EntriesAdded?.Invoke(this, new LogEntriesAddedEventArgs(entries));
        }
        catch (Exception ex)
        {
            AppLogger.LogWarn($"Error notifying listeners about new log entries: {ex.Message}");
        }
    }
}
