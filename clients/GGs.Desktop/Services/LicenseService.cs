using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using GGs.Shared.Licensing;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using GGs.Shared.Http;

namespace GGs.Desktop.Services;

public sealed class LicenseService
{
    private static readonly ActivitySource _activity = new("GGs.Desktop.License");
    private readonly string _storagePath;
    private readonly string _serverBaseUrl;
    private readonly string _publicKeyPem;
    private readonly string _metadataPath;
    private readonly HttpClient _http;

    private const string EmbeddedPublicKeyPem = "-----BEGIN PUBLIC KEY-----\nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAsmES+kz7bcyKPavKKzPX7X3JDSrS2/hz\nPjMTcKbxkZKJh9X6eefyKHzJ5MjpizWn9QxWjH+EeJywUEtcroNUFUJp5sFcfeoDeqd0s687m4fH\naPVkLUs18+WphEnonPMAXL7awPf9KiensKrzYffcgfGv2+DdtduVLAjfLyc/w8kDiU/0fRsl6Kuc\nvxVzG5Hw3oLLFXk2UlxlQSzGxffd1ENr43iGq6u7AZaQmrhu4YiyoZB21ajPW5aMQreMtVVN7wqA\n8g9QmfSB/ytMd+eeAArkxQBC9DgFeIn7A6YPtrji5k0o/xGASQhhkVg4fsiPMYiI+BUio8hZTOf4\nlkYVcQIDAQAB\n-----END PUBLIC KEY-----\n";

    // Cross-process IO coordination for license files
    private static readonly Mutex _licenseIoMutex = new Mutex(false, "GGs.License.IO");

    public LicenseService()
    {
        var cfg = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        _serverBaseUrl = cfg["Server:BaseUrl"] ?? "https://localhost:5001";
        _publicKeyPem = cfg["License:PublicKeyPem"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_publicKeyPem))
        {
            _publicKeyPem = EmbeddedPublicKeyPem;
        }
        // Configure shared HTTP client with security options
        var sec = BuildSecurityOptions(cfg);
        _http = GGs.Shared.Http.SecureHttpClientFactory.GetOrCreate(_serverBaseUrl, sec, userAgent: "GGs.Desktop");

        // Determine storage path with test-friendly overrides
        var explicitPath = Environment.GetEnvironmentVariable("GGS_LICENSE_PATH");
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            _storagePath = explicitPath!;
        }
        else
        {
            var baseDirOverride = Environment.GetEnvironmentVariable("GGS_DATA_DIR");
            var baseDir = !string.IsNullOrWhiteSpace(baseDirOverride)
                ? baseDirOverride!
                : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(baseDir, "GGs");
            _storagePath = Path.Combine(dir, "license.bin");
        }

        _metadataPath = Path.Combine(Path.GetDirectoryName(_storagePath)!, "license.meta.json");
    }

    public bool TryLoadAndValidate(out SignedLicense? license, out string? message)
    {
        using var activity = _activity.StartActivity("license.try_load_validate", ActivityKind.Internal);
        license = null; message = null;
        if (!File.Exists(_storagePath)) return false;
        try
        {
            if (!TryLoadRaw(out var raw)) { activity?.SetTag("license.loaded", false); return false; }
            license = raw;
            if (license == null) { activity?.SetTag("license.loaded", false); return false; }
            activity?.SetTag("license.loaded", true);
            var deviceId = GGs.Shared.Platform.DeviceIdHelper.GetStableDeviceId();

            // Special-case: accept locally persisted demo licenses for offline access
            if (license.Signature == "DEMO" && license.KeyFingerprint == "DEMO" &&
                license.Payload.IsAdminKey && license.Payload.AllowOfflineValidation &&
                RsaLicenseService.IsDeviceMatch(license.Payload, deviceId))
            {
                return true;
            }

            // Try online validation quickly; fall back to offline if allowed.
            try
            {
                var resp = _http.PostAsJsonAsync("api/licenses/validate", new { License = license, CurrentDeviceBinding = deviceId }).Result;
                if (resp.IsSuccessStatusCode)
                {
                    var result = resp.Content.ReadFromJsonAsync<GGs.Shared.Api.LicenseValidateResponse>().Result;
                    if (result?.IsValid == true) { activity?.SetTag("license.valid", true); activity?.SetTag("license.online", true); return true; }
                    message = result?.Message;
                    return false;
                }
            }
            catch { /* ignore and fall back */ }
            // Offline path
            if (!license.Payload.AllowOfflineValidation)
            {
                activity?.SetTag("license.offline_allowed", false);
                message = "Online validation required.";
                return false;
            }
            activity?.SetTag("license.offline_allowed", true);
            if (!string.IsNullOrWhiteSpace(_publicKeyPem) && RsaLicenseService.Verify(license!, _publicKeyPem))
            {
                if (license!.Payload.IsAdminKey) { activity?.SetTag("license.valid", true); activity?.SetTag("license.offline", true); return true; }
                if (RsaLicenseService.IsExpired(license.Payload, DateTime.UtcNow)) { message = "Expired"; return false; }
                if (!RsaLicenseService.IsDeviceMatch(license.Payload, deviceId)) { message = "Device mismatch"; return false; }
                return true;
            }
        }
        catch { }
        return false;
    }

    public async Task<bool> ValidateAndSaveAsync(SignedLicense license)
    {
        var (ok, _, _) = await ValidateAndSaveDetailedAsync(license);
        return ok;
    }

    public async Task<(bool ok, string? message, bool offlineUsed)> ValidateAndSaveDetailedAsync(SignedLicense license, CancellationToken ct = default)
    {
        using var activity = _activity.StartActivity("license.validate_save", ActivityKind.Internal);
        try
        {
            var deviceId = GGs.Shared.Platform.DeviceIdHelper.GetStableDeviceId();
            // Try online first with retries
            var online = await PostValidateWithRetryAsync(license, deviceId, retries: 3, perAttemptTimeoutMs: 3000, ct);
            if (online.success)
            {
                activity?.SetTag("license.online", true);
                if (!online.result!.IsValid)
                {
                    activity?.SetTag("license.valid", false);
                    TryUpdateMetadata(m =>
                    {
                        m.LastOnlineCheckUtc = DateTime.UtcNow;
                        m.RevocationStatus = string.IsNullOrWhiteSpace(online.result.Message) ? "Invalid" : online.result.Message;
                        m.NextRevalidationUtc = DateTime.UtcNow.AddMinutes(15);
                    });
                    return (false, online.result.Message ?? "License invalid.", false);
                }
                activity?.SetTag("license.valid", true);
                SaveEncrypted(license);
                TryUpdateMetadata(m =>
                {
                    m.LastOnlineCheckUtc = DateTime.UtcNow;
                    m.RevocationStatus = "Valid";
                    // Jittered backoff between 5-7 hours
                    var jitter = new Random().Next(5 * 60, 7 * 60);
                    m.NextRevalidationUtc = DateTime.UtcNow.AddMinutes(jitter);
                });
                return (true, online.result.Message ?? "License validated.", false);
            }

            // If server unreachable, optionally allow offline validation
            if (!license.Payload.AllowOfflineValidation)
                return (false, "Online validation required for this license.", false);

            if (!string.IsNullOrWhiteSpace(_publicKeyPem) && RsaLicenseService.Verify(license, _publicKeyPem))
            {
                var now = DateTime.UtcNow;
                if (!license.Payload.IsAdminKey && RsaLicenseService.IsExpired(license.Payload, now))
                    return (false, "License expired.", true);
                if (!RsaLicenseService.IsDeviceMatch(license.Payload, deviceId))
                    return (false, "This license is bound to a different device.", true);
                activity?.SetTag("license.offline", true);
                activity?.SetTag("license.valid", true);
                SaveEncrypted(license);
                return (true, "Validated offline.", true);
            }
            activity?.SetTag("license.offline", true);
            activity?.SetTag("license.valid", false);
            return (false, "Offline validation failed.", true);
        }
        catch (Exception ex)
        {
            try { _activity?.StartActivity("license.validate_save.error"); } catch { }
            return (false, ex.Message, false);
        }
    }

    private void SaveEncrypted(SignedLicense license)
    {
        var json = JsonSerializer.Serialize(license);
        var bytes = Encoding.UTF8.GetBytes(json);
        var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        Directory.CreateDirectory(Path.GetDirectoryName(_storagePath)!);

        // Coordinate cross-process writes via named mutex
        for (int attempt = 0; attempt < 3; attempt++)
        {
            bool locked = false;
            try
            {
                locked = _licenseIoMutex.WaitOne(TimeSpan.FromMilliseconds(500));
                using var fs = new FileStream(_storagePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                fs.Write(protectedBytes, 0, protectedBytes.Length);
                fs.Flush(true);
                // Update metadata after successful write
                TryUpdateMetadata(m =>
                {
                    m.LastValidationUtc = DateTime.UtcNow;
                    m.KeyFingerprint = license.KeyFingerprint;
                    m.DeviceId = license.Payload.DeviceBindingId;
                });
                return;
            }
            catch (IOException) when (attempt < 2)
            {
                Thread.Sleep(50 * (attempt + 1));
            }
            finally
            {
                if (locked)
                {
                    try { _licenseIoMutex.ReleaseMutex(); } catch { }
                }
            }
        }
        // Last attempt without swallow but still guarded
        bool finalLocked = false;
        try
        {
            finalLocked = _licenseIoMutex.WaitOne(TimeSpan.FromMilliseconds(500));
            using var fs = new FileStream(_storagePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            fs.Write(protectedBytes, 0, protectedBytes.Length);
            fs.Flush(true);
        }
        finally { if (finalLocked) { try { _licenseIoMutex.ReleaseMutex(); } catch { } } }
    }

    private async Task<(bool success, GGs.Shared.Api.LicenseValidateResponse? result)> PostValidateWithRetryAsync(SignedLicense license, string deviceId, int retries, int perAttemptTimeoutMs, CancellationToken ct)
    {
        var resilient = _http.WithResilience(Math.Max(1, retries), Math.Max(100, perAttemptTimeoutMs / Math.Max(1, retries)));
        for (int attempt = 0; attempt < retries; attempt++)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, "api/licenses/validate")
                {
                    Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new { License = license, CurrentDeviceBinding = deviceId }), Encoding.UTF8, "application/json")
                };
                var resp = await resilient.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode) { if (attempt + 1 >= retries) break; else continue; }
                var result = await resp.Content.ReadFromJsonAsync<GGs.Shared.Api.LicenseValidateResponse>(cancellationToken: ct);
                return (true, result);
            }
            catch (OperationCanceledException) { throw; }
            catch { await Task.Delay(250 * (attempt + 1), ct); }
        }
        return (false, null);
    }

    public async Task<(bool ok, string? message)> ValidateAndSaveFromTextAsync(string input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input)) return (false, "Please enter a license.");
        input = input.Trim();
        // Try JSON first
        if (input.StartsWith("{"))
        {
            try
            {
                var lic = JsonSerializer.Deserialize<SignedLicense>(input);
                if (lic == null) return (false, "Could not parse license JSON.");
                var res = await ValidateAndSaveDetailedAsync(lic, ct);
                return (res.ok, res.message);
            }
            catch (Exception ex)
            {
                return (false, $"Invalid license JSON: {ex.Message}");
            }
        }
        // Try demo simple key (16 alphanumeric)
        var clean = new string(input.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (clean.Length == 16)
        {
            var deviceId = GGs.Shared.Platform.DeviceIdHelper.GetStableDeviceId();
            var demo = new SignedLicense
            {
                Payload = new LicensePayload
                {
                    LicenseId = Guid.NewGuid().ToString(),
                    UserId = $"demo:{clean}",
                    Tier = GGs.Shared.Enums.LicenseTier.Admin,
                    IssuedUtc = DateTime.UtcNow,
                    ExpiresUtc = null,
                    IsAdminKey = true,
                    DeviceBindingId = deviceId,
                    AllowOfflineValidation = true,
                    Notes = "Demo simple key"
                },
                Signature = "DEMO",
                KeyFingerprint = "DEMO"
            };
            SaveEncrypted(demo);
            TryUpdateMetadata(m =>
            {
                m.LastValidationUtc = DateTime.UtcNow;
                m.KeyFingerprint = demo.KeyFingerprint;
                m.DeviceId = deviceId;
                m.RevocationStatus = "Valid";
                m.NextRevalidationUtc = DateTime.UtcNow.AddHours(1);
            });
            return (true, "Demo license accepted.");
        }
        return (false, "Unsupported license format. Paste JSON license or a 16-character key.");
    }

    public LicensePayload? CurrentPayload
    {
        get
        {
            if (!TryLoadAndValidate(out var lic, out var _)) return null;
            return lic!.Payload;
        }
    }

    // New: Load raw signed license without validation (used by diagnostics/revalidation)
    public bool TryLoadRaw(out SignedLicense? license)
    {
        license = null;
        try
        {
            if (!File.Exists(_storagePath)) return false;
            byte[] blob;
            // Read with retries and optional IO mutex to avoid partial reads
            for (int attempt = 0; attempt < 3; attempt++)
            {
                bool locked = false;
                try
                {
                    locked = _licenseIoMutex.WaitOne(TimeSpan.FromMilliseconds(250));
                    using var fs = new FileStream(_storagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var ms = new MemoryStream();
                    fs.CopyTo(ms);
                    blob = ms.ToArray();
                }
                finally { try { if (locked) _licenseIoMutex.ReleaseMutex(); } catch { } }

                try
                {
                    var json = Encoding.UTF8.GetString(ProtectedData.Unprotect(blob, null, DataProtectionScope.CurrentUser));
                    license = JsonSerializer.Deserialize<SignedLicense>(json);
                    if (license != null) return true;
                }
                catch
                {
                    // If unprotect/parse failed (possibly mid-write), backoff and retry
                    Thread.Sleep(50 * (attempt + 1));
                }
            }
            return false;
        }
        catch { return false; }
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

    // New: Metadata helpers
    private LicenseMetadata LoadMetadata()
    {
        try
        {
            if (!File.Exists(_metadataPath)) return new LicenseMetadata();
            var json = File.ReadAllText(_metadataPath);
            return JsonSerializer.Deserialize<LicenseMetadata>(json) ?? new LicenseMetadata();
        }
        catch { return new LicenseMetadata(); }
    }

    private void SaveMetadata(LicenseMetadata meta)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_metadataPath)!);
            var json = JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_metadataPath, json, Encoding.UTF8);
        }
        catch { }
    }

    private void TryUpdateMetadata(Action<LicenseMetadata> update)
    {
        try
        {
            var meta = LoadMetadata();
            update(meta);
            SaveMetadata(meta);
        }
        catch { }
    }

    // Expose metadata for diagnostics UI
    public LicenseMetadata GetMetadata()
    {
        return LoadMetadata();
    }
}
