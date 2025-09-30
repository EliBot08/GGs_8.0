using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace GGs.E2ETests;

public class Prompt12Tests
{
    [Fact]
    public async Task Jwks_Reachable_And_RevokeJti_BlocksToken_And_RefreshBeyondMaxAge401()
    {
        await using var factory = new WebApplicationFactory<GGs.Server.Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Development")
                .ConfigureAppConfiguration((ctx, cfg) =>
                {
                    cfg.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Auth:AccessTokenMinutes"] = "5",
                        ["Auth:RefreshTokenMinutes"] = "1"
                    });
                }));

        using var client = factory.CreateClient();

        // JWKS reachable
        var jwks = await client.GetAsync("/api/jwks");
        jwks.EnsureSuccessStatusCode();

        // Login
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username = "admin@ggs.local", password = "ChangeMe!123" });
        login.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var access = doc.RootElement.GetProperty("accessToken").GetString()!;
        var refresh = doc.RootElement.GetProperty("refreshToken").GetString()!;

        // Call a protected endpoint
        var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        var usersOk = await authed.GetAsync("/api/users");
        usersOk.EnsureSuccessStatusCode();

        // Decode JTI from token (simplified decode, not validated)
        var parts = access.Split('.');
        var payload = JsonSerializer.Deserialize<JsonElement>(System.Text.Encoding.UTF8.GetString(Base64UrlDecode(parts[1])));
        var jti = payload.GetProperty("jti").GetString();

        // Admin revoke JTI
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        var revoke = await admin.PostAsJsonAsync("/api/auth/revoke-jti", new { jti });
        Assert.Equal(System.Net.HttpStatusCode.NoContent, revoke.StatusCode);

        // Subsequent call should be 401 due to revoked token
        var blocked = await authed.GetAsync("/api/users");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, blocked.StatusCode);

        // Wait for refresh token to become invalid (Auth:RefreshTokenMinutes = 1). We'll simulate by setting to 0 and trying refresh immediately.
        await using var factory2 = new WebApplicationFactory<GGs.Server.Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Development")
                .ConfigureAppConfiguration((ctx, cfg) =>
                {
                    cfg.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Auth:RefreshTokenMinutes"] = "0"
                    });
                }));
        var client2 = factory2.CreateClient();
        var login2 = await client2.PostAsJsonAsync("/api/auth/login", new { username = "admin@ggs.local", password = "ChangeMe!123" });
        login2.EnsureSuccessStatusCode();
        var doc2 = JsonDocument.Parse(await login2.Content.ReadAsStringAsync());
        var refresh2 = doc2.RootElement.GetProperty("refreshToken").GetString()!;
        var refreshCall = await client2.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = refresh2, deviceId = "dev" });
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, refreshCall.StatusCode);
    }

    private static byte[] Base64UrlDecode(string input)
    {
        string s = input.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 0: break;
            case 2: s += "=="; break;
            case 3: s += "="; break;
            default: throw new System.Exception("Illegal base64url string!");
        }
        return Convert.FromBase64String(s);
    }
}

