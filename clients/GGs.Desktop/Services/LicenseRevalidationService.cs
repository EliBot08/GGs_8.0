using System;
using System.Threading;
using System.Threading.Tasks;

namespace GGs.Desktop.Services;

public sealed class LicenseRevalidationService : IDisposable
{
    private static LicenseRevalidationService? _instance;
    public static LicenseRevalidationService Instance => _instance ??= new LicenseRevalidationService();

    private readonly LicenseService _licenseService = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    private LicenseRevalidationService() { }

    public void Start()
    {
        if (_loop != null) return;
        _loop = Task.Run(() => LoopAsync(_cts.Token));
        AppLogger.LogInfo("License revalidation service started");
    }

    public void Dispose()
    {
        try
        {
            _cts.Cancel();
            _loop?.Wait(TimeSpan.FromSeconds(1));
        }
        catch { }
    }

    public async Task<(bool ok, string? message)> RevalidateNowAsync(CancellationToken ct = default)
    {
        try
        {
            if (!_licenseService.TryLoadRaw(out var lic) || lic == null)
                return (false, "No license to revalidate.");
            var res = await _licenseService.ValidateAndSaveDetailedAsync(lic, ct);
            AppLogger.LogInfo($"Manual license revalidation result: ok={res.ok}, offline={res.offlineUsed}, msg={res.message}");
            return (res.ok, res.message);
        }
        catch (Exception ex)
        {
            AppLogger.LogError("Manual license revalidation failed", ex);
            return (false, ex.Message);
        }
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Decide next run
                var meta = new LicenseService().GetType() // ensure type is referenced
                    ;
                var svc = _licenseService; // shorthand
                var dueAt = DateTime.UtcNow.AddHours(6); // default
                try
                {
                    // reload metadata via helper
                    var m = GetMetadataSafe();
                    if (m?.NextRevalidationUtc != null)
                        dueAt = m.NextRevalidationUtc.Value;
                }
                catch { }

                var delay = dueAt - DateTime.UtcNow;
                if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
                await Task.Delay(delay, ct);
                if (ct.IsCancellationRequested) break;

                if (!svc.TryLoadRaw(out var lic) || lic == null)
                {
                    AppLogger.LogWarn("Revalidation skipped: no license present.");
                    // Sleep a bit to avoid tight loop
                    await Task.Delay(TimeSpan.FromMinutes(5), ct);
                    continue;
                }

                var result = await svc.ValidateAndSaveDetailedAsync(lic, ct);
                if (!result.ok)
                {
                    AppLogger.LogWarn($"Background revalidation failed: {result.message}");
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Revalidation loop error", ex);
                try { await Task.Delay(TimeSpan.FromMinutes(5), ct); } catch { }
            }
        }
    }

    private LicenseMetadata? GetMetadataSafe()
    {
        try
        {
            // Leverage LicenseService internals via reflection isn't ideal; instead, re-read file directly
            var svcField = typeof(LicenseService).GetField("_metadataPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var path = (string?)svcField?.GetValue(_licenseService);
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path)) return null;
            var json = System.IO.File.ReadAllText(path);
            return System.Text.Json.JsonSerializer.Deserialize<LicenseMetadata>(json);
        }
        catch { return null; }
    }
}

