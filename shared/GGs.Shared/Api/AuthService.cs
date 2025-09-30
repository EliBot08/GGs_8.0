using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace GGs.Shared.Api;

/// <summary>
/// Authentication service for user login and token management
/// </summary>
public class AuthService
{
    private readonly HttpClient _httpClient;
    private string? _accessToken;

    public AuthService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public User? CurrentUser { get; private set; }
    public string[] CurrentRoles { get; private set; } = Array.Empty<string>();

    public async Task<(bool ok, string? token, string? error)> LoginAsync(string username, string password)
    {
        try
        {
            // Simulate login
            await Task.Delay(100);
            
            if (username == "admin" && password == "admin")
            {
                _accessToken = "mock-access-token";
                CurrentUser = new User { Id = "admin", Username = username, Email = "admin@example.com" };
                CurrentRoles = new[] { "Admin", "Manager" };
                return (true, _accessToken, null);
            }
            
            return (false, null, "Invalid credentials");
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }

    public async Task<(bool ok, string? token)> EnsureAccessTokenAsync()
    {
        await Task.Delay(10);
        return (true, _accessToken);
    }

    public async Task<string> GetValidTokenAsync()
    {
        await Task.Delay(10);
        return _accessToken ?? throw new InvalidOperationException("No valid token available");
    }
}

public class User
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
