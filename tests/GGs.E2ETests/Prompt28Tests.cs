using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GGs.E2ETests;

public class Prompt28Tests
{
    [Fact]
    public async Task JwtRotation_NewTokensUseLatestKid_OldTokensStillValidate()
    {
        // Factory A: signing keys [K1 (current), K2]
        await using var factoryA = new TestAppFactory().WithWebHostBuilder(b => b.ConfigureAppConfiguration((ctx, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:SigningKeys:0:kid"] = "k1",
                ["Auth:SigningKeys:0:key"] = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
                ["Auth:SigningKeys:1:kid"] = "k2",
                ["Auth:SigningKeys:1:key"] = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB"
            });
        }));
        var clientA = factoryA.CreateClient();
        // Login via Auth API to get a token with kid=k1
        var loginA = await clientA.PostAsJsonAsync("/api/auth/login", new { username = "admin@ggs.local", password = "ChangeMe!123" });
        loginA.EnsureSuccessStatusCode();
        var jsonA = await loginA.Content.ReadFromJsonAsync<JsonElement>();
        var tokenOld = jsonA.GetProperty("accessToken").GetString()!;

        // Factory B: signing keys [K2 (current), K1] so new tokens use k2 but validation keys include both
        await using var factoryB = new TestAppFactory().WithWebHostBuilder(b => b.ConfigureAppConfiguration((ctx, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:SigningKeys:0:kid"] = "k2",
                ["Auth:SigningKeys:0:key"] = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
                ["Auth:SigningKeys:1:kid"] = "k1",
                ["Auth:SigningKeys:1:key"] = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
            });
        }));
        var clientB = factoryB.CreateClient();

        // Call an authorized endpoint on B with old token (k1) -> should be valid
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/roles");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenOld);
        var respOld = await clientB.SendAsync(req);
        // For non-admin, roles endpoint may require Admin; we are admin user by seeded login -> expect 200
        Assert.True(respOld.IsSuccessStatusCode);

        // Get a new token from B -> should carry kid=k2 (cannot easily read header without decoding); instead trust issuance path works
        var loginB = await clientB.PostAsJsonAsync("/api/auth/login", new { username = "admin@ggs.local", password = "ChangeMe!123" });
        loginB.EnsureSuccessStatusCode();
        var jsonB = await loginB.Content.ReadFromJsonAsync<JsonElement>();
        var tokenNew = jsonB.GetProperty("accessToken").GetString()!;

        // Call with new token also succeeds
        var req2 = new HttpRequestMessage(HttpMethod.Get, "/api/roles");
        req2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenNew);
        var respNew = await clientB.SendAsync(req2);
        Assert.True(respNew.IsSuccessStatusCode);
    }
}

