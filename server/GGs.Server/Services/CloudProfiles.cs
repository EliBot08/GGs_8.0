using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GGs.Shared.CloudProfiles;
using GGs.Shared.Licensing;

namespace GGs.Server.Services;

public interface ICloudProfileStore
{
    IEnumerable<SignedCloudProfile> GetAll();
    SignedCloudProfile? Get(string id);
    void Upsert(SignedCloudProfile profile);
}

public sealed class FileCloudProfileStore : ICloudProfileStore
{
    private readonly string _indexPath;
    private readonly ConcurrentDictionary<string, SignedCloudProfile> _index = new(StringComparer.OrdinalIgnoreCase);

    public FileCloudProfileStore(IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data", "profiles");
        Directory.CreateDirectory(dataDir);
        _indexPath = Path.Combine(dataDir, "index.json");
        Load();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_indexPath))
            {
                var json = File.ReadAllText(_indexPath);
                var arr = JsonSerializer.Deserialize<List<SignedCloudProfile>>(json) ?? new();
                foreach (var p in arr) _index[p.Payload.Id] = p;
            }
        }
        catch { }
    }

    private void Save()
    {
        try
        {
            var list = _index.Values.OrderByDescending(p => p.Payload.UpdatedUtc).ToList();
            var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
            Directory.CreateDirectory(Path.GetDirectoryName(_indexPath)!);
            File.WriteAllText(_indexPath, json, Encoding.UTF8);
        }
        catch { }
    }

    public IEnumerable<SignedCloudProfile> GetAll() => _index.Values;

    public SignedCloudProfile? Get(string id)
    {
        return _index.TryGetValue(id, out var p) ? p : null;
    }

    public void Upsert(SignedCloudProfile profile)
    {
        _index[profile.Payload.Id] = profile;
        Save();
    }
}

public sealed class MarketplaceKeyService
{
    public string PrivateKeyPem { get; }
    public string PublicKeyPem { get; }

    public MarketplaceKeyService(IWebHostEnvironment env, IConfiguration cfg)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data", "profiles");
        Directory.CreateDirectory(dataDir);
        var privPath = Path.Combine(dataDir, "market_private.pem");
        var pubPath = Path.Combine(dataDir, "market_public.pem");

        var privCfg = cfg["Marketplace:PrivateKeyPem"];
        var pubCfg = cfg["Marketplace:PublicKeyPem"];

        if (!string.IsNullOrWhiteSpace(privCfg) && !string.IsNullOrWhiteSpace(pubCfg))
        {
            PrivateKeyPem = privCfg; PublicKeyPem = pubCfg; return;
        }

        if (!File.Exists(privPath) || !File.Exists(pubPath))
        {
            var (priv, pub) = RsaLicenseService.CreateRsaKeyPair();
            File.WriteAllText(privPath, priv, Encoding.UTF8);
            File.WriteAllText(pubPath, pub, Encoding.UTF8);
        }
        PrivateKeyPem = File.ReadAllText(privPath);
        PublicKeyPem = File.ReadAllText(pubPath);
    }
}

public sealed class CloudProfileManager
{
    private readonly ICloudProfileStore _store;
    private readonly MarketplaceKeyService _keys;

    public CloudProfileManager(ICloudProfileStore store, MarketplaceKeyService keys)
    {
        _store = store;
        _keys = keys;
    }

    public SignedCloudProfile Sign(CloudProfilePayload payload)
    {
        var hash = GetPayloadHash(payload);
        payload.ContentHash = hash;
        var (sig, key) = RsaLicenseService.SignPayload(payload, _keys.PrivateKeyPem);
        return new SignedCloudProfile(payload, sig, key);
    }

    public bool Verify(SignedCloudProfile profile)
    {
        var hash = GetPayloadHash(profile.Payload);
        if (hash != profile.Payload.ContentHash) return false;
        return RsaLicenseService.VerifyPayload(profile.Payload, profile.Signature, _keys.PublicKeyPem);
    }

    private static string GetPayloadHash(CloudProfilePayload payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hashBytes);
    }
}

