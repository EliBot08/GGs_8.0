using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using GGs.Shared.CloudProfiles;
using GGs.Shared.Licensing;

using Microsoft.Extensions.Configuration;

namespace GGs.Desktop.Services;

public class CloudProfileService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _localProfilesPath;
    private string? _userToken;

    // New API fields
    private readonly HttpClient _apiHttp;
    private readonly string _baseUrl;
    private readonly string _cacheDir;
    private readonly string _publicKeyPem;
    
    public event EventHandler<ProfileDownloadedEventArgs>? ProfileDownloaded;
    public event EventHandler<ProfileUploadedEventArgs>? ProfileUploaded;
    
    public CloudProfileService()
    {
        _httpClient = new HttpClient();
        _localProfilesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "GGs", 
            "CloudProfiles");
        Directory.CreateDirectory(_localProfilesPath);

        // Initialize real API client
        var cfg = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        _baseUrl = cfg["Server:BaseUrl"] ?? "https://localhost:5001";
        _publicKeyPem = cfg["CloudProfiles:PublicKeyPem"] ?? cfg["License:PublicKeyPem"] ?? string.Empty;
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _cacheDir = Path.Combine(baseDir, "GGs", "cloud_profiles");
        Directory.CreateDirectory(_cacheDir);
        var sec = BuildSecurityOptions(cfg);
        _apiHttp = GGs.Shared.Http.SecureHttpClientFactory.GetOrCreate(_baseUrl, sec, userAgent: "GGs.Desktop");
    }
    
    public async Task<bool> AuthenticateAsync(string username, string password)
    {
        try
        {
            // In production, this would call real authentication API
            // For demo, simulate authentication
            await Task.Delay(500);
            _userToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<List<CommunityProfile>> GetMarketplaceProfilesAsync(string? category = null)
    {
        // Simulate fetching marketplace profiles
        await Task.Delay(300);
        
        var profiles = new List<CommunityProfile>
        {
            new CommunityProfile
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Pro CS2 Settings by s1mple",
                Description = "Professional Counter-Strike 2 settings used by s1mple",
                Author = "s1mple",
                AuthorVerified = true,
                Category = "FPS",
                Game = "Counter-Strike 2",
                Downloads = 15420,
                Rating = 4.9f,
                Tags = new[] { "competitive", "fps", "esports" },
                CreatedDate = DateTime.Now.AddDays(-30),
                LastUpdated = DateTime.Now.AddDays(-2)
            },
            new CommunityProfile
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Ultimate Valorant Performance",
                Description = "Maximum FPS and lowest input lag for Valorant",
                Author = "TenZ",
                AuthorVerified = true,
                Category = "FPS",
                Game = "Valorant",
                Downloads = 12350,
                Rating = 4.8f,
                Tags = new[] { "performance", "fps", "competitive" },
                CreatedDate = DateTime.Now.AddDays(-45),
                LastUpdated = DateTime.Now.AddDays(-5)
            },
            new CommunityProfile
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Streaming Optimized Settings",
                Description = "Perfect balance for streaming and gaming",
                Author = "Shroud",
                AuthorVerified = true,
                Category = "Streaming",
                Game = "All Games",
                Downloads = 8920,
                Rating = 4.7f,
                Tags = new[] { "streaming", "obs", "balanced" },
                CreatedDate = DateTime.Now.AddDays(-60),
                LastUpdated = DateTime.Now.AddDays(-10)
            },
            new CommunityProfile
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Low-End PC Optimizer",
                Description = "Get more FPS on older hardware",
                Author = "TechGuru42",
                AuthorVerified = false,
                Category = "Performance",
                Game = "All Games",
                Downloads = 25670,
                Rating = 4.6f,
                Tags = new[] { "low-end", "fps-boost", "optimization" },
                CreatedDate = DateTime.Now.AddDays(-90),
                LastUpdated = DateTime.Now.AddDays(-7)
            },
            new CommunityProfile
            {
                Id = Guid.NewGuid().ToString(),
                Name = "RTX 4090 Ultra Settings",
                Description = "Maximum quality for high-end systems",
                Author = "NvidiaOfficial",
                AuthorVerified = true,
                Category = "Quality",
                Game = "All Games",
                Downloads = 5430,
                Rating = 4.5f,
                Tags = new[] { "rtx", "ultra", "quality" },
                CreatedDate = DateTime.Now.AddDays(-15),
                LastUpdated = DateTime.Now.AddDays(-1)
            }
        };
        
        if (!string.IsNullOrEmpty(category))
        {
            profiles = profiles.Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        
        return profiles;
    }
    
    public async Task<bool> DownloadProfileAsync(CommunityProfile profile)
    {
        try
        {
            // Simulate download
            await Task.Delay(1000);
            
            // Create profile settings
            var settings = new ProfileSettings
            {
                ProfileId = profile.Id,
                Name = profile.Name,
                Author = profile.Author,
                GameSettings = GenerateGameSettings(profile),
                SystemSettings = GenerateSystemSettings(profile)
            };
            
            // Save locally
            var fileName = Path.Combine(_localProfilesPath, $"{profile.Id}.json");
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(fileName, json);
            
            // Trigger event
            ProfileDownloaded?.Invoke(this, new ProfileDownloadedEventArgs { Profile = profile });
            
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<bool> UploadProfileAsync(string name, string description, ProfileSettings settings)
    {
        try
        {
            if (string.IsNullOrEmpty(_userToken))
                return false;
            
            // Simulate upload
            await Task.Delay(1000);
            
            var profile = new CommunityProfile
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Description = description,
                Author = GetCurrentUsername(),
                Category = DetermineCategory(settings),
                Game = settings.GameSettings?.GameName ?? "All Games",
                CreatedDate = DateTime.Now,
                LastUpdated = DateTime.Now,
                Tags = GenerateTags(settings)
            };
            
            // Save to marketplace (simulated)
            ProfileUploaded?.Invoke(this, new ProfileUploadedEventArgs { Profile = profile });
            
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    // New real API methods
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

    public async Task<CloudProfilePage> BrowseAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var res = await _apiHttp.GetFromJsonAsync<CloudProfilePage>($"api/profiles?page={page}&pageSize={pageSize}", ct)
                  ?? new CloudProfilePage { Page = page, PageSize = pageSize, Total = 0 };
        return res;
    }

    public async Task<CloudProfilePage> SearchAsync(CloudProfileSearchRequest req, CancellationToken ct = default)
    {
        var res = await _apiHttp.PostAsJsonAsync("api/profiles/search", req, ct);
        res.EnsureSuccessStatusCode();
        var page = await res.Content.ReadFromJsonAsync<CloudProfilePage>(cancellationToken: ct)
                   ?? new CloudProfilePage { Page = req.Page, PageSize = req.PageSize, Total = 0 };
        return page;
    }

    public async Task<SignedCloudProfile?> DownloadAsync(string id, CancellationToken ct = default)
    {
        var res = await _apiHttp.GetAsync($"api/profiles/{id}", ct);
        if (!res.IsSuccessStatusCode) return null;
        var signed = await res.Content.ReadFromJsonAsync<SignedCloudProfile>(cancellationToken: ct);
        if (signed == null) return null;
        if (!VerifySignature(signed)) return null;
        SaveToCache(signed);
        return signed;
    }

    public async Task<(bool ok, string? message)> UploadAsync(CloudProfilePayload payload, string bearerToken, CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "api/profiles");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var res = await _apiHttp.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            return (false, $"{res.StatusCode}: {body}");
        }
        var signed = await res.Content.ReadFromJsonAsync<SignedCloudProfile>(cancellationToken: ct);
        if (signed != null && VerifySignature(signed)) SaveToCache(signed);
        return (true, "Uploaded");
    }

    public async Task<(bool ok, string? message)> ApproveAsync(string id, string bearerToken, CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"api/profiles/{id}/approve");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
        var res = await _apiHttp.SendAsync(req, ct);
        if (res.IsSuccessStatusCode || res.StatusCode == System.Net.HttpStatusCode.NoContent)
        {
            return (true, null);
        }
        var body = await res.Content.ReadAsStringAsync(ct);
        return (false, $"{res.StatusCode}: {body}");
    }

    public IEnumerable<CloudProfileSummary> ListCachedProfiles()
    {
        foreach (var file in Directory.GetFiles(_cacheDir, "*.json"))
        {
            SignedCloudProfile? signed = null;
            try { signed = JsonSerializer.Deserialize<SignedCloudProfile>(File.ReadAllText(file)); } catch { }
            if (signed == null) continue;
            yield return new CloudProfileSummary
            {
                Id = signed.Payload.Id,
                Name = signed.Payload.Name,
                Version = signed.Payload.Version,
                Publisher = signed.Payload.Publisher,
                UpdatedUtc = signed.Payload.UpdatedUtc,
                Category = signed.Payload.Category,
                ModerationApproved = signed.Payload.ModerationApproved
            };
        }
    }

    public SignedCloudProfile? GetCachedProfile(string id)
    {
        var path = Path.Combine(_cacheDir, id + ".json");
        if (!File.Exists(path)) return null;
        try { return JsonSerializer.Deserialize<SignedCloudProfile>(File.ReadAllText(path)); } catch { return null; }
    }

    private bool VerifySignature(SignedCloudProfile p)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_publicKeyPem)) return false;
            var canonical = RsaLicenseService.CanonicalJson(p.Payload);
            using var rsa = RSA.Create();
            rsa.ImportFromPem(_publicKeyPem);
            var data = Encoding.UTF8.GetBytes(canonical);
            var sig = Convert.FromBase64String(p.Signature);
            return rsa.VerifyData(data, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch { return false; }
    }

    private void SaveToCache(SignedCloudProfile profile)
    {
        try
        {
            var path = Path.Combine(_cacheDir, profile.Payload.Id + ".json");
            if (File.Exists(path))
            {
                try
                {
                    var existing = JsonSerializer.Deserialize<SignedCloudProfile>(File.ReadAllText(path));
                    if (existing != null && existing.Payload.UpdatedUtc >= profile.Payload.UpdatedUtc) return;
                }
                catch { }
            }
            File.WriteAllText(path, JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
        }
        catch { }
    }

    // Added wrapper: download by id returning tuple expected by Admin VM
    public async Task<(bool Success, string Message)> DownloadProfileAsync(string id)
    {
        try
        {
            var signed = await DownloadAsync(id);
            if (signed == null) return (false, "Profile not found or signature invalid");
            return (true, "Downloaded");
        }
        catch (Exception ex)
        {
            return (false, $"Download failed: {ex.Message}");
        }
    }

    // Added wrapper: upload by file path returning tuple expected by Admin VM
    public async Task<(bool Success, string Message)> UploadProfileAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return (false, "File not found");
            if (string.IsNullOrEmpty(_userToken)) return (false, "Not authenticated");

            var content = await File.ReadAllTextAsync(filePath);
            var payload = new CloudProfilePayload
            {
                Name = Path.GetFileNameWithoutExtension(filePath),
                Description = $"Uploaded from {Environment.MachineName}",
                Publisher = Environment.UserName,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                Category = "general",
                Content = content,
                ContentHash = ComputeSha256Hex(content)
            };

            var (ok, message) = await UploadAsync(payload, _userToken);
            return (ok, message ?? (ok ? "Uploaded" : "Upload failed"));
        }
        catch (Exception ex)
        {
            return (false, $"Upload failed: {ex.Message}");
        }
    }

    // Added wrapper: approve by id using stored token
    public async Task<(bool Success, string Message)> ApproveProfileAsync(string id)
    {
        try
        {
            if (string.IsNullOrEmpty(_userToken)) return (false, "Not authenticated");
            var (ok, message) = await ApproveAsync(id, _userToken);
            return (ok, message ?? (ok ? "Approved" : "Approval failed"));
        }
        catch (Exception ex)
        {
            return (false, $"Approval failed: {ex.Message}");
        }
    }

    private static string ComputeSha256Hex(string input)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    public void Dispose()
    {
        try { _httpClient?.Dispose(); } catch { }
    }

    public async Task<List<CommunityProfile>> GetAvailableProfilesAsync()
    {
        try
        {
            // Simulate getting available profiles
            await Task.Delay(100);
            return new List<CommunityProfile>();
        }
        catch
        {
            return new List<CommunityProfile>();
        }
    }

    public async Task<bool> SyncProfilesAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(_userToken))
                return false;
            
            // Get local profiles
            var localFiles = Directory.GetFiles(_localProfilesPath, "*.json");
            
            // Simulate sync with cloud
            await Task.Delay(500);
            
            // In production, this would:
            // 1. Upload new/modified local profiles
            // 2. Download new/modified cloud profiles
            // 3. Handle conflicts
            
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<bool> RateProfileAsync(string profileId, int rating)
    {
        try
        {
            // Simulate rating submission
            await Task.Delay(200);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    private GameSettings GenerateGameSettings(CommunityProfile profile)
    {
        return new GameSettings
        {
            GameName = profile.Game,
            ProcessPriority = "High",
            DisableFullscreenOptimizations = false,
            UseGameMode = true,
            CustomSettings = new Dictionary<string, string>
            {
                { "MaxFPS", "300" },
                { "VSync", "Off" },
                { "AntiAliasing", profile.Category == "Quality" ? "High" : "Low" }
            }
        };
    }
    
    private SystemSettings GenerateSystemSettings(CommunityProfile profile)
    {
        return new SystemSettings
        {
            PowerPlan = profile.Category == "Performance" ? "High Performance" : "Balanced",
            DisableServices = new[] { "SysMain", "WSearch" },
            NetworkOptimizations = true,
            MemoryOptimizations = true,
            StartupPrograms = profile.Category == "Streaming" 
                ? new[] { "OBS", "StreamDeck" } 
                : Array.Empty<string>()
        };
    }
    
    private string DetermineCategory(ProfileSettings settings)
    {
        if (settings.SystemSettings?.StartupPrograms?.Contains("OBS") == true)
            return "Streaming";
        if (settings.SystemSettings?.PowerPlan == "High Performance")
            return "Performance";
        return "General";
    }
    
    private string[] GenerateTags(ProfileSettings settings)
    {
        var tags = new List<string>();
        
        if (settings.SystemSettings?.NetworkOptimizations == true)
            tags.Add("network");
        if (settings.SystemSettings?.MemoryOptimizations == true)
            tags.Add("memory");
        if (settings.GameSettings?.UseGameMode == true)
            tags.Add("game-mode");
        
        return tags.ToArray();
    }
    
    private string GetCurrentUsername()
    {
        // In production, decode from token
        return "CurrentUser";
    }
}

public class CommunityProfile
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
    public bool AuthorVerified { get; set; }
    public string Category { get; set; } = "";
    public string Game { get; set; } = "";
    public int Downloads { get; set; }
    public float Rating { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public DateTime CreatedDate { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class ProfileSettings
{
    public string ProfileId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Author { get; set; } = "";
    public GameSettings? GameSettings { get; set; }
    public SystemSettings? SystemSettings { get; set; }
}

public class GameSettings
{
    public string GameName { get; set; } = "";
    public string ProcessPriority { get; set; } = "";
    public bool DisableFullscreenOptimizations { get; set; }
    public bool UseGameMode { get; set; }
    public Dictionary<string, string> CustomSettings { get; set; } = new();
}

public class SystemSettings
{
    public string PowerPlan { get; set; } = "";
    public string[] DisableServices { get; set; } = Array.Empty<string>();
    public bool NetworkOptimizations { get; set; }
    public bool MemoryOptimizations { get; set; }
    public string[] StartupPrograms { get; set; } = Array.Empty<string>();
}

public class ProfileDownloadedEventArgs : EventArgs
{
    public CommunityProfile Profile { get; set; } = null!;
}

public class ProfileUploadedEventArgs : EventArgs
{
    public CommunityProfile Profile { get; set; } = null!;
}
