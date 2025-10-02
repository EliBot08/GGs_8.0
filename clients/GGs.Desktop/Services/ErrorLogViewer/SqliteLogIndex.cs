using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace GGs.Desktop.Services.ErrorLogViewer;

public sealed class SqliteLogIndex : ILogIndex
{
    private readonly string _databasePath;
    private readonly SemaphoreSlim _initSemaphore = new(1, 1);
    private bool _initialized;

    public SqliteLogIndex(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Database path must be provided", nameof(databasePath));
        }

        _databasePath = databasePath;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }

            var directory = Path.GetDirectoryName(_databasePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS Entries (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    TimestampUtc TEXT NOT NULL,
                    Level TEXT NOT NULL,
                    Source TEXT NOT NULL,
                    Message TEXT NOT NULL,
                    Raw TEXT NOT NULL,
                    FilePath TEXT NOT NULL,
                    LineNumber INTEGER NOT NULL DEFAULT 0
                );
                CREATE INDEX IF NOT EXISTS idx_entries_timestamp ON Entries(TimestampUtc DESC);
                CREATE INDEX IF NOT EXISTS idx_entries_level ON Entries(Level);
                CREATE INDEX IF NOT EXISTS idx_entries_source ON Entries(Source);
            ";
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            _initialized = true;
        }
        finally
        {
            _initSemaphore.Release();
        }
    }

    public async Task<IReadOnlyList<LogEntryRecord>> AddEntriesAsync(IEnumerable<LogEntryRecord> entries, CancellationToken cancellationToken)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Call InitializeAsync before adding entries.");
        }

        var inserted = new List<LogEntryRecord>();

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Entries (TimestampUtc, Level, Source, Message, Raw, FilePath, LineNumber)
            VALUES (@ts, @level, @source, @message, @raw, @filePath, @lineNumber);
        ";

        var tsParam = command.CreateParameter();
        tsParam.ParameterName = "@ts";
        command.Parameters.Add(tsParam);

        var levelParam = command.CreateParameter();
        levelParam.ParameterName = "@level";
        command.Parameters.Add(levelParam);

        var sourceParam = command.CreateParameter();
        sourceParam.ParameterName = "@source";
        command.Parameters.Add(sourceParam);

        var messageParam = command.CreateParameter();
        messageParam.ParameterName = "@message";
        command.Parameters.Add(messageParam);

        var rawParam = command.CreateParameter();
        rawParam.ParameterName = "@raw";
        command.Parameters.Add(rawParam);

        var fileParam = command.CreateParameter();
        fileParam.ParameterName = "@filePath";
        command.Parameters.Add(fileParam);

        var lineParam = command.CreateParameter();
        lineParam.ParameterName = "@lineNumber";
        command.Parameters.Add(lineParam);

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            tsParam.Value = entry.Timestamp.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
            levelParam.Value = Sanitize(entry.Level);
            sourceParam.Value = Sanitize(entry.Source);
            messageParam.Value = Sanitize(entry.Message);
            rawParam.Value = entry.Raw ?? string.Empty;
            fileParam.Value = Sanitize(entry.FilePath);
            lineParam.Value = entry.LineNumber;

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            var assignedId = connection.LastInsertRowId;
            inserted.Add(new LogEntryRecord
            {
                Id = assignedId,
                Timestamp = entry.Timestamp,
                Level = entry.Level,
                Source = entry.Source,
                Message = entry.Message,
                Raw = entry.Raw,
                FilePath = entry.FilePath,
                LineNumber = entry.LineNumber
            });
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return inserted;
    }
    public async Task<int> CountAsync(LogQueryOptions options, CancellationToken cancellationToken)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Call InitializeAsync before querying.");
        }

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();

        var builder = new StringBuilder("SELECT COUNT(*) FROM Entries WHERE 1 = 1");
        AddFilters(options, command, builder);
        command.CommandText = builder.ToString();

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(result ?? 0, CultureInfo.InvariantCulture);
    }

    public async Task<IReadOnlyList<LogEntryRecord>> QueryAsync(LogQueryOptions options, CancellationToken cancellationToken)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Call InitializeAsync before querying.");
        }

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();

        var builder = new StringBuilder("SELECT Id, TimestampUtc, Level, Source, Message, Raw, FilePath, LineNumber FROM Entries WHERE 1 = 1");
        AddFilters(options, command, builder);
        builder.Append(" ORDER BY TimestampUtc DESC LIMIT @take OFFSET @skip");

        var takeParam = command.CreateParameter();
        takeParam.ParameterName = "@take";
        takeParam.Value = Math.Max(1, options.Take);
        command.Parameters.Add(takeParam);

        var skipParam = command.CreateParameter();
        skipParam.ParameterName = "@skip";
        skipParam.Value = Math.Max(0, options.Skip);
        command.Parameters.Add(skipParam);

        command.CommandText = builder.ToString();

        var results = new List<LogEntryRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var rawTimestamp = reader.GetString(1);
            DateTime.TryParse(rawTimestamp, null, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var timestamp);

            results.Add(new LogEntryRecord
            {
                Id = reader.GetInt64(0),
                Timestamp = timestamp,
                Level = reader.GetString(2),
                Source = reader.GetString(3),
                Message = reader.GetString(4),
                Raw = reader.GetString(5),
                FilePath = reader.GetString(6),
                LineNumber = reader.GetInt32(7)
            });
        }

        return results;
    }

    public async Task ClearAsync(CancellationToken cancellationToken)
    {
        if (!_initialized)
        {
            return;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Entries";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        _initSemaphore.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static void AddFilters(LogQueryOptions options, SqliteCommand command, StringBuilder builder)
    {
        if (options.Levels is { Count: > 0 })
        {
            var filtered = options.Levels.Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            if (filtered.Length > 0)
            {
                builder.Append(" AND Level IN (");
                for (var i = 0; i < filtered.Length; i++)
                {
                    var paramName = $"@level{i}";
                    builder.Append(paramName);
                    if (i < filtered.Length - 1)
                    {
                        builder.Append(',');
                    }

                    var levelParam = command.CreateParameter();
                    levelParam.ParameterName = paramName;
                    levelParam.Value = filtered[i];
                    command.Parameters.Add(levelParam);
                }
                builder.Append(')');
            }
        }

        if (!string.IsNullOrWhiteSpace(options.SearchText))
        {
            var param = command.CreateParameter();
            param.ParameterName = "@search";
            param.Value = "%" + options.SearchText.Trim() + "%";
            command.Parameters.Add(param);
            builder.Append(" AND (Message LIKE @search OR Raw LIKE @search OR Source LIKE @search)");
        }

        if (options.FromUtc.HasValue)
        {
            var param = command.CreateParameter();
            param.ParameterName = "@from";
            param.Value = options.FromUtc.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
            command.Parameters.Add(param);
            builder.Append(" AND TimestampUtc >= @from");
        }

        if (options.ToUtc.HasValue)
        {
            var param = command.CreateParameter();
            param.ParameterName = "@to";
            param.Value = options.ToUtc.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
            command.Parameters.Add(param);
            builder.Append(" AND TimestampUtc <= @to");
        }
    }

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "[Unknown]";
        }

        return value.Length > 1024 ? value[..1024] : value;
    }
}
