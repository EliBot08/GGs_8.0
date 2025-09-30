using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace GGs.Desktop.Services;

public sealed class AutoUpdateService
{
    private readonly HttpClient _http;
    private readonly string _feedUrl;
    private readonly string _channel;
    private readonly bool _silent;
    private readonly int _limitKb;
    private readonly string? _pubKeyPem;

public AutoUpdateService()
        : this(new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: true)
                    .AddEnvironmentVariables()
                    .Build(),
               null)
    { }

public AutoUpdateService(Microsoft.Extensions.Configuration.IConfiguration cfg, HttpClient? http)
    {
        _feedUrl = cfg["Update:FeedUrl"] ?? "https://localhost:5001/api/updates/desktop";
        _channel = cfg["Update:Channel"] ?? "stable";
        try
        {
            // Override channel with user preference if set
            var userChannel = SettingsService.UpdateChannel;
            if (!string.IsNullOrWhiteSpace(userChannel))
            {
                _channel = userChannel;
            }
        }
        catch { }
        // Silent + bandwidth limit from config with SettingsService overrides
        var silentFromCfg = bool.TryParse(cfg["Update:Silent"], out var s) && s;
        var kbFromCfg = int.TryParse(cfg["Update:BandwidthLimitKBps"], out var l) ? l : 0;
        try { _silent = SettingsService.UpdateSilent; } catch { _silent = silentFromCfg; }
        try { var kb = SettingsService.UpdateBandwidthLimitKBps; _limitKb = kb >= 0 ? kb : kbFromCfg; } catch { _limitKb = kbFromCfg; }
        _pubKeyPem = cfg["Update:PublicKeyPem"];

        if (http is null)
        {
            // Build client for the feed host
            var sec = BuildSecurityOptions(cfg);
            Uri baseUri;
            try { baseUri = new Uri(_feedUrl); }
            catch { baseUri = new Uri((cfg["Server:BaseUrl"] ?? "https://localhost:5001").TrimEnd('/') + "/"); }
            var hostBase = baseUri.GetLeftPart(UriPartial.Authority);
            _http = GGs.Shared.Http.SecureHttpClientFactory.GetOrCreate(hostBase, sec, userAgent: "GGs.Desktop");
        }
        else
        {
            _http = http;
            if (_http.Timeout == default) _http.Timeout = TimeSpan.FromSeconds(15);
        }
    }

    public async Task<UpdateInfo?> CheckForUpdatesAsync(CancellationToken ct = default)
    {
        try
        {
            var url = AppendQuery(_feedUrl, $"channel={_channel}&current={GetCurrentVersion()}");
            var info = await _http.GetFromJsonAsync<UpdateInfo>(url, ct);
            try { SettingsService.LastUpdateCheckUtc = DateTime.UtcNow; } catch { }
            if (info == null || string.IsNullOrWhiteSpace(info.Version)) return null;
            if (!IsNewer(info.Version, GetCurrentVersion())) return null;
            if (!VerifyManifest(info)) return null;
            return info;
        }
        catch (Exception ex)
        {
            AppLogger.LogWarn($"Update check failed: {ex.Message}");
            try { SettingsService.LastUpdateCheckUtc = DateTime.UtcNow; } catch { }
            return null;
        }
    }

    public async Task<(bool ok, string? message)> DownloadAndInstallAsync(UpdateInfo info, IProgress<int>? progress = null, CancellationToken ct = default, bool launchInstaller = true)
    {
        try
        {
            if (!VerifyManifest(info)) return (false, "Manifest verification failed");
            var temp = Path.Combine(Path.GetTempPath(), $"ggs_update_{info.Version}_{Guid.NewGuid():N}{GetFileExtension(info.Url)}");
            await DownloadAsync(info.Url, temp, info.Sha256, progress, ct);

            if (!launchInstaller)
            {
                return (true, temp);
            }

            // Install: choose msiexec for .msi, otherwise run exe normally
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                UseShellExecute = true,
                Verb = "runas",
            };
            if (temp.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
            {
                psi.FileName = "msiexec";
                psi.Arguments = _silent ? $"/i \"{temp}\" /passive" : $"/i \"{temp}\"";
            }
            else
            {
                psi.FileName = temp;
                psi.Arguments = _silent ? "/S" : string.Empty; // best-effort silent flag
            }
            System.Diagnostics.Process.Start(psi);
            return (true, "Installer launched");
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return (false, "User cancelled installer UAC");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
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

    private async Task DownloadAsync(string url, string dest, string? expectedSha256, IProgress<int>? progress, CancellationToken ct)
    {
        using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength ?? 0;
        await using var fs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None);
        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[81920];
        long readTotal = 0;
        using var sha = SHA256.Create();
        int read;
        while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
        {
            await fs.WriteAsync(buffer.AsMemory(0, read), ct);
            sha.TransformBlock(buffer, 0, read, null, 0);
            readTotal += read;
            if (total > 0 && progress != null)
            {
                progress.Report((int)(readTotal * 100 / total));
            }
            if (_limitKb > 0)
            {
                await Task.Delay(1000 * read / Math.Max(1, _limitKb * 1024), ct);
            }
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        var hash = Convert.ToHexString(sha.Hash!).ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(expectedSha256) && !string.Equals(hash, expectedSha256.ToLowerInvariant(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Downloaded file hash does not match expected");
        }
    }

    private bool VerifyManifest(UpdateInfo info)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(info.Signature) || string.IsNullOrWhiteSpace(_pubKeyPem)) return true; // optional
            var data = Encoding.UTF8.GetBytes($"{info.Version}|{info.Channel}|{info.Url}|{info.Sha256}");
            using var rsa = RSA.Create();
            rsa.ImportFromPem(_pubKeyPem);
            var sig = Convert.FromBase64String(info.Signature);
            return rsa.VerifyData(data, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch { return false; }
    }

    private static string AppendQuery(string url, string q)
    {
        if (url.Contains("?")) return url + "&" + q; else return url + "?" + q;
    }

    private static string GetFileExtension(string url)
    {
        try { return Path.GetExtension(new Uri(url).AbsolutePath); } catch { return ".exe"; }
    }

    private static string GetCurrentVersion()
    {
        var v = Assembly.GetEntryAssembly()?.GetName()?.Version?.ToString();
        return v ?? "0.0.0.0";
    }

    private static bool IsNewer(string candidate, string current)
    {
        try
        {
            var cv = new Version(candidate);
            var cur = new Version(current);
            return cv > cur;
        }
        catch { return false; }
    }
}

public sealed class UpdateInfo
{
    public string Version { get; set; } = "";
    public string Channel { get; set; } = "stable";
    public string Url { get; set; } = "";
    public string? Notes { get; set; }
    public string? Sha256 { get; set; }
    public string? Signature { get; set; }
}

