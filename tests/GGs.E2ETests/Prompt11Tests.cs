using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace GGs.E2ETests;

public class Prompt11Tests
{
    [Fact]
    public async Task LicenseIssue_Idempotency_Works_And_RateLimit_429()
    {
        await using var factory = new WebApplicationFactory<GGs.Server.Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Development"));

        using var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username = "admin@ggs.local", password = "ChangeMe!123" });
        login.EnsureSuccessStatusCode();
        var lr = await login.Content.ReadFromJsonAsync<LoginResponse>();
        var token = lr!.accessToken;

        var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Create a user
        var u = await authed.PostAsJsonAsync("/api/users", new { Email = "idemp@example.com", Password = "P@ssw0rd!", Roles = new[] { "User" } });
        u.EnsureSuccessStatusCode();
        var udoc = JsonDocument.Parse(await u.Content.ReadAsStringAsync());
        var userId = udoc.RootElement.GetProperty("id").GetString();

        var body = new { UserId = userId, Tier = "Pro", AllowOfflineValidation = true };

        // First request with Idempotency-Key
        var req1 = new HttpRequestMessage(HttpMethod.Post, "/api/licenses/issue");
        req1.Content = JsonContent.Create(body);
        req1.Headers.TryAddWithoutValidation("Idempotency-Key", "KEY-123");
        var resp1 = await authed.SendAsync(req1);
        resp1.EnsureSuccessStatusCode();
        var license1 = await resp1.Content.ReadAsStringAsync();

        // Second request identical with same key -> should replay same response
        var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/licenses/issue");
        req2.Content = JsonContent.Create(body);
        req2.Headers.TryAddWithoutValidation("Idempotency-Key", "KEY-123");
        var resp2 = await authed.SendAsync(req2);
        resp2.EnsureSuccessStatusCode();
        var license2 = await resp2.Content.ReadAsStringAsync();
        Assert.Equal(license1, license2);

        // Different body with same key -> 409
        var req3 = new HttpRequestMessage(HttpMethod.Post, "/api/licenses/issue");
        req3.Content = JsonContent.Create(new { UserId = userId, Tier = "Admin", AllowOfflineValidation = true });
        req3.Headers.TryAddWithoutValidation("Idempotency-Key", "KEY-123");
        var resp3 = await authed.SendAsync(req3);
        Assert.Equal(System.Net.HttpStatusCode.Conflict, resp3.StatusCode);

        // Rate limit: send 4 quick requests without idempotency to exceed 3/sec
        var r1 = await authed.PostAsJsonAsync("/api/licenses/issue", body);
        var r2 = await authed.PostAsJsonAsync("/api/licenses/issue", body);
        var r3 = await authed.PostAsJsonAsync("/api/licenses/issue", body);
        var r4 = await authed.PostAsJsonAsync("/api/licenses/issue", body);
        Assert.Equal(System.Net.HttpStatusCode.TooManyRequests, r4.StatusCode);
    }

    private sealed class LoginResponse { public string accessToken { get; set; } = string.Empty; }
}

