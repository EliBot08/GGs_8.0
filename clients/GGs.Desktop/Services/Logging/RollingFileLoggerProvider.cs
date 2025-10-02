using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;

namespace GGs.Desktop.Services.Logging;

public sealed class RollingFileLoggerOptions
{
    public string FileName { get; set; } = "desktop.log";
    public long MaxFileSizeBytes { get; set; } = 8 * 1024 * 1024; // 8 MB
    public int MaxRetainedFiles { get; set; } = 5;
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;
}

public sealed class RollingFileLoggerProvider : ILoggerProvider
{
    private readonly object _sync = new();
    private readonly string _logDirectory;
    private readonly RollingFileLoggerOptions _options;
    private StreamWriter? _writer;
    private bool _disposed;

    public RollingFileLoggerProvider(string logDirectory, RollingFileLoggerOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(logDirectory))
        {
            throw new ArgumentException("Log directory must be provided", nameof(logDirectory));
        }

        _logDirectory = logDirectory;
        _options = options ?? new RollingFileLoggerOptions();

        Directory.CreateDirectory(_logDirectory);
    }

    internal LogLevel MinimumLevel => _options.MinimumLevel;

    public ILogger CreateLogger(string categoryName)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(RollingFileLoggerProvider));
        }

        return new RollingFileLogger(categoryName, this);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                _writer?.Flush();
                _writer?.Dispose();
            }
            catch
            {
                // Swallow dispose-time IO errors
            }
            finally
            {
                _writer = null;
            }
        }
    }

    internal void Write(string categoryName, LogLevel level, EventId eventId, Exception? exception, string message)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var payload = BuildPayload(categoryName, level, eventId, exception, message);

            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                var writer = EnsureWriter();
                RotateIfNeeded(writer);
                writer.WriteLine(payload);
                writer.Flush();
            }
        }
        catch
        {
            // Never let logging failures crash the application
        }
    }

    private StreamWriter EnsureWriter()
    {
        if (_writer != null)
        {
            return _writer;
        }

        var filePath = Path.Combine(_logDirectory, _options.FileName);
        var stream = new FileStream(
            filePath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.WriteThrough | FileOptions.SequentialScan);

        _writer = new StreamWriter(stream, Encoding.UTF8)
        {
            AutoFlush = false
        };

        return _writer;
    }

    private void RotateIfNeeded(StreamWriter writer)
    {
        if (writer.BaseStream is not FileStream stream)
        {
            return;
        }

        if (stream.Length < _options.MaxFileSizeBytes)
        {
            return;
        }

        try
        {
            writer.Flush();
            writer.Dispose();
            _writer = null;

            var currentPath = Path.Combine(_logDirectory, _options.FileName);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var baseName = Path.GetFileNameWithoutExtension(_options.FileName);
            var extension = Path.GetExtension(_options.FileName);
            var archiveName = $"{baseName}-{timestamp}{extension}";
            var archivePath = Path.Combine(_logDirectory, archiveName);

            if (File.Exists(archivePath))
            {
                archivePath = Path.Combine(_logDirectory, $"{baseName}-{timestamp}-{Guid.NewGuid():N}{extension}");
            }

            File.Move(currentPath, archivePath, overwrite: false);
            TrimArchives();

            EnsureWriter();
        }
        catch
        {
            // Ignore rotation errors; the current writer has already been disposed.
        }
    }

    private void TrimArchives()
    {
        try
        {
            var baseName = Path.GetFileNameWithoutExtension(_options.FileName) ?? "desktop";
            var extension = Path.GetExtension(_options.FileName);
            var pattern = $"{baseName}-*{extension}";
            var files = Directory.EnumerateFiles(_logDirectory, pattern)
                .Select(path => new FileInfo(path))
                .OrderByDescending(info => info.CreationTimeUtc)
                .ToList();

            // Skip the most recent files according to retention policy
            for (var i = _options.MaxRetainedFiles; i < files.Count; i++)
            {
                try
                {
                    files[i].Delete();
                }
                catch
                {
                    // Ignore deletion errors
                }
            }
        }
        catch
        {
            // Ignore pruning errors
        }
    }

    private static string BuildPayload(string categoryName, LogLevel level, EventId eventId, Exception? exception, string message)
    {
        var timestamp = DateTime.UtcNow.ToString("o");
        var safeCategory = string.IsNullOrWhiteSpace(categoryName) ? "General" : categoryName;
        var safeMessage = string.IsNullOrWhiteSpace(message) ? "[no message]" : message.Replace('\r', ' ').Replace('\n', ' ');
        var builder = new StringBuilder();
        builder.Append(timestamp)
               .Append(' ')
               .Append('[').Append(level).Append(']')
               .Append(' ')
               .Append(safeCategory);

        if (eventId.Id != 0)
        {
            builder.Append(" (event ").Append(eventId.Id).Append(')');
        }

        builder.Append(" - ").Append(safeMessage);

        if (exception != null)
        {
            builder.AppendLine();
            builder.Append(exception);
        }

        return builder.ToString();
    }

    private sealed class RollingFileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly RollingFileLoggerProvider _provider;

        public RollingFileLogger(string categoryName, RollingFileLoggerProvider provider)
        {
            _categoryName = string.IsNullOrWhiteSpace(categoryName) ? "General" : categoryName;
            _provider = provider;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _provider.MinimumLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel) || formatter == null)
            {
                return;
            }

            var message = formatter(state, exception);
            _provider.Write(_categoryName, logLevel, eventId, exception, message);
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
