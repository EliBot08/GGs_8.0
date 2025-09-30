using System.Collections.Generic;
using System.Threading.Tasks;

namespace GGs.Shared.SystemIntelligence;

public class CloudProfileManager
{
    public async Task<List<CommunityProfile>> GetAvailableProfilesAsync()
    {
        try
        {
            using var httpClient = new HttpClient();
            var serverUrl = Environment.GetEnvironmentVariable("GGS_SERVER_URL") ?? "https://localhost:5001";
            httpClient.BaseAddress = new Uri(serverUrl);
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            
            var response = await httpClient.GetAsync("/api/profiles/community");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return System.Text.Json.JsonSerializer.Deserialize<List<CommunityProfile>>(content) 
                    ?? new List<CommunityProfile>();
            }
            
            return new List<CommunityProfile>();
        }
        catch
        {
            return new List<CommunityProfile>();
        }
    }

    public async Task<bool> UploadProfileAsync(SystemIntelligenceProfile profile)
    {
        try
        {
            using var httpClient = new HttpClient();
            var serverUrl = Environment.GetEnvironmentVariable("GGS_SERVER_URL") ?? "https://localhost:5001";
            httpClient.BaseAddress = new Uri(serverUrl);
            httpClient.Timeout = TimeSpan.FromMinutes(5);
            
            var json = System.Text.Json.JsonSerializer.Serialize(profile);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            var response = await httpClient.PostAsync("/api/profiles/upload", content);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DownloadProfileAsync(string profileId)
    {
        try
        {
            using var httpClient = new HttpClient();
            var serverUrl = Environment.GetEnvironmentVariable("GGS_SERVER_URL") ?? "https://localhost:5001";
            httpClient.BaseAddress = new Uri(serverUrl);
            httpClient.Timeout = TimeSpan.FromMinutes(2);
            
            var response = await httpClient.GetAsync($"/api/profiles/download/{profileId}");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var profile = System.Text.Json.JsonSerializer.Deserialize<SystemIntelligenceProfile>(content);
                
                if (profile != null)
                {
                    var localPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "GGs", "Profiles", $"{profileId}.json");
                    
                    Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
                    await File.WriteAllTextAsync(localPath, content);
                    return true;
                }
            }
            
            return false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeleteProfileAsync(string profileId)
    {
        try
        {
            using var httpClient = new HttpClient();
            var serverUrl = Environment.GetEnvironmentVariable("GGS_SERVER_URL") ?? "https://localhost:5001";
            httpClient.BaseAddress = new Uri(serverUrl);
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            
            var response = await httpClient.DeleteAsync($"/api/profiles/{profileId}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

public class CommunityProfile
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public int DownloadCount { get; set; }
    public double Rating { get; set; }
    public List<string> Tags { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
