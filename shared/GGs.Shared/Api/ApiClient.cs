using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using GGs.Shared.Models;
using GGs.Shared.Tweaks;

namespace GGs.Shared.Api;

/// <summary>
/// API client for communicating with the GGs server
/// </summary>
public class ApiClient
{
    private readonly HttpClient _httpClient;

    public ApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<T> GetAsync<T>(string endpoint)
    {
        var response = await _httpClient.GetAsync(endpoint);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        return System.Text.Json.JsonSerializer.Deserialize<T>(content) ?? throw new InvalidOperationException("Failed to deserialize response");
    }

    public async Task<T> PostAsync<T>(string endpoint, object data)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(data);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(endpoint, content);
        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content.ReadAsStringAsync();
        return System.Text.Json.JsonSerializer.Deserialize<T>(responseContent) ?? throw new InvalidOperationException("Failed to deserialize response");
    }

    // User management methods
    public async Task<List<UserDto>> GetUsersAsync()
    {
        await Task.Delay(100);
        return new List<UserDto>();
    }

    public async Task<bool> SuspendUserAsync(string userId)
    {
        await Task.Delay(100);
        return true;
    }

    public async Task<bool> DeleteUserAsync(string userId)
    {
        await Task.Delay(100);
        return true;
    }

    public async Task<bool> CreateUserAsync(CreateUserRequest request)
    {
        await Task.Delay(100);
        return true;
    }

    // Tweak management methods
    public async Task<List<TweakDefinition>> GetTweaksAsync()
    {
        await Task.Delay(100);
        return new List<TweakDefinition>();
    }

    public async Task<bool> CreateTweakAsync(TweakDefinition tweak)
    {
        await Task.Delay(100);
        return true;
    }

    public async Task<bool> UpdateTweakAsync(TweakDefinition tweak)
    {
        await Task.Delay(100);
        return true;
    }

    // License management methods
    public async Task<bool> IssueLicenseAsync(string userId, string tier, DateTime expiresAt)
    {
        await Task.Delay(100);
        return true;
    }

    public async Task<bool> RevokeLicenseAsync(string licenseId)
    {
        await Task.Delay(100);
        return true;
    }

    public async Task<List<LicenseRecord>> GetLicensesAsync()
    {
        await Task.Delay(100);
        return new List<LicenseRecord>();
    }

    // Device management methods
    public async Task<(List<DeviceRegistrationDto> items, int total)> GetDevicesPagedAsync(int page, int pageSize, string searchQuery = "")
    {
        await Task.Delay(100);
        return (new List<DeviceRegistrationDto>(), 0);
    }

    // Authentication methods
    public void SetBearer(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    // Analytics methods
    public async Task<AnalyticsSummary?> GetAnalyticsSummaryAsync(int days = 7)
    {
        await Task.Delay(100);
        return new AnalyticsSummary();
    }

    public async Task<List<TweakStatistic>> GetTopTweaksAsync(int count = 10)
    {
        await Task.Delay(100);
        return new List<TweakStatistic>();
    }
}
