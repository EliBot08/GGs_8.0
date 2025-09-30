using System.IO.Compression;
using System.Text.Json;
using GGs.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace GGs.Server.Services;

public interface IAuditArchivalService
{
    Task<(int archived, string filePath)> RunOnceAsync(CancellationToken ct = default);
}

public sealed class AuditArchivalService : BackgroundService, IAuditArchivalService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<AuditArchivalService> _logger;
    private readonly IHostEnvironment _env;
    private readonly IConfiguration _config;

    public AuditArchivalService(IServiceProvider services, ILogger<AuditArchivalService> logger, IHostEnvironment env, IConfiguration config)
    {
        _services = services;
        _logger = logger;
        _env = env;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _config.GetValue("Retention:Enabled", true);
        if (!enabled)
        {
            _logger.LogInformation("Audit archival is disabled via configuration.");
            return;
        }
        // Optional immediate run on startup (useful for tests)
        var runOnStartup = _config.GetValue("Retention:RunOnStartup", true);
        if (runOnStartup)
        {
            try { await RunOnceAsync(stoppingToken); } catch (Exception ex) { _logger.LogError(ex, "Audit archival on startup failed"); }
        }
        // Schedule periodic runs
        var intervalSeconds = _config.GetValue("Retention:IntervalSeconds", 24 * 60 * 60);
        intervalSeconds = Math.Max(60, intervalSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
                if (stoppingToken.IsCancellationRequested) break;
                await RunOnceAsync(stoppingToken);
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audit archival periodic run failed");
            }
        }
    }

    public async Task<(int archived, string filePath)> RunOnceAsync(CancellationToken ct = default)
    {
        var days = Math.Max(1, _config.GetValue("Retention:AuditDays", 90));
        var cutoff = DateTime.UtcNow.AddDays(-days);
        var archived = 0;
        var baseDir = _config["Retention:ArchiveFolder"];
        if (string.IsNullOrWhiteSpace(baseDir)) baseDir = Path.Combine(_env.ContentRootPath, "archives");
        Directory.CreateDirectory(baseDir);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var file = Path.Combine(baseDir, $"audit_{stamp}.json.gz");

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Batch process to avoid huge memory usage
        const int batchSize = 500;
        await using var fs = File.Create(file);
        await using var gzip = new GZipStream(fs, CompressionLevel.Optimal);
        await using var writer = new StreamWriter(gzip);

        while (true)
        {
            var oldBatch = await db.TweakLogs.AsNoTracking()
                .Where(l => l.AppliedUtc < cutoff)
                .OrderBy(l => l.AppliedUtc)
                .Take(batchSize)
                .ToListAsync(ct);
            if (oldBatch.Count == 0) break;

            foreach (var item in oldBatch)
            {
                var json = JsonSerializer.Serialize(item);
                await writer.WriteLineAsync(json);
            }
            await writer.FlushAsync();

            // Delete by IDs to avoid tracking overhead
            var ids = oldBatch.Select(l => l.Id).ToArray();
            // parameterized raw SQL for speed and safety
            var paramList = new List<object>();
            var placeholders = new List<string>();
            for (int i = 0; i < ids.Length; i++)
            {
                var p = new Microsoft.Data.Sqlite.SqliteParameter($"@p{i}", ids[i].ToString());
                paramList.Add(p);
                placeholders.Add(p.ParameterName);
            }
            var sql = $"DELETE FROM TweakLogs WHERE Id IN ({string.Join(",", placeholders)})";
            await db.Database.ExecuteSqlRawAsync(sql, paramList.ToArray());
            archived += oldBatch.Count;
        }

        _logger.LogInformation("Archived {Count} audit logs older than {Cutoff} to {File}", archived, cutoff, file);
        return (archived, file);
    }
}

