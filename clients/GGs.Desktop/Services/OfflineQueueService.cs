using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

using Microsoft.Extensions.Configuration;

namespace GGs.Desktop.Services;

public sealed class OfflineQueueService
{
    private readonly string _dbPath;
    private readonly HttpClient _http;
    private readonly string _ingestUrl;
    private readonly int _maxRows;
    private readonly int _batchSize;
    private readonly Timer _timer;
    private readonly object _sync = new();

    public OfflineQueueService(HttpClient? http = null)
    {
        var baseDirOverride = Environment.GetEnvironmentVariable("GGS_DATA_DIR");
        var baseDir = !string.IsNullOrWhiteSpace(baseDirOverride)
            ? baseDirOverride!
            : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(baseDir, "GGs", "offline_queue");
        Directory.CreateDirectory(dir);
        _dbPath = Path.Combine(dir, "queue.db");

        var cfg = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        var baseUrl = cfg["Server:BaseUrl"] ?? "https://localhost:5001";
        _ingestUrl = baseUrl.TrimEnd('/') + "/api/ingest/events";
        _maxRows = int.TryParse(cfg["OfflineQueue:MaxRows"], out var m) ? Math.Max(100, m) : 10000;
        _batchSize = int.TryParse(cfg["OfflineQueue:BatchSize"], out var b) ? Math.Clamp(b, 1, 500) : 50;
        if (http is null)
        {
            var sec = BuildSecurityOptions(cfg);
            _http = GGs.Shared.Http.SecureHttpClientFactory.GetOrCreate(baseUrl, sec, userAgent: "GGs.Desktop");
        }
        else
        {
            _http = http;
            if (_http.Timeout == default) _http.Timeout = TimeSpan.FromSeconds(10);
        }

        InitDb();
        _timer = new Timer(async _ => await DispatchAsync(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
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

    private void InitDb()
    {
        using var con = new SqliteConnection($"Data Source={_dbPath}");
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"
        CREATE TABLE IF NOT EXISTS events (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            EventId TEXT UNIQUE,
            Type TEXT NOT NULL,
            Payload TEXT NOT NULL,
            CreatedUtc TEXT NOT NULL,
            AttemptCount INTEGER NOT NULL DEFAULT 0,
            NextAttemptUtc TEXT NOT NULL,
            Priority INTEGER NOT NULL DEFAULT 0,
            DedupHash TEXT
        );
        CREATE INDEX IF NOT EXISTS ix_events_nextattempt ON events(NextAttemptUtc);
        CREATE INDEX IF NOT EXISTS ix_events_dedup ON events(DedupHash);
        ";
        cmd.ExecuteNonQuery();
    }

    public async Task<bool> EnqueueAsync(string type, object payload, int priority = 0, string? dedupKey = null, CancellationToken ct = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(payload);
            var dedup = ComputeHash(type + "|" + (dedupKey ?? string.Empty) + "|" + json);
            using var con = new SqliteConnection($"Data Source={_dbPath}");
            await con.OpenAsync(ct);
            using (var check = con.CreateCommand())
            {
                check.CommandText = "SELECT 1 FROM events WHERE DedupHash=@h LIMIT 1";
                check.Parameters.AddWithValue("@h", dedup);
                var exists = await check.ExecuteScalarAsync(ct);
                if (exists != null) return true; // Duplicate; treat as success
            }
            using (var tx = con.BeginTransaction())
            {
                using var cmd = con.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"INSERT INTO events (EventId,Type,Payload,CreatedUtc,AttemptCount,NextAttemptUtc,Priority,DedupHash)
                                    VALUES (@id,@t,@p,@c,0,@n,@pr,@h);";
                var id = Guid.NewGuid().ToString("N");
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@t", type);
                cmd.Parameters.AddWithValue("@p", json);
                cmd.Parameters.AddWithValue("@c", DateTime.UtcNow.ToString("o"));
                cmd.Parameters.AddWithValue("@n", DateTime.UtcNow.ToString("o"));
                cmd.Parameters.AddWithValue("@pr", priority);
                cmd.Parameters.AddWithValue("@h", dedup);
                await cmd.ExecuteNonQueryAsync(ct);

                // prune if exceeding max rows
                using var countCmd = con.CreateCommand();
                countCmd.Transaction = tx;
                countCmd.CommandText = "SELECT COUNT(1) FROM events";
                var count = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));
                if (count > _maxRows)
                {
                    var toDelete = count - _maxRows;
                    using var del = con.CreateCommand();
                    del.Transaction = tx;
                    del.CommandText = $"DELETE FROM events WHERE Id IN (SELECT Id FROM events ORDER BY Priority ASC, Id ASC LIMIT {toDelete})";
                    await del.ExecuteNonQueryAsync(ct);
                }
                tx.Commit();
            }
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.LogWarn($"OfflineQueue enqueue failed: {ex.Message}");
            return false;
        }
    }

    public async Task DispatchAsync(CancellationToken ct = default)
    {
        try
        {
            var now = DateTime.UtcNow;
            var batch = new List<(long rowId, string eventId, string type, string payload, int attempts)>();
            using (var con = new SqliteConnection($"Data Source={_dbPath}"))
            {
                await con.OpenAsync(ct);
                using var cmd = con.CreateCommand();
                cmd.CommandText = @"SELECT Id, EventId, Type, Payload, AttemptCount FROM events WHERE NextAttemptUtc<=@now ORDER BY Priority DESC, Id ASC LIMIT @limit";
                cmd.Parameters.AddWithValue("@now", now.ToString("o"));
                cmd.Parameters.AddWithValue("@limit", _batchSize);
                using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    batch.Add((reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4)));
                }
            }
            if (batch.Count == 0) return;

            var payload = batch.ConvertAll(e => new { eventId = e.eventId, type = e.type, payload = JsonSerializer.Deserialize<JsonElement>(e.payload), createdUtc = now });
            var resp = await _http.PostAsJsonAsync(_ingestUrl, payload, ct);
            if (resp.IsSuccessStatusCode)
            {
                using var con2 = new SqliteConnection($"Data Source={_dbPath}");
                await con2.OpenAsync(ct);
                using var tx = con2.BeginTransaction();
                foreach (var row in batch)
                {
                    using var del = con2.CreateCommand();
                    del.Transaction = tx;
                    del.CommandText = "DELETE FROM events WHERE Id=@id";
                    del.Parameters.AddWithValue("@id", row.rowId);
                    await del.ExecuteNonQueryAsync(ct);
                }
                tx.Commit();
                return;
            }
            else
            {
                var status = (int)resp.StatusCode;
                bool drop = status >= 400 && status < 500 && status != 429;
                await RescheduleAsync(batch, drop, ct);
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogWarn($"OfflineQueue dispatch error: {ex.Message}");
        }
    }

    private async Task RescheduleAsync(List<(long rowId, string eventId, string type, string payload, int attempts)> batch, bool drop, CancellationToken ct)
    {
        using var con = new SqliteConnection($"Data Source={_dbPath}");
        await con.OpenAsync(ct);
        using var tx = con.BeginTransaction();
        foreach (var row in batch)
        {
            if (drop)
            {
                using var del = con.CreateCommand();
                del.Transaction = tx;
                del.CommandText = "DELETE FROM events WHERE Id=@id";
                del.Parameters.AddWithValue("@id", row.rowId);
                await del.ExecuteNonQueryAsync(ct);
            }
            else
            {
                var next = DateTime.UtcNow.Add(ComputeBackoff(row.attempts + 1));
                using var upd = con.CreateCommand();
                upd.Transaction = tx;
                upd.CommandText = "UPDATE events SET AttemptCount=AttemptCount+1, NextAttemptUtc=@n WHERE Id=@id";
                upd.Parameters.AddWithValue("@n", next.ToString("o"));
                upd.Parameters.AddWithValue("@id", row.rowId);
                await upd.ExecuteNonQueryAsync(ct);
            }
        }
        tx.Commit();
    }

    private static TimeSpan ComputeBackoff(int attempts)
    {
        var baseSec = Math.Min(60, attempts * attempts * 5); // 5s, 20s, 45s, ... capped
        var jitter = RandomNumberGenerator.GetInt32(-baseSec / 3, baseSec / 3 + 1);
        return TimeSpan.FromSeconds(Math.Max(5, baseSec + jitter));
    }

    private static string ComputeHash(string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}

